module UI.Main

open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.Types
open Elmish
open DataTypes.UI
open Avalonia.Layout

let init _ = { games = Map.empty }, Cmd.Empty

let justUnlocked (gameName: string, ordersName, game: Game) =
    let justApproved = game.files |> List.find (function { detail = Orders { approved = true } } as file -> file.Name = ordersName | _ -> false)
    let trns = game.files |> List.filter (function { detail = Trn } -> true | _ -> false) |> Map.ofListBy (_.Nation >> Option.get)
    let approvedOrders = game.files |> List.filter (function { detail = Orders { approved = true } } -> true | _ -> false) |> Map.ofListBy (_.Nation >> Option.get)
    let newCombinations =
        if approvedOrders.Keys.Count <> trns.Keys.Count then [] // wait for more approvals
        else
            let otherNations = trns.Keys |> Seq.filter ((<>) justApproved.Nation.Value) |> List.ofSeq
            let rec permutationsOf (accumulatedOrders: GameFile list) = function
                | [] -> [accumulatedOrders]
                | nation :: rest ->
                    if approvedOrders.ContainsKey nation |> not then
                        shouldntHappen $"Hey, '{nation}' has no approved orders, even though {trns} and {approvedOrders} have the same length."

                    approvedOrders[nation] |> List.collect (fun approvedOrder -> permutationsOf (approvedOrder :: accumulatedOrders) rest)
            permutationsOf [justApproved] otherNations
    newCombinations

let update (fs: FileSystem, ex:ExecutionEngine) msg model =
    match msg with
    | FileSystemMsg(NewGame(game)) ->
        { model with games = Map.change game (Option.orElse (Some { name = game; files = []; children = [] })) model.games }, Cmd.Empty
    | FileSystemMsg(NewFile(game, path, nation)) ->
        let game = model.games |> Map.tryFind game |> Option.defaultValue { name = game; files = []; children = [] }
        let detail =
            match Path.GetExtension path with
            | ".trn" -> Trn
            | ".2h" ->
                let priorIx = game.files |> List.collect (function { detail = Orders { index = ix; nation = nation' } } when nation' = nation -> [ ix ] | _ -> []) |> List.append [0] |> List.max
                Orders { name = None; approved = false; index = priorIx + 1; nation = nation }
            | _ -> Other
        let file = { frozenPath = path; detail = detail }
        { model with games = Map.add game.name { game with files = file :: game.files } model.games }, Cmd.Empty
    | Approve(gameName, ordersName) ->
        let game = {
            model.games[gameName]
            with
                files =
                    model.games[gameName].files
                    |> List.map (function
                        | { detail = Orders det } as f when f.Name = ordersName -> { f with detail = Orders { det with approved = true } }
                        | otherwise -> otherwise
                    )
            }
        let queue = justUnlocked(gameName, ordersName, game)
        let getPermutationName (orders: GameFile list) = [gameName; yield! orders |> List.map _.Name] |> String.join "_"
        let game = { game with children = queue |> List.map (fun orders -> { name = getPermutationName orders; status = NotStarted }) |> List.append game.children }
        { model with games = Map.add gameName game model.games },
            Cmd.ofEffect (fun dispatch ->
                task {
                    for orders in queue do
                        // asynchronously: make a new, excluded game directory, copy all of the 2h files + ftherlnd into it, and run Dom5.exe on it, while keeping the UI informed of progress
                        let newGameName = getPermutationName orders
                        dispatch (UpdatePermutationStatus(gameName, newGameName, InProgress))
                        fs.exclude newGameName
                        // copy back ftherlnd
                        for file in game.files do
                            match file.detail with
                            | Other -> fs.CopyBackToGame(newGameName, file.frozenPath)
                            | Trn | Orders _ -> ()
                        for file in orders do
                            fs.CopyBackToGame(newGameName, file.frozenPath)
                        ex.Execute(newGameName)
                        Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                            dispatch (UpdatePermutationStatus(gameName, newGameName, Complete)))
                } |> ignore
                )
    | UpdatePermutationStatus(gameName, permutationName, status) ->
        let game = model.games[gameName]
        let game = { game with children = game.children |> List.map (fun p -> if p.name = permutationName then { p with status = status } else p) }
        { model with games = model.games |> Map.add gameName game }, Cmd.Empty


let view (model: Model) dispatch : IView =
    View.StackPanel [
        TextBlock.create [
            TextBlock.classes ["title"]
            TextBlock.text $"Games"
            ]
        for game in model.games.Values do
            StackPanel.create [
                StackPanel.orientation Orientation.Vertical
                StackPanel.children [
                    TextBlock.create [
                        TextBlock.classes ["subtitle"]
                        TextBlock.text (game.name)
                        // TextBox.onTextChanged (fun txt -> exePath.Set (Some txt); exePathValid.Set ((String.IsNullOrWhiteSpace txt |> not) && File.Exists txt))
                        ]
                    for file in game.files do
                        match file.detail with
                        | Orders det ->
                            StackPanel.create [
                                StackPanel.orientation Orientation.Horizontal
                                StackPanel.children [
                                    TextBlock.create [
                                        TextBlock.text (file.Name)
                                        ]
                                    if not det.approved then
                                        let name = file.Name
                                        Button.create [
                                            Button.content $"Approve {name} at {file.frozenPath}"
                                            Button.onClick(fun _ -> dispatch (Approve(game.name, name)))
                                            ]
                                    ]
                                ]
                        | _ -> ()
                    if game.children.Length > 0 then
                        TextBlock.create [
                            TextBlock.classes ["subtitle"]
                            TextBlock.text (game.name)
                            // TextBox.onTextChanged (fun txt -> exePath.Set (Some txt); exePathValid.Set ((String.IsNullOrWhiteSpace txt |> not) && File.Exists txt))
                            ]
                    for permutation in game.children do
                        TextBlock.create [
                            TextBlock.text $"{permutation.status}: {permutation.name}"
                            ]
                    ]
                ]
        ]
    // match model.fileSettings with
    // | { exePath = Some exePath
    //     dataDirectory = Some dataDirectory } when not model.showSettings ->
    //     View.DockPanel
    //         [ AcceptanceQueue.view
    //               model.acceptance
    //               ({ AcceptanceQueue.approved = ExecutionQueue.Queue >> Execution >> dispatch
    //                  AcceptanceQueue.showSettings = thunk1 dispatch ShowSettings })
    //               (dispatch << Acceptance)
    //           ExecutionQueue.view
    //               model.acceptance
    //               ({ ExecutionQueue.finished = notImpl Executed.Queue >> Results >> dispatch })
    //               (dispatch << Execution)
    //           Executed.view model.acceptance (dispatch << Results) ]
    // | _ ->
    //     let onSend settings =
    //         Settings.saveFileSettings settings
    //         dispatch (ReceiveFileSettings settings)

    //     UI.Settings.view ({ ok = onSend }, model.fileSettings)
