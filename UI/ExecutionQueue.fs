module UI.ExecutionQueue
open System

type Msg =
    | Execute of game: string * Version2h list list
    | Finished of Id

type Signals = {
    finished: string * Version2h list * DateTimeOffset -> unit
    }

type Model = {
    todo: (string * Version2h list * DateTimeOffset) Queue.d
    inProgress: (Id * string * Version2h list * DateTimeOffset) Queue.d
    }

let init _ = {
    todo = Queue.empty
    inProgress = Queue.empty
    }

let update (msg: Msg) (state: Model) : Model =
    match msg with
    | Execute (game, versions) -> notImpl()
    | Finished id -> notImpl()

let view model signals dispatch =
    View.DockPanel [
        View.TextBlock "ExecutionQueue placeholder"
        ]
