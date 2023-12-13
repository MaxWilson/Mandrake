module POC.Test

open Expecto
open POC

// assertion for asynchronous testing
module Assert =
    let soon f msg =
        task {
            let startTime = System.DateTime.Now
            let mutable passed = f ()

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
        [ testCase "basic"
          <| fun () ->
              let mutable fakeFileSystemWatcher = {| create = ignore; update = ignore |}
              let mutable fakeTempDir = Set.empty

              let fs =
                  FileSystem(fun this ->
                      fakeFileSystemWatcher <-
                          {| create = (fun path -> this.New path)
                             update = (fun path -> this.Updated path) |})

              let engine = ExecutionEngine fs

              let model, dispatch = TestElmish.simpleSynchronous UI.init UI.update

              fs.register
                  { whenNew = (fun path -> dispatch (UI.Msg.Queue(GameId path)))
                    whenUpdated = (fun _ -> ()) }

              fs.New @"foo\ftherlnd"
              fs.New @"foo\xibalba.trn"

              Assert.soon
                  (fun () -> fakeTempDir.Contains @"foo\ftherlnd" && fakeTempDir.Contains @"foo\xibalba.trn")
                  "Game files should be copied to temp directory"

              Assert.eq 2 (3 - 1) ]
