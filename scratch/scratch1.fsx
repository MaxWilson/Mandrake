#I ".."
#load "Common.fs"

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

async {
    for _ in 1..10 do
        do! Async.Sleep 10
        x <- x + 1
}
|> Async.StartImmediate

Assert.soon (fun () -> x = 10) "x should quickly reach 10"

type FullPath = string

type FileSystemListener =
    { whenNew: FullPath -> unit
      whenUpdated: FullPath -> unit }

type FileSystem(initialize) as this =
    let mutable listeners = []
    do initialize this

    member this.New(path: FullPath) =
        for l in listeners do
            l.whenNew path

    member this.Updated(path: FullPath) =
        for l in listeners do
            l.whenUpdated path

    member this.register(listener: FileSystemListener) = ()

type ExecutionEngine(fs: FileSystem) =
    class
    end

module UI =
    type Model = Model
    type Msg = Msg
    let init _ = Model
    let update msg model = model

module TestElmish =
    let simpleSynchronous init update =
        let mutable model = init ()
        let dispatch msg = model <- update msg model
        model, dispatch

module Tests =
    let test1 () =
        let mutable fakeFileSystemWatcher = {| create = ignore; update = ignore |}
        let mutable fakeTempDir = Set.empty

        let fs =
            FileSystem(fun this ->
                fakeFileSystemWatcher <-
                    {| create = (fun path -> this.New path)
                       update = (fun path -> this.Updated path) |})

        let engine = ExecutionEngine fs

        let model, dispatch = TestElmish.simpleSynchronous UI.init UI.update

        fs.New @"foo\ftherlnd"
        fs.New @"foo\xibalba.trn"

        Assert.soon
            (fun () -> fakeTempDir.Contains @"foo\ftherlnd" && fakeTempDir.Contains @"foo\xibalba.trn")
            "Game files should be copied to temp directory"

        Assert.eq 2 (3 - 1)
