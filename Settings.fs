module Settings

open System
open System.IO
open Thoth.Json.Net
open DataTypes

type FileSettings = {
    exePath: string option
    dataDirectory: string option
    }

// e.g. c:\users\wilso\AppData\Roaming\Mandrake\settings.json
let settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mandrake", "settings.json")

let mutable dom5Path, dom5Saves = // e.g. @"C:\usr\bin\steam\steamapps\common\Dominions5\win64\dominions5.exe", @"C:\Users\wilso\AppData\Roaming\Dominions5\savedGames"
    let fresh = None, None
    try
        if File.Exists settingsPath then
                let json = File.ReadAllText settingsPath
                match Decode.Auto.fromString<FileSettings> json with
                | Ok settings -> settings.exePath, settings.dataDirectory
                | Error _ -> fresh
        else fresh
    with _ -> fresh

let saveFileSettings () =
    let json = Encode.Auto.toString { exePath = dom5Path; dataDirectory = dom5Saves }
    Directory.CreateDirectory(Path.GetDirectoryName settingsPath) |> ignore
    File.WriteAllText(settingsPath, json)

