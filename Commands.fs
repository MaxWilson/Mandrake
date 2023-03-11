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
module Task =
    let map f t = task { let! v = t in return f v }
    let ignore t = t |> map ignore
    let wait (t: _ System.Threading.Tasks.Task) = t.Wait()
    let runSynchronously (t: _ System.Threading.Tasks.Task) = t.Result
    let waitAll (tasks: _ Task array) = Task.WhenAll(tasks |> Array.map (fun t -> t :> Task))

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

            let trigger (args: FileSystemEventArgs) = task {
                    do! AnsiConsole.Status().StartAsync($"Setting up battle for {args.Name}", (fun _ -> task { do! Task.Delay 1000 }))
                    Out.WriteLine $"Running battles for {args.FullPath}"
                    do! AnsiConsole.Progress().StartAsync (fun ctx -> task {
                        Out.WriteLine $"Running battles for {settings.exePath} with data {settings.dataPath}"
                        let startFor name = (task {
                            let t = ctx.AddTask $"Running '{name}'"
                            while not t.IsFinished do
                                do! Task.Delay 30
                                t.Increment 1.5
                            })
                        let! lines = File.ReadAllLinesAsync args.FullPath
                        do! lines |> Array.filter (not << System.String.IsNullOrWhiteSpace) |> Array.map startFor |> Task.waitAll
                    })
                }

            watcher.Changed.Add (trigger >> ignore)
            watcher.Created.Add (trigger >> ignore)
            watcher.EnableRaisingEvents <- true
            while(true) do do! Task.Delay 1000;
            return 0
        }
