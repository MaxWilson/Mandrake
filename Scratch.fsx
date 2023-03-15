open System
open System.IO
Directory.GetFiles(@"C:\Program Files", "Dominions5.exe", EnumerationOptions(RecurseSubdirectories=true, AttributesToSkip=FileAttributes.System))
Directory.GetFiles(@"C:\usr\bin", "Dom*.exe", EnumerationOptions(RecurseSubdirectories=true, AttributesToSkip=FileAttributes.System))

#r "nuget: Thoth.Json.Net"

let permutations (groups: 't list list) =
    let rec recur (lst: 't list list): 't list list =
        match lst with
        | [] -> []
        | head::tail -> [
            for item in head do
                for t in recur tail do
                    yield! item::t
            ]
    recur [] groups

permutations [[1;2;3];[10]; [7;9;12;19]]

