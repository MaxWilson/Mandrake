module Dom5

open System
open System.IO
open System.Threading.Tasks
open DataTypes

let r = Random()

let createOrders (file: FullPath) =
    let copyDest = Path.ChangeExtension(Path.GetTempFileName(), "mandrake")
    File.Copy(file, copyDest)
    {
        id = Guid.NewGuid()
        fileName = file
        time = File.GetLastWriteTime file
        nickName = None
        description = None
        approvedForExecution = false
        copiedFileLocation = copyDest // not necessarily in the same place as the game copiedDirectory, because it doesn't matter as long as we can make new Dom5 directories for them in the same place
        }

let ignoreThisFile (file: FullPath) =
    Path.GetDirectoryName file <> "newlords"

let setup (gameTurns: GameTurn list option) (settings: Settings.FileSettings) : GameTurn list Task = backgroundTask {
    // scan C:\Users\wilso\AppData\Roaming\Dominions5\ or whatever for saved games
    let uiSynchronizationContext = System.Threading.SynchronizationContext.Current
    let games =
        Directory.GetFiles(settings.dataDirectory.Value, "ftherlnd", System.IO.SearchOption.AllDirectories)
        |> Array.append (Directory.GetFiles(settings.dataDirectory.Value, "*.trn", System.IO.SearchOption.AllDirectories))
        |> Array.append (Directory.GetFiles(settings.dataDirectory.Value, "*.2h", System.IO.SearchOption.AllDirectories))
        |> Array.filter ignoreThisFile
        |> Array.groupBy (Path.GetDirectoryName)
        |> Array.map (fun (dir, files: string array) ->
            {
                id = Guid.NewGuid()
                name = Path.GetFileName dir
                originalDirectory = dir
                originalFiles = files |> Array.map Path.GetFileName |> List.ofArray
                copiedDirectory = None
                turnTime = files |> Array.maxBy' File.GetLastWriteTime
                orders = []
                })
    let tryCreate ix game =
        try
            // copy to C:\Users\wilso\AppData\Local\Temp\ or whatever
            let copiedPath = Path.Combine(Path.GetTempPath(), "Mandrake", game.name, ix.ToString())
            try
                Directory.Delete(copiedPath, true)
            with _ -> ()
            Directory.CreateDirectory(copiedPath) |> ignore
            for file in game.originalFiles do
                File.Copy(Path.Combine(game.originalDirectory, file), Path.Combine(copiedPath, file))
            let orders =
                game.originalFiles
                |> List.filter (fun file -> Path.GetExtension file = ".2h")
                |> List.map (fun fileName -> Path.Combine(game.originalDirectory, fileName) |> createOrders)
            { game with copiedDirectory = Some copiedPath; orders = orders }
        with exn ->
            Console.Error.WriteLine $"Error creating game: {exn}"
            game
    do! Async.SwitchToContext uiSynchronizationContext
    return [
        for ix, game in games |> Array.mapi Tuple2.create do
            match gameTurns |> Option.bind (List.tryFind (fun gameTurn -> gameTurn.name = game.name)) with
            | None ->
                tryCreate ix game
            | Some game ->
                game
        ]
    }

let receiveOrders (games: GameTurn list) (file: FullPath) = backgroundTask {
    match games |> List.tryFind (fun game -> game.originalDirectory = Path.GetDirectoryName file) with
    | Some game ->
        return game.id, createOrders file
    | None ->
        return shouldntHappen() // shouldn't happen unless someone is copying .2h files around manually
    }

let fakeHost gameName feedback = task {
    let mutable ticks = 0
    // pretend to run C:\usr\bin\steam\steamapps\common\Dominions5\win64\dominions5.exe  -c -T -g <name>
    let fakeHost = task {
        do! Task.Delay (500 + r.Next 30 * 100)
    }
    let timer = task {
        while(fakeHost.IsCompleted |> not) do
            do! Task.Delay 100
            ticks <- ticks + 1
            feedback ticks
    }
    do! fakeHost
    feedback 100
    return ticks
    }
