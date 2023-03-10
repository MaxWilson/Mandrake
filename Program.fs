// For more information see https://aka.ms/fsharp-console-apps
module Program
open Spectre.Console.Cli
open Commands
open System.Reflection

let app = new CommandApp<StartCommand>()
app.Configure(fun config ->
    config
#if DEBUG
        .PropagateExceptions()
        .ValidateExamples()
#endif
        |> ignore
    )

app.Run (System.Environment.GetCommandLineArgs() |> Array.skip 1) |> ignore