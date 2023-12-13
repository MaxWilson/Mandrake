module POC

type FullPath = string

type FileSystemListener =
    { whenNew (*gameName*) : string * FullPath -> unit
      whenUpdated (*gameName*) : string * FullPath -> unit }

type FileSystem(copy: string * FullPath -> unit, initialize) as this =
    let mutable listeners = []
    do initialize this

    member this.New(path: FullPath) =
        let gameName = (System.IO.Directory.GetParent path).Name
        copy (gameName, path)

        for l in listeners do
            l.whenNew (gameName, path)

    member this.Updated(path: FullPath) =
        let gameName = (System.IO.Directory.GetParent path).Name
        copy (gameName, path)

        for l in listeners do
            l.whenNew (gameName, path)

    member this.register(listener: FileSystemListener) = listeners <- listener :: listeners

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
