module Commands

open System
open System.IO
open System.Threading.Tasks

let start dataPath = task {
            Console.WriteLine "Mandrake"
            Console.WriteLine "A battle tester for Dominions 5"
            Console.WriteLine $"Watching {dataPath}"
            use watcher = new FileSystemWatcher (System.IO.Path.GetFullPath dataPath)

            let mutable tickCounts = []
            let trigger (args: FileSystemEventArgs) = task {
                    //do! AnsiConsole.Status().StartAsync($"Setting up battle for {args.Name}", (fun _ -> task { do! Task.Delay 1000 }))
                    Console.WriteLine $"Running battles for {args.FullPath}"
                    Console.WriteLine $"Running battles for {args.Name}"
                    let setup name expectedTicks = task {
                        let! total = Dom5.fakeHost name ignore
                        return total
                        }
                    let lines = [| for ix in 1..10 -> $"{args.Name}{ix}" |> setup |]
                    for line in lines do
                        // by deliberately overestimating the amount of time it will take to finish we may improve the UX/perceived performance/user satisfaction, by reducing how often it "hangs" at 97% due to a bad estimate.
                        let! total = line (if tickCounts.IsEmpty then None else tickCounts |> List.map float |> List.average |> ((*) 1.2) |> int |> Some)
                        tickCounts <- total::tickCounts
                }

            watcher.Changed.Add (trigger >> ignore)
            watcher.Created.Add (trigger >> ignore)
            watcher.EnableRaisingEvents <- true
            while(true) do do! Task.Delay 1000;
            return 0
        }
