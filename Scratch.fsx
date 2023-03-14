open System
open System.IO
Directory.GetFiles(@"C:\Program Files", "Dominions5.exe", EnumerationOptions(RecurseSubdirectories=true, AttributesToSkip=FileAttributes.System))
Directory.GetFiles(@"C:\usr\bin", "Dom*.exe", EnumerationOptions(RecurseSubdirectories=true, AttributesToSkip=FileAttributes.System))

#r "nuget: Thoth.Json.Net"

let games () =
    let dir = @"C:\Users\wilso\AppData\Roaming\Dominions5\"
    Directory.GetFiles(dir, "ftherlnd", System.IO.SearchOption.AllDirectories)
        |> Array.append (Directory.GetFiles(dir, "*.trn", System.IO.SearchOption.AllDirectories))
        |> Array.groupBy (Path.GetDirectoryName)
        |> Array.map (fun (dir, files: string array) -> {|
            name = Path.GetFileName dir
            originalDirectory = dir
            originalFiles = files |> Array.map Path.GetFileName
            copiedDirectory = None
            turnTime = DateTimeOffset.Now
            orders = [] |})
games<obj, obj>()
