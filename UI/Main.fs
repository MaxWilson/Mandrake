module UI.Main

open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.Types
open Elmish
open DataTypes.UI
open Avalonia.Layout
open Thoth.Json.Net

let appStatePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Mandrake", "mandrake.json")
let appStateDir = System.IO.Path.GetDirectoryName(appStatePath)

let saveMemory (memory: Model) =
    let json = Encode.Auto.toString memory
    logM "Writing memory to" appStatePath
    if not (System.IO.Directory.Exists appStateDir) then
        System.IO.Directory.CreateDirectory appStateDir |> ignore
    System.IO.File.WriteAllText(appStatePath, json)

let tryLoadMemory() =
    try
        logM "Reading memory from" appStatePath
        if System.IO.File.Exists(appStatePath) then
            let json = System.IO.File.ReadAllText appStatePath
            match Decode.Auto.fromString<Model> json with
            | Ok model -> Some model
            | Result.Error err ->
                log $"Error loading mandrake.json: {err}"
                None
        else None
    with exn ->
        log $"Error loading mandrake.json: {exn}"
        None

let init memory () = (defaultArg memory { games = Map.empty }), Cmd.Empty

type Permutation = {
    name: string
    orders: GameFile list
    }

let justUnlocked (gameName: string, ordersName, game: Game) =
    let justApproved = game.files |> List.find (function { detail = Orders { approved = true } } as file -> file.Name = ordersName | _ -> false)
    let trns = game.files |> List.filter (function { detail = Trn _ } -> true | _ -> false) |> Map.ofListBy (_.Nation >> Option.get)
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
    [   for c in newCombinations do
            for ix in 1..3 do
                let getPermutationName (orders: GameFile list) = [gameName; yield! orders |> List.map _.Name; ix.ToString()] |> String.join "_"
                { orders = c ; name = getPermutationName c }
        ]

let update (fs: FileSystem, ex:ExecutionEngine) msg model =
    match msg with
    | FileSystemMsg(NewGame(game)) ->
        { model with games = Map.change game (Option.orElse (Some { name = game; files = []; children = [] })) model.games }, Cmd.Empty
    | FileSystemMsg(NewFile(game, path, nation, fileName)) ->
        let game = model.games |> Map.tryFind game |> Option.defaultValue { name = game; files = []; children = [] }
        let detail =
            match Path.GetExtension path with
            | ".trn" -> Trn nation
            | ".2h" ->
                let priorIx = game.files |> List.collect (function { detail = Orders { index = ix; nation = nation' } } when nation' = nation -> [ ix ] | _ -> []) |> List.append [0] |> List.max
                Orders { name = None; approved = false; index = priorIx + 1; nation = nation; editing = false }
            | _ -> Other
        let file = { frozenPath = path; detail = detail; fileName = fileName }
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
        let game = { game with children = queue |> List.map (fun permutation -> { name = permutation.name; status = NotStarted }) |> List.append game.children }
        let model = { model with games = Map.add gameName game model.games }
        model,
            Cmd.ofEffect (fun dispatch ->
                backgroundTask {
                    if queue.Length > 0 then saveMemory model // we want to make sure we don't accidentally mistake permutations for real games, even if Mandrake gets closed and reopened.

                    for permutation in queue do
                        // asynchronously: make a new, excluded game directory, copy all of the 2h files + ftherlnd into it, and run Dom5.exe on it, while keeping the UI informed of progress
                        let newGameName = permutation.name
                        let setStatus status =
                            Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                                dispatch (UpdatePermutationStatus(gameName, newGameName, status)))
                        try
                            setStatus InProgress
                            fs.exclude newGameName
                            // copy back ftherlnd and .trn
                            for file in game.files do
                                match file.detail with
                                | Other | Trn _ -> fs.CopyBackToGame(newGameName, file.frozenPath, file.fileName)
                                | Orders _ -> ()
                            for file in permutation.orders do
                                fs.CopyBackToGame(newGameName, file.frozenPath, file.fileName)
                            do! ex.Execute(newGameName, Dom5.hostDom5)
                            setStatus Complete
                        with exn ->
                            log $"Error processing {newGameName}: {exn}"
                            setStatus (Error $"Error processing {newGameName}: {exn}")
                } |> ignore
                )
    | UpdatePermutationStatus(gameName, permutationName, status) ->
        let game = model.games[gameName]
        let game = { game with children = game.children |> List.map (fun p -> if p.name = permutationName then { p with status = status } else p) }
        { model with games = model.games |> Map.add gameName game }, Cmd.Empty
    | SetEditingStatus(gameName, ordersName, v) ->
        let game = model.games[gameName]
        let game = { game with files = game.files |> List.map (function { detail = Orders det } as f when f.Name = ordersName -> { f with detail = Orders { det with editing = v } } | otherwise -> otherwise) }
        { model with games = model.games |> Map.add gameName game }, Cmd.Empty
    | SetName(gameName, ordersName, v) ->
        let game = model.games[gameName]
        let game = { game with files = game.files |> List.map (function { detail = Orders det } as f when f.Name = ordersName -> { f with detail = Orders { det with name = Some v } } | otherwise -> otherwise) }
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
                                    if det.editing then
                                        TextBox.create [
                                            TextBox.text (file.Name)
                                            TextBox.onTextChanged (fun txt -> dispatch (SetName(game.name, file.Name, txt)))
                                            TextBox.onDoubleTapped(fun _ -> dispatch (SetEditingStatus(game.name, file.Name, false)))
                                            ]
                                    else
                                        TextBlock.create [
                                            TextBlock.text (file.Name)
                                            TextBlock.onDoubleTapped(fun _ -> dispatch (SetEditingStatus(game.name, file.Name, true)))
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
                            TextBlock.text $"{permutation.name}: {permutation.status}"
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
