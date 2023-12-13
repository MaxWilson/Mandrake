module POC

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

    type Msg =
        | NewGame of FullPath
        | NewFile of FullPath

    let init _ = Model
    let update msg model = model

module TestElmish =
    let simpleSynchronous init update =
        let mutable model = init ()
        let dispatch msg = model <- update msg model
        model, dispatch
