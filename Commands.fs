namespace Commands

open Spectre.Console
open Spectre.Console.Cli
open System.Threading.Tasks

type Out =
    static member WriteLine(txt: string, ?color: Color option) =
        Panel(txt).RoundedBorder().Collapse().BorderColor(Color.Yellow)
        |> AnsiConsole.Write
module Task =
    let map f t = task { let! v = t in return f v }
    let ignore t = t |> map ignore
    let wait (t: _ System.Threading.Tasks.Task) = t.Wait()
    let runSynchronously (t: _ System.Threading.Tasks.Task) = t.Result

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
            do! AnsiConsole.Status().StartAsync("Setting up battle", (fun _ -> task { do! Task.Delay 1000 }))
            do! AnsiConsole.Progress().StartAsync (fun ctx -> task {
                Out.WriteLine $"Running battles for {settings.exePath} with data {settings.dataPath}"
                let t = ctx.AddTask "Running a battle"
                let t2 = ctx.AddTask "Running a battle"
                while (t.IsFinished && t2.IsFinished) |> not do
                    do! Task.Delay 30
                    t.Increment 1.5
                    t2.Increment 0.5
            })
            return 0
        }
