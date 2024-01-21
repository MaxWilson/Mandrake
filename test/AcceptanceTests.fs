module Test

open Expecto
open DataTypes.UI
open UI.Main
open System.Threading.Tasks

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

module TestElmish =
    let simpleSynchronous init update =
        let model = ref (init ())
        let dispatch msg = model.Value <- update msg model.Value
        model, dispatch
    let synchronous init update =
        let initModel, initCmds = init()
        let model = ref (initModel)
        let mutable cmds: _ list = initCmds
        let rec dispatch msg =
            let model', cmds' = update msg model.Value
            model.Value <- model'
            cmds <- cmds'@cmds
            while not (List.isEmpty cmds) do
                match cmds with
                | [] -> ()
                | cmd::tail ->
                    cmds <- tail
                    cmd dispatch
        model, dispatch

[<Tests>]
let tests =
    testList "AcceptanceTests" [
        testCase "basic" <| fun () ->
            let mutable fakeFileSystemWatcher = {| create = ignore; update = ignore |}
            let mutable fakeTempDir = Map.empty
            let mutable fakeGameDir: Map<string, string list> = Map.empty

            let fs =
                let fakeCopy (_path, dest: string) =
                    // in this case we completely ignore the incoming path because everything we need has been faked into the dest argument
                    let path, dest = Path.GetFileName dest, Path.GetDirectoryName dest
                    fakeTempDir <- fakeTempDir |> Map.addMulti dest path
                    Task.FromResult ()
                let copyBack (gameName, src: FullPath, destFileName) =
                    fakeGameDir <- fakeGameDir |> Map.addMulti gameName (Path.GetFileName src)
                    Task.FromResult ()
                FileSystem(
                    (fun gameName path -> Path.Combine(gameName, Path.GetFileName path), true), // always pretend to be new, and create a path name that looks like a plausible path but can also be easily parsed by fakeCopy
                    fakeCopy,
                    copyBack,
                    ignore,
                    fun this ->
                        fakeFileSystemWatcher <-
                            {|  create = fun path -> this.New path
                                update = fun path -> this.Updated path |}
                )

            let engine = ExecutionEngine fs

            let model, dispatch = TestElmish.synchronous (init None) (update (fs, engine))
            fs.register (FileSystemMsg >> dispatch)
            fs.initialize()

            fakeFileSystemWatcher.create @"blahblahblah\foo\ftherlnd"
            fakeFileSystemWatcher.create @"blahblahblah\foo\xibalba.trn"

            Assert.soon
                (fun () -> fakeTempDir["foo"] |> List.contains @"ftherlnd" && fakeTempDir["foo"] |> List.contains @"xibalba.trn")
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
                (hasFile "foo" "xibalba")
                (missingFile (sprintf @"foo\xibalba.2h should be added to model as xibalba but found only %A"))

            Approve("foo", "xibalba") |> dispatch
            for i in 1..3 do
                Assert.soon
                    (fun () -> fs.exclusions |> List.contains $"foo_xibalba_{i}" && fakeGameDir[$"foo_xibalba_{i}"] |> List.containsAll ["xibalba.2h"; "ftherlnd"])
                    (fun () -> $"New game directory foo_xibalba_{i}should soon contain copies of all the 2h files and ftherlnd so we can execute, and should be marked as excluded so we don't try to copy it again")
            ]
