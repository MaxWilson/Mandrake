module Settings

open System
open System.IO
open Thoth.Json.Net
open DataTypes
open DataTypes.UI

// e.g. c:\users\wilso\AppData\Roaming\Mandrake\settings.json
let appStateDir = System.Reflection.Assembly.GetEntryAssembly().Location |> System.IO.Path.GetDirectoryName
let settingsPath = Path.Combine(appStateDir, "settings.json")
let appStatePath = System.IO.Path.Combine(appStateDir, "mandrake.json")

let loadSettings() =
    let fresh = SettingsModel.fresh
    try
        if File.Exists settingsPath then
            let json = File.ReadAllText settingsPath
            match Decode.Auto.fromString<SettingsModel> json with
            | Ok settings -> settings
            | Error _ -> fresh
        else fresh
    with _ -> fresh

let mutable domExePath, userDataDirectory = // e.g. @"C:\usr\bin\steam\steamapps\common\Dominions5\win64\dominions5.exe", @"C:\Users\wilso\AppData\Roaming\Dominions5\savedGames"
    let settings = loadSettings()
    let extract = function Valid v -> Some v | Invalid _ -> None
    extract settings.dominionsExePath, extract settings.userDataDirectoryPath

let saveFileSettings (settings: SettingsModel) =
    let json = Encode.Auto.toString settings
    Directory.CreateDirectory(Path.GetDirectoryName settingsPath) |> ignore
    File.WriteAllText(settingsPath, json)

