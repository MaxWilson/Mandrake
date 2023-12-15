module Dom5

open System
open System.IO
open System.Threading.Tasks
open DataTypes

let r = Random()

let ignoreThisFile (file: FullPath) =
    Path.GetDirectoryName file <> "newlords"

let getTempDirPath : _ -> _ -> FullPath * bool=
    let mutable gameDests: Map<string, DirectoryPath> = Map.empty
    fun gameName fileName ->
        match gameDests |> Map.tryFind gameName with
        | Some dest -> Path.Combine(dest, fileName), false
        | None ->
            let dest = Path.Combine (Path.GetTempPath(), gameName)
            let dir = System.IO.Directory.CreateDirectory dest
            if not dir.Exists then shouldntHappen "Couldn't create temp directory"
            gameDests <- Map.add gameName dest gameDests
            Path.Combine(dest, fileName), true

let setupNewWatcher (savedGamesDirectory: DirectoryPath) (onNew, onUpdated) =
    let mutable files =
        Directory.GetFiles(savedGamesDirectory, "ftherlnd", System.IO.SearchOption.AllDirectories)
        |> Array.append (Directory.GetFiles(savedGamesDirectory, "*.trn", System.IO.SearchOption.AllDirectories))
        |> Array.append (Directory.GetFiles(savedGamesDirectory, "*.2h", System.IO.SearchOption.AllDirectories))
        |> Array.filter ignoreThisFile
    files |> Array.iter onNew
    let watcher = new FileSystemWatcher (System.IO.Path.GetFullPath savedGamesDirectory)
    watcher.Changed.Add (fun args -> onUpdated args.FullPath)
    watcher.Created.Add (fun args -> onNew args.FullPath)
    watcher

// let setup (gameTurns: GameTurn list option) (settings: Settings.FileSettings) : GameTurn list Task = backgroundTask {
    // // scan C:\Users\wilso\AppData\Roaming\Dominions5\ or whatever for saved games
    // let uiSynchronizationContext = System.Threading.SynchronizationContext.Current
    // let games =
    //     Directory.GetFiles(settings.dataDirectory.Value, "ftherlnd", System.IO.SearchOption.AllDirectories)
    //     |> Array.append (Directory.GetFiles(settings.dataDirectory.Value, "*.trn", System.IO.SearchOption.AllDirectories))
    //     |> Array.append (Directory.GetFiles(settings.dataDirectory.Value, "*.2h", System.IO.SearchOption.AllDirectories))
    //     |> Array.filter ignoreThisFile
    //     |> Array.groupBy (Path.GetDirectoryName)
    //     |> Array.map (fun (dir, files: string array) ->
    //         {
    //             id = Guid.NewGuid()
    //             name = Path.GetFileName dir
    //             originalDirectory = dir
    //             originalFiles = files |> Array.map Path.GetFileName |> List.ofArray
    //             copiedDirectory = None
    //             turnTime = files |> Array.maxBy' File.GetLastWriteTime
    //             orders = []
    //             })
    // let tryCreate ix game =
    //     try
    //         // copy to C:\Users\wilso\AppData\Local\Temp\ or whatever
    //         let copiedPath = Path.Combine(Path.GetTempPath(), "Mandrake", game.name, ix.ToString())
    //         try
    //             Directory.Delete(copiedPath, true)
    //         with _ -> ()
    //         Directory.CreateDirectory(copiedPath) |> ignore
    //         for file in game.originalFiles do
    //             File.Copy(Path.Combine(game.originalDirectory, file), Path.Combine(copiedPath, file))
    //         let orders =
    //             game.originalFiles
    //             |> List.filter (fun file -> Path.GetExtension file = ".2h")
    //             |> List.map (fun fileName -> Path.Combine(game.originalDirectory, fileName) |> createOrders)
    //         { game with copiedDirectory = Some copiedPath; orders = orders }
    //     with exn ->
    //         Console.Error.WriteLine $"Error creating game: {exn}"
    //         game
    // do! Async.SwitchToContext uiSynchronizationContext
    // return [
    //     for ix, game in games |> Array.mapi Tuple2.create do
    //         match gameTurns |> Option.bind (List.tryFind (fun gameTurn -> gameTurn.name = game.name)) with
    //         | None ->
    //             tryCreate ix game
    //         | Some game ->
    //             game
    //     ]
    // }

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
