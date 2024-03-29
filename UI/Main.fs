module UI.Main

open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.Types
open Elmish
open DataTypes.UI
open Avalonia.Layout
open Thoth.Json.Net
open Settings
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL

let saveMemory (memory: GlobalModel) =
    let json = Encode.Auto.toString memory
    if not (System.IO.Directory.Exists appStateDir) then
        System.IO.Directory.CreateDirectory appStateDir |> ignore
    System.IO.File.WriteAllText(appStatePath, json)

let tryLoadMemory() =
    try
        logM "Reading memory from" appStatePath
        if System.IO.File.Exists(appStatePath) then
            let json = System.IO.File.ReadAllText appStatePath
            match Decode.Auto.fromString<GlobalModel> json with
            | Ok model -> Some model
            | Result.Error err ->
                log $"Error loading mandrake.json: {err}"
                None
        else None
    with exn ->
        log $"Error loading mandrake.json: {exn}"
        None

let init memory ()  =
    let settings = loadSettings()
    let model = memory |> Option.defaultWith (fun () -> GlobalModel.fresh settings)
    { model with settings = settings }, Cmd.Empty

type Permutation = {
    name: string
    orders: GameFile list
    }

let justUnlocked (N, gameName: string, ordersName, game: Game) =
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
            for ix in 1..N do
                let getPermutationName (orders: GameFile list) = ["test"; gameName.Substring(0, min gameName.Length 3); yield! orders |> List.map _.Name; ix.ToString("00")] |> String.join "_" |> fun s -> s.Trim().Replace(" ", "").Replace("_", "")
                { orders = c ; name = getPermutationName c }
        ]

let update (fs: FileSystem, ex:ExecutionEngine) msg model =
    match msg with
    | SaveAndCloseSettingsDialog settings ->
        { model with settings = settings }, Cmd.Empty
    | FileSystemMsg(NewGame(game)) ->
        { model with games = Map.change game (Option.orElse (Some (Game.create game))) model.games }, Cmd.Empty
    | FileSystemMsg(NewFile(game, path, nation, fileName, lastWriteTime)) ->
        let game = model.games |> Map.tryFind game |> Option.defaultValue (Game.create game)
        if game.files |> List.exists (fun f -> f.fileName = fileName && f.lastWriteTime = lastWriteTime) then model, Cmd.Empty // ignore the duplicate
        else
            let detail =
                match Path.GetExtension path with
                | ".trn" -> Trn nation
                | ".2h" ->
                    let priorIx = game.files |> List.collect (function { detail = Orders { index = ix; nation = nation' } } when nation' = nation -> [ ix ] | _ -> []) |> List.append [0] |> List.max
                    Orders { name = None; approved = false; index = priorIx + 1; nation = nation; editing = false }
                | _ -> Other
            let file = { frozenPath = path; detail = detail; fileName = fileName; lastWriteTime = lastWriteTime }
            { model with games = Map.add game.name { game with files = file :: game.files } model.games },
                // auto-approve if enabled
                match file.detail with Orders _ when model.autoApprove -> Cmd.ofMsg (Approve(game.name, file.Name)) | _ -> Cmd.Empty
    | SetAutoApprove v -> { model with autoApprove = v }, Cmd.Empty
    | ReplicationCountChange(gameName, v) ->
        let game = model.games[gameName]
        let game = { game with replicationCount = v }
        { model with games = Map.add gameName game model.games }, Cmd.Empty
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
        let queue = justUnlocked(game.replicationCount, gameName, ordersName, game)
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
                                | Other | Trn _ -> do! fs.CopyBackToGame(newGameName, file.frozenPath, file.fileName)
                                | Orders _ -> ()
                            for file in permutation.orders do
                                do! fs.CopyBackToGame(newGameName, file.frozenPath, file.fileName)
                            do! System.Threading.Tasks.Task.Delay 100 // klduge for mysterious error: give ftherlnd a chance to finish writing
                            do! ex.Execute(newGameName, Dom5.hostDom5)
                            setStatus Complete
                        with exn ->
                            log $"Error processing {newGameName}: {exn}"
                            setStatus (ErrorState $"Error processing {newGameName}: {exn}")
                } |> ignore
                )
    | DeleteOrders(gameName, ordersName) ->
        let game = model.games[gameName]
        let game = { game with files = game.files |> List.filter (fun f -> f.Name <> ordersName) }
        let model = { model with games = model.games |> Map.add gameName game }
        saveMemory model
        model, Cmd.Empty
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
    | DeletePermutation(gameName, permutationName) ->
        let game = model.games[gameName]
        match game.children |> List.tryFind (fun p -> p.name = permutationName) with
        | None -> model, Cmd.Empty
        | Some c ->
            let game = { game with children = game.children |> List.filter (fun p -> p.name <> permutationName) }
            let model = { model with games = model.games |> Map.add gameName game }
            backgroundTask { saveMemory model; fs.Delete permutationName } |> ignore
            model, Cmd.Empty

let viewGames (model: GlobalModel) dispatch : IView =
    let stack content =
        ScrollViewer.create [
            ScrollViewer.content (
                StackPanel.create [
                    StackPanel.children content
                    ])
            ]
    stack [
        TextBlock.create [
            TextBlock.classes ["title"]
            TextBlock.text $"Games"
            ]
        Button.create [
            Button.content $"Auto-Approve incoming orders: {model.autoApprove}"
            Button.onClick (fun _ -> dispatch (SetAutoApprove (not model.autoApprove)))
            ]
        let panel orientation children =
            StackPanel.create [
                StackPanel.orientation orientation
                StackPanel.children children
                ]
        for game in model.games.Values do
            panel Orientation.Vertical [
                TextBlock.create [
                    TextBlock.classes ["subtitle"]
                    TextBlock.text (game.name)
                    ]
                panel Orientation.Horizontal [
                    TextBlock.create [
                        TextBlock.text "Replication count"
                        ]
                    NumericUpDown.create [
                        NumericUpDown.minimum 1
                        NumericUpDown.watermark "Replication count"
                        NumericUpDown.value game.replicationCount
                        NumericUpDown.formatString "0"
                        NumericUpDown.onValueChanged (fun v -> dispatch (ReplicationCountChange(game.name, System.Convert.ToInt32 v)))
                        ]
                    ]

                for file in game.files do
                    match file.detail with
                    | Orders det ->
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                if det.editing then
                                    TextBox.create [
                                        TextBox.text file.Name
                                        TextBox.onTextChanged (fun txt -> dispatch (SetName(game.name, file.Name, txt)))
                                        TextBox.onDoubleTapped(fun _ -> dispatch (SetEditingStatus(game.name, file.Name, false)))
                                        TextBox.onKeyDown(fun e ->
                                            if ["Return"; "Enter"] |> List.contains (e.Key.ToString()) then
                                                dispatch (SetEditingStatus(game.name, file.Name, false))
                                            )
                                        ]
                                else
                                    TextBlock.create [
                                        TextBlock.text (file.Name)
                                        TextBlock.onDoubleTapped(fun _ -> dispatch (SetEditingStatus(game.name, file.Name, true)))
                                        ]
                                TextBlock.create [
                                    let age =
                                        let elapsed = (System.DateTimeOffset.UtcNow - file.lastWriteTime)
                                        if elapsed.TotalDays > 1 then $"{int elapsed.TotalDays} days ago"
                                        elif elapsed.TotalHours > 1 then $"{int elapsed.TotalHours} hours ago"
                                        elif elapsed.TotalMinutes > 1 then $"{int elapsed.TotalMinutes} minutes ago"
                                        else $"just now"
                                    TextBlock.text $" ({age})"
                                    TextBlock.onDoubleTapped(fun _ -> dispatch (SetEditingStatus(game.name, file.Name, true)))
                                    ]
                                if not det.approved then
                                    let name = file.Name
                                    Button.create [
                                        Button.content $"Approve {name}"
                                        Button.onClick(fun _ -> dispatch (Approve(game.name, name)))
                                        ]
                                Button.create [
                                    let name = file.Name
                                    Button.content $"Delete {name}"
                                    Button.onClick(fun _ -> dispatch (DeleteOrders(game.name, name)))
                                    ]
                                ]
                            ]
                    | _ -> ()
                if game.children.Length > 0 then
                    TextBlock.create [
                        TextBlock.classes ["subtitle"]
                        TextBlock.text "    Permutations"
                        ]
                    for permutation in game.children do
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text $"        {permutation.name}: {permutation.status}"
                                    ]
                                Button.create [
                                    Button.content $"Delete {permutation.name}"
                                    Button.onClick (fun _ -> dispatch (DeletePermutation(game.name, permutation.name)))
                                    ]
                                ]
                            ]
                ]
        ]



let view fs (model:GlobalModel) dispatch =
    if model.settings.dominionsExePath.isValid && model.settings.userDataDirectoryPath.isValid then
        viewGames model dispatch
    else
        let key = $"{model.settings}"
        viewSettings key (Settings.cmdSaveAndCloseSettings fs dispatch)