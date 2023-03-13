open System.IO
Directory.GetFiles(@"C:\Program Files", "Dominions5.exe", EnumerationOptions(RecurseSubdirectories=true, AttributesToSkip=FileAttributes.System))
Directory.GetFiles(@"C:\usr\bin", "Dom*.exe", EnumerationOptions(RecurseSubdirectories=true, AttributesToSkip=FileAttributes.System))
Directory.GetFiles(@"C:\Users\wilso\AppData\", "*.2h", EnumerationOptions(RecurseSubdirectories=true, AttributesToSkip=FileAttributes.System))

#r "nuget: Thoth.Json.Net"
open Thoth.Json.Net
open System
type FileSettings = {
    exePath: string option
    dataDirectory: string option
    }

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
    File.WriteAllText(settingsPath, json)
