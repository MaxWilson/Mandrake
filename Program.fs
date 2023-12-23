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

open UI.Main
open Dom5
type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "Mandrake for Dom5"
        let fs = FileSystem(
                        getTempDirPath,
                        copyIfNewer,
                        copyBack,
                        fun this ->
                            Dom5.setupNewWatcher @"C:\Users\wilso\AppData\Roaming\Dominions5\savedGames" (debounce this.New, debounce this.Updated)
                    )

        let engine = ExecutionEngine fs
        let memory = tryLoadMemory()
        match memory with
        | None -> ()
        | Some model ->
            for permutation in model.games.Values |> Seq.collect _.children do
                fs.exclude permutation.name
        Elmish.Program.mkProgram (init memory) (update(fs, engine)) view
        |> Program.withHost this
        |> Program.withSubscription (fun model ->
            Sub.batch [
                [[], fun dispatch ->
                    fs.register(fun msg -> Avalonia.Threading.Dispatcher.UIThread.Post(fun () -> DataTypes.UI.FileSystemMsg msg |> dispatch))
                    fs.initialize();
                    { new System.IDisposable with member this.Dispose() = ()}
                    ]
                ]
            )
#if DEBUG
        // |> Program.withConsoleTrace // Console trace becomes very hard to read actually
#endif
        |> Program.run

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add (FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark
        this.Styles.Load "avares://Mandrake/UI/Styles.xaml"

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
