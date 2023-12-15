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
// e.g. c:\users\wilso\AppData\Roaming\Mandrake\gameData.json
let gameDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mandrake", "gameData.json")

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

let saveFileSettings (settings: FileSettings) =
    let json = Encode.Auto.toString settings
    Directory.CreateDirectory(Path.GetDirectoryName settingsPath) |> ignore
    File.WriteAllText(settingsPath, json)

let getGameTurns() =
    let fresh = None
    if File.Exists gameDataPath then
        try
            let json = File.ReadAllText gameDataPath
            match Decode.Auto.fromString<Map<string, UI.Game>> json with
            | Ok settings -> Some settings
            | Error _ -> fresh
        with _ -> fresh
    else fresh

let key = obj()
let saveGameTurns (gameTurns: Map<string, UI.Game>) =
    lock key (fun () ->
        let json = Encode.Auto.toString gameTurns
        Directory.CreateDirectory(Path.GetDirectoryName gameDataPath) |> ignore
        File.WriteAllText(gameDataPath, json)
        )

