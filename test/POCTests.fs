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
                failwith msg
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
                                {|  create = fun (name, path) -> this.New path
                                    update = fun (name, path) -> this.Updated path |}
                    )

                let engine = ExecutionEngine fs

                let model, dispatch = TestElmish.simpleSynchronous UI.init UI.update
                fs.register dispatch

                fs.New @"blahblahblah\foo\ftherlnd"
                fs.New @"blahblahblah\foo\xibalba.trn"

                Assert.soon
                    (fun () ->
                        fakeTempDir["foo"] |> List.contains @"ftherlnd"
                        && fakeTempDir["foo"] |> List.contains @"xibalba.trn")
                    $"Game files should be copied to temp directory but found only {fakeTempDir}"

                Assert.soon
                    (fun () ->
                        try
                            model.Value.games.["foo"].files |> List.exists (fun f -> f.detail = Trn && f.Name = "xibalba")
                        with _ -> false)
                    $"Game should be added to model but found only {model.Value.games}"
            ]
