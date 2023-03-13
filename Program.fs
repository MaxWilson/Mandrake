namespace CounterApp

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Elmish
open Avalonia.FuncUI.Hosts
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.Elmish
open System.Threading.Tasks

module Counter =
    type CounterState = {
        count : int
    }

    let init _ = {
        count = 0
    }

    type Msg =
    | Increment
    | Decrement

    let update (msg: Msg) (state: CounterState) : CounterState =
        match msg with
        | Increment -> { state with count =  state.count + 1 }
        | Decrement -> { state with count =  state.count - 1 }

    let view (state: CounterState) (dispatch): IView =
        DockPanel.create [
            DockPanel.children [
                Button.create [
                    Button.onClick (fun _ -> dispatch Increment)
                    Button.content "click to increment"
                ]
                Button.create [
                    Button.onClick (fun _ -> dispatch Decrement)
                    Button.content "click to decrement"
                ]
                TextBlock.create [
                    TextBlock.dock Dock.Top
                    TextBlock.text (sprintf "the count is %i" state.count)
                ]
            ]
        ]

open UI.Main
type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "Counter Example"
        Elmish.Program.mkSimple init update view
        |> Program.withHost this
        |> Program.withSubscription (fun model ->
            [   [], (fun dispatch ->
                        let cancel = new System.Threading.CancellationTokenSource()
                        let t = task {
                            while(not cancel.IsCancellationRequested) do
                                try
                                    for f in System.IO.Directory.EnumerateFiles(@"C:\Users\wilso\AppData\Roaming\Dominions5\savedgames\Arcanus", "*.2h") do
                                        UI.AcceptanceQueue.FileChanged f |> Acceptance |> dispatch
                                with _ -> ()
                                try
                                    for f in System.IO.Directory.EnumerateFiles(@"C:\Users\wilso\AppData\Roaming\Dominions5\savedgames\YoungEarth", "*.2h") do
                                        UI.AcceptanceQueue.FileChanged f |> Acceptance |> dispatch
                                with _ -> ()

                                do! Task.Delay 4000
                        }
                        { new System.IDisposable with
                              member this.Dispose() = cancel.Cancel()
                        })
                ]
            )
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> Program.run

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add (FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

module Program =

    [<EntryPoint>]
    let main(args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
