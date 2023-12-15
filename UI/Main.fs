module UI.Main

open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.Types
open Elmish
open DataTypes.UI
open Avalonia.Layout

let init _ = { games = Map.empty }

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
                    approvedOrders[nation] |> List.collect (fun approvedOrder -> permutationsOf (approvedOrder :: accumulatedOrders) rest)
            permutationsOf [justApproved] otherNations
    newCombinations

let update (fs: FileSystem, ex:ExecutionEngine) msg model =
    match msg with
    | FileSystemMsg(NewGame(game)) ->
        { model with games = Map.change game (Option.orElse (Some { name = game; files = [] })) model.games }
    | FileSystemMsg(NewFile(game, path)) ->
        let game = model.games |> Map.tryFind game |> Option.defaultValue { name = game; files = [] }
        let detail =
            match Path.GetExtension path with
            | ".trn" -> Trn
            | ".2h" ->
                let priorIx = game.files |> List.collect (function { detail = Orders { index = ix } } -> [ ix ] | _ -> []) |> List.append [0] |> List.max
                Orders { name = None; approved = false; index = priorIx + 1 }
            | _ -> Other
        let file = { frozenPath = path; detail = detail }
        { model with games = Map.add game.name { game with files = file :: game.files } model.games }
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
        task {
            for orders in queue do
                // asynchronously: make a new, excluded game directory, copy all of the 2h files + ftherlnd into it, and run Dom5.exe on it, while keeping the UI informed of progress
                let newGameName = [gameName; yield! orders |> List.map _.Name] |> String.join "_"
                fs.exclude newGameName
                // copy back ftherlnd
                for file in game.files do
                    match file.detail with
                    | Other -> fs.CopyBackToGame(newGameName, file.frozenPath)
                    | Trn | Orders _ -> ()
                for file in orders do
                    fs.CopyBackToGame(newGameName, file.frozenPath)
                ex.Execute(newGameName)
                // todo: update UI somehow. Will probably be obvious once I'm actually looking at the UI.
        } |> ignore
        { model with games = Map.add gameName game model.games }


let view (model: Model) dispatch : IView =
    View.DockPanel [
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
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text (file.Name)
                                    ]
                                ]
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
