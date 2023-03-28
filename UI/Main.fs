module UI.Main

open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.Types
open Elmish

type Msg =
    | ShowSettings
    | ReceiveFileSettings of Settings.FileSettings
    | Acceptance of AcceptanceQueue.Msg
    | Execution of ExecutionQueue.Msg
    | Results of Executed.Msg

type Model = {
    showSettings: bool
    fileSettings: Settings.FileSettings
    acceptance: AcceptanceQueue.Model
    execution: ExecutionQueue.Model
    results: Executed.Model
    }

let init arg =
    let settings = Settings.getFileSettings()
    let accept, cmd = AcceptanceQueue.init settings
    {
        showSettings = false
        fileSettings = settings
        acceptance = accept
        execution = ExecutionQueue.init arg
        results = Executed.init arg
        }, (Cmd.map Acceptance cmd)

let update (msg: Msg) (state: Model) =
    match msg with
    | ShowSettings -> { state with showSettings = true }, Cmd.none
    | ReceiveFileSettings fileSettings -> { state with fileSettings = fileSettings; showSettings = false }, Cmd.none
    | Acceptance msg -> { state with acceptance = AcceptanceQueue.update msg state.acceptance }, Cmd.none
    | Execution msg -> { state with execution = ExecutionQueue.update msg state.execution }, Cmd.none
    | Results msg -> { state with results = Executed.update msg state.results }, Cmd.none

let view (model: Model) dispatch : IView =
    match model.fileSettings with
    | { exePath = Some exePath; dataDirectory = Some dataDirectory } when not model.showSettings ->
        View.DockPanel [
            AcceptanceQueue.view model.acceptance
                ({  AcceptanceQueue.approved = ExecutionQueue.Queue >> Execution >> dispatch
                    AcceptanceQueue.showSettings = thunk1 dispatch ShowSettings
                    })
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
