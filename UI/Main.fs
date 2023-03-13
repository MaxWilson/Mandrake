module UI.Main

open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.Types

type Model = {
    fileSettings: Settings.FileSettings
    acceptance: AcceptanceQueue.Model
    execution: ExecutionQueue.Model
    results: Executed.Model
    }

let init arg =
    {
        fileSettings = Settings.getFileSettings()
        acceptance = AcceptanceQueue.init arg
        execution = ExecutionQueue.init arg
        results = Executed.init arg
        }

type Msg =
    | ReceiveFileSettings of Settings.FileSettings
    | Acceptance of AcceptanceQueue.Msg
    | Execution of ExecutionQueue.Msg
    | Results of Executed.Msg

let update (msg: Msg) (state: Model) : Model =
    match msg with
    | ReceiveFileSettings fileSettings -> { state with fileSettings = fileSettings }
    | Acceptance msg -> { state with acceptance = AcceptanceQueue.update msg state.acceptance }
    | Execution msg -> { state with execution = ExecutionQueue.update msg state.execution }
    | Results msg -> { state with results = Executed.update msg state.results }

let view (model: Model) dispatch : IView =
    match model.fileSettings with
    | { exePath = Some exePath; dataDirectory = Some dataDirectory } ->
        View.DockPanel [
            AcceptanceQueue.view model.acceptance
                ({ AcceptanceQueue.approved = ExecutionQueue.Execute >> Execution >> dispatch })
                (dispatch << Acceptance)
            ExecutionQueue.view model.acceptance
                ({ ExecutionQueue.finished = Executed.Queue >> Results >> dispatch })
                (dispatch << Execution)
            Executed.view model.acceptance (dispatch << Results)
            ]
    | _ ->
        let onSend settings =
            Settings.saveFileSettings settings
            dispatch (ReceiveFileSettings settings)
        UI.Settings.view({ ok = onSend }, model.fileSettings)
