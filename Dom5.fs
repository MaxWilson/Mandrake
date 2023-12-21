module Dom5

open System
open System.IO
open System.Threading.Tasks
open DataTypes

let r = Random()

let ignoreThisFile (file: FullPath) =
    let getDirectoryName (path: FullPath) = path |> Path.GetDirectoryName |> Path.GetFileName
    getDirectoryName file = "newlords"

module File =
    let equivalent src dest =
        let srcInfo = FileInfo(src)
        let destInfo = FileInfo(dest)
        srcInfo.LastWriteTime = destInfo.LastWriteTime && srcInfo.Length = destInfo.Length

let getTempDirPath : _ -> _ -> FullPath * bool=
    let mutable gameDests: Map<string, DirectoryPath> = Map.empty
    let uniquePath dest (filePath: FullPath) =
        let lastInfo = FileInfo(filePath)
        let fileName = Path.GetFileName filePath
        let path = Path.Combine(dest, fileName)
        let rec recur ix =
            let fileName = Path.GetFileNameWithoutExtension fileName + (if ix = 1 then "" else ix.ToString()) + Path.GetExtension fileName
            let path = Path.Combine(dest, fileName)
            if not (File.Exists path) || (let info = FileInfo(path) in info.LastWriteTime = lastInfo.LastWriteTime && info.Length = lastInfo.Length) then
                path // if the the Length and last write time match we'll assume the files are equivalent and the path is good, and if there's no file at all then that's also good
            else
                recur (ix + 1)
        recur 1
    fun gameName (filePath:FullPath) ->
        match gameDests |> Map.tryFind gameName with
        | Some dest -> uniquePath dest filePath, false
        | None ->
            let dest = Path.Combine (Path.GetTempPath(), "Mandrake", gameName)
            let dir = System.IO.Directory.CreateDirectory dest
            if not dir.Exists then shouldntHappen "Couldn't create temp directory"
            gameDests <- Map.add gameName dest gameDests
            uniquePath dest filePath, true

let robustCopy src dest =
    // minimally robust currently (just retry once a second later) but we can improve if needed
    let rec attempt (nextDelay: int) =
        task {
            try
                System.IO.File.Copy(src, dest, true)
            with
            | err when nextDelay < 2000 ->
                do! Task.Delay nextDelay
                return! attempt (nextDelay * 3)
            }
    if File.equivalent src dest then
        // nothing to do
        ()
    else
        attempt 100
        |> fun t -> t.Wait()
let copyIfNewer (src, dest) =
    if System.IO.File.Exists src then
        let srcInfo = System.IO.FileInfo(src)
        let destInfo = System.IO.FileInfo(dest)
        if srcInfo.LastWriteTime > destInfo.LastWriteTime then
            robustCopy src dest
let copyBack (src, gameName) =
    let dest = Path.Combine(@"C:\Users\wilso\AppData\Roaming\Dominions5\savedGames", gameName)
    robustCopy src dest


let setupNewWatcher (savedGamesDirectory: DirectoryPath) (onNew, onUpdated) =
    let mutable files =
        Directory.GetFiles(savedGamesDirectory, "ftherlnd", System.IO.SearchOption.AllDirectories)
        |> Array.append (Directory.GetFiles(savedGamesDirectory, "*.trn", System.IO.SearchOption.AllDirectories))
        |> Array.append (Directory.GetFiles(savedGamesDirectory, "*.2h", System.IO.SearchOption.AllDirectories))
        |> Array.filter (not << ignoreThisFile)
    files |> Array.iter onNew
    let watcher = new FileSystemWatcher (System.IO.Path.GetFullPath savedGamesDirectory)
    watcher.Changed.Add (fun args -> onUpdated args.FullPath)
    watcher.Created.Add (fun args -> onNew args.FullPath)
    watcher.IncludeSubdirectories <- true
    watcher.EnableRaisingEvents <- true
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
