namespace Commands

open Spectre.Console
open Spectre.Console.Cli
open System.Threading.Tasks
open System.IO

type Out =
    static member Figlet(txt: string, ?color: Color) =
        FigletText(txt, Color = defaultArg color Color.Blue)
        |> AnsiConsole.Write
    static member WriteLine(txt: string, ?color: Color option) =
        Panel(txt).RoundedBorder().Collapse().BorderColor(Color.Yellow)
        |> AnsiConsole.Write

type Settings() =
    inherit CommandSettings()
    // add a Spectre.Console setting for exePath
    [<CommandOption("-x | --exePath")>]
    member val exePath : string = "" with get, set
    [<CommandOption("-d | --dataDirectory")>]
    member val dataPath : string = "" with get, set

type StartCommand() =
    inherit Command<Settings>()
    override _.Execute(ctx, settings) = Task.runSynchronously <| task {
            Out.Figlet "Mandrake"
            Out.WriteLine "A battle tester for Dominions 5"
            Panel $"Watching {settings.dataPath}" |> AnsiConsole.Write
            use watcher = new FileSystemWatcher (System.IO.Path.GetFullPath settings.dataPath)

            let mutable tickCounts = []
            let trigger (args: FileSystemEventArgs) = task {
                    do! AnsiConsole.Status().StartAsync($"Setting up battle for {args.Name}", (fun _ -> task { do! Task.Delay 1000 }))
                    Out.WriteLine $"Running battles for {args.FullPath}"
                    do! AnsiConsole.Progress().StartAsync (fun ctx -> task {
                        Out.WriteLine $"Running battles for {args.Name}"
                        let setup name =
                            let mutable percentage = 0.
                            let progressBar = ctx.AddTask $"Hosting for '{name}'" // by running C:\usr\bin\steam\steamapps\common\Dominions5\win64\dominions5.exe  -c -T -g Warhammer {name}
                            fun expectedTicks -> task {
                                if expectedTicks |> Option.isSome then
                                    progressBar.Description <- $"Hosting for '{name}', ETA {expectedTicks.Value} seconds" // show latest ETA based on prior completions
                                let registerProgress tickReached =
                                    let percentage' = (float tickReached / float (defaultArg expectedTicks 100)) * 100. |> min 97.
                                    if percentage' > percentage then
                                        progressBar.Increment (percentage' - percentage)
                                        percentage <- percentage'
                                let! total = Dom5.fakeHost name registerProgress
                                progressBar.Increment 100 // make sure it's done and not just stuck at 99.9%
                                return total
                                }
                        let lines = [| for ix in 1..10 -> $"{args.Name}{ix}" |> setup |] // File.ReadAllLinesAsync args.FullPath
                        for line in lines do
                            // by deliberately overestimating the amount of time it will take to finish we may improve the UX/perceived performance/user satisfaction, by reducing how often it "hangs" at 97% due to a bad estimate.
                            let! total = line (if tickCounts.IsEmpty then None else tickCounts |> List.map float |> List.average |> ((*) 1.2) |> int |> Some)
                            tickCounts <- total::tickCounts
                    })
                }

            watcher.Changed.Add (trigger >> ignore)
            watcher.Created.Add (trigger >> ignore)
            watcher.EnableRaisingEvents <- true
            while(true) do do! Task.Delay 1000;
            return 0
        }
