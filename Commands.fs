namespace Commands

open Spectre.Console
open Spectre.Console.Cli

type Settings() =
    inherit CommandSettings()
    // add a Spectre.Console setting for exePath
    [<CommandOption("-x | --exePath")>]
    member val exePath : string = "" with get, set
    [<CommandOption("-d | --dataDirectory")>]
    member val dataPath : string = "" with get, set

type StartCommand() =
    inherit Command<Settings>()
    override _.Execute(ctx, settings) =
        printfn $"Running battles for {settings.exePath} with data {settings.dataPath}"
        0
