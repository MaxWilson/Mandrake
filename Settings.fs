module Settings

open System
open System.IO
open Thoth.Json.Net

type FileSettings = {
    exePath: string option
    dataDirectory: string option
    }

// e.g. c:\users\wilso\AppData\Roaming\Mandrake\settings.json
let settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mandrake", "settings.json")

let getFileSettings() =
    let fresh = { exePath = None; dataDirectory = None }
    if File.Exists settingsPath then
        try
            let json = File.ReadAllText settingsPath
            match Decode.Auto.fromString<FileSettings> json with
            | Ok settings -> settings
            | Error _ -> fresh
        with _ -> fresh
    else fresh

let saveFileSettings settings =
    let json = Encode.Auto.toString settings
    Directory.CreateDirectory(Path.GetDirectoryName settingsPath) |> ignore
    File.WriteAllText(settingsPath, json)
