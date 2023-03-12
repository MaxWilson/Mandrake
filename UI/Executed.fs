module UI.Executed
open System

type Msg =
    | Queue of string * Version2h list * DateTimeOffset
    | Delete of Id

// no signals because it doesn't send to anywhere external, just deletes rows when finished

type Model = {
    results: (Id * string * Version2h list * DateTimeOffset) list
    }

let init _ = {
    results = []
    }

let update (msg: Msg) (state: Model) : Model =
    match msg with
    | Queue (game, versions, time) -> notImpl()
    | Delete id -> notImpl()

let view model dispatch =
    View.DockPanel [
        View.TextBlock "ResultsDisplay placeholder"
        ]
