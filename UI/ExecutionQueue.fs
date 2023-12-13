module UI.ExecutionQueue

open System
open DataTypes

type Msg =
    | Queue of game: ExecutableGameTurn
    | UpdateProgress of percentage: int
    | Finished of FullPath

type Signals =
    { finished: ExecutableGameTurn -> unit }

type Model =
    { todo: ExecutableGameTurn Queue.d
      inProgress:
          {| current: ExecutableGameTurn
             percentDone: int |} option }

let init _ =
    { todo = Queue.empty
      inProgress = None }

let update (msg: Msg) (model: Model) : Model =
    match msg with
    | Queue game ->
        { model with
            todo = model.todo |> Queue.append game }
    | Finished id -> notImpl ()

let view model signals dispatch =
    View.DockPanel [ View.TextBlock "ExecutionQueue placeholder" ]
