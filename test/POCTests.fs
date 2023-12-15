module POC.Test

open Expecto
open POC
open POC.UI

// assertion for asynchronous testing
module Assert =
    let soon f msg =
        task {
            let startTime = System.DateTime.Now
            let mutable passed = f()

            while (not passed && (System.DateTime.Now - startTime).TotalMilliseconds <= 1000) do
                do! Async.Sleep 50
                passed <- f ()

            if not passed then
                failwith (msg())
        }
        |> Task.wait

    let eq expected actual =
        if expected <> actual then
            failwithf $"Expected {expected} but got {actual}"

[<Tests>]
let tests =
    testList
        "POC"
        [   testCase "basic" <| fun () ->
                let mutable fakeFileSystemWatcher = {| create = ignore; update = ignore |}
                let mutable fakeTempDir = Map.empty
                let mutable fakeGameDir = Map.empty

                let fs =
                    let fakeCopy (name, path: string, _dest) =
                        fakeTempDir <-
                            fakeTempDir
                            |> Map.change
                                name
                                (Option.orElse (Some [])
                                >> Option.map (List.append [ path |> System.IO.Path.GetFileName ]))
                    FileSystem(
                        fakeCopy,
                        fun this ->
                            fakeFileSystemWatcher <-
                                {|  create = fun path -> this.New path
                                    update = fun path -> this.Updated path |}
                    )

                let engine = ExecutionEngine fs

                let model, dispatch = TestElmish.simpleSynchronous UI.init UI.update
                fs.register (FileSystemMsg >> dispatch)

                fakeFileSystemWatcher.create @"blahblahblah\foo\ftherlnd"
                fakeFileSystemWatcher.create @"blahblahblah\foo\xibalba.trn"

                Assert.soon
                    (fun () ->
                        fakeTempDir["foo"] |> List.contains @"ftherlnd"
                        && fakeTempDir["foo"] |> List.contains @"xibalba.trn")
                    (fun () -> $"Game files should be copied to temp directory but found only {fakeTempDir}")

                let hasFile gameName fileName () = try model.Value.games.[gameName].files |> List.exists (fun f -> f.Name = fileName) with _ -> false
                let missingFile fMsg () : string =
                    let games = model.Value.games |> Map.map(fun key game -> game.files |> List.map (fun f -> $@"{key}\{f.Name}"))
                    fMsg (games.Values |> List.ofSeq |> List.collect id)

                Assert.soon
                    (hasFile "foo" "xibalba.trn")
                    (missingFile (sprintf @"foo\xibalba.trn should be added to model but found only %A"))
                fakeFileSystemWatcher.create @"blahblahblah\foo\xibalba.2h"
                Assert.soon
                    (hasFile "foo" "xibalba1")
                    (missingFile (sprintf @"foo\xibalba.2h should be added to model as xibalba1 but found only %A"))
            ]
