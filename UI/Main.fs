module UI.Main

open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.Types

type Model = {
    acceptance: AcceptanceQueue.Model
    execution: ExecutionQueue.Model
    results: ResultsDisplay.Model
    }

let init arg =
    {
        acceptance = AcceptanceQueue.init arg
        execution = ExecutionQueue.init arg
        results = ResultsDisplay.init arg
        }

type Msg =
    | Acceptance of AcceptanceQueue.Msg
    | Execution of ExecutionQueue.Msg
    | Results of ResultsDisplay.Msg

let update (msg: Msg) (state: Model) : Model =
    match msg with
    | Acceptance msg -> { state with acceptance = AcceptanceQueue.update msg state.acceptance }
    | Execution msg -> { state with execution = ExecutionQueue.update msg state.execution }
    | Results msg -> { state with results = ResultsDisplay.update msg state.results }

let view model dispatch =
    View.DockPanel [
        AcceptanceQueue.view model.acceptance
            ({ AcceptanceQueue.approved = ExecutionQueue.Execute >> Execution >> dispatch })
            (dispatch << Acceptance)
        ExecutionQueue.view model.acceptance
            ({ ExecutionQueue.finished = ResultsDisplay.Queue >> Results >> dispatch })
            (dispatch << Execution)
        ResultsDisplay.view model.acceptance (dispatch << Results)
        ]
