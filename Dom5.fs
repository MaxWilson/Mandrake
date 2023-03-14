module Dom5

open System
open System.IO
open System.Threading.Tasks
let r = Random()

type FileName = string // E.g. "mid_marignon.2h", not a full path.
type FullPath = string

type OrdersVersion = {
    fileName: FileName
    time: DateTimeOffset
    nickName: string option
    description: string option
    approvedForExecution: bool
    }

type GameTurn = {
    name: string
    originalDirectory: FullPath
    originalFiles: FileName list
    copiedDirectory: FullPath option
    turnTime: DateTimeOffset
    orders: OrdersVersion list
    }

let setup (settings: Settings.FileSettings) =
    // scan C:\Users\wilso\AppData\Roaming\Dominions5\ or whatever for saved games
    let games =
        Directory.GetFiles(settings.dataDirectory.Value, "ftherlnd", System.IO.SearchOption.AllDirectories)
        |> Array.append (Directory.GetFiles(settings.dataDirectory.Value, "*.trn", System.IO.SearchOption.AllDirectories))
        |> Array.groupBy (Path.GetDirectoryName)
        |> Array.map (fun (dir, files: string array) ->
            {
                name = Path.GetFileName dir
                originalDirectory = dir
                originalFiles = files |> Array.map Path.GetFileName |> List.ofArray
                copiedDirectory = None
                turnTime = DateTimeOffset.Now
                orders = []
                })
    [   for ix, game in games |> Tuple2.create do
            try
                let path = Path.Combine(Path.GetTempPath(), game.name, ix.ToString())
                Directory.Delete(path, true)
                Directory.CreateDirectory(path)
                for file in game.originalFiles do
                    File.Copy(Path.Combine(game.originalDirectory, file), Path.Combine(path, file))
                { game with copiedDirectory = path }
            with _ ->
                game
        ]

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
