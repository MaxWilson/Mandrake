module UI.Main

open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.Types

type Model = {
    acceptance: AcceptanceQueue.Model
    execution: ExecutionQueue.Model
    results: Executed.Model
    }

let init arg =
    {
        acceptance = AcceptanceQueue.init arg
        execution = ExecutionQueue.init arg
        results = Executed.init arg
        }

type Msg =
    | Acceptance of AcceptanceQueue.Msg
    | Execution of ExecutionQueue.Msg
    | Results of Executed.Msg

let update (msg: Msg) (state: Model) : Model =
    match msg with
    | Acceptance msg -> { state with acceptance = AcceptanceQueue.update msg state.acceptance }
    | Execution msg -> { state with execution = ExecutionQueue.update msg state.execution }
    | Results msg -> { state with results = Executed.update msg state.results }

let view model dispatch =
    View.DockPanel [
        AcceptanceQueue.view model.acceptance
            ({ AcceptanceQueue.approved = ExecutionQueue.Execute >> Execution >> dispatch })
            (dispatch << Acceptance)
        ExecutionQueue.view model.acceptance
            ({ ExecutionQueue.finished = Executed.Queue >> Results >> dispatch })
            (dispatch << Execution)
        Executed.view model.acceptance (dispatch << Results)
        ]
