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
type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "Mandrake for Dom5"
        let robustCopy src dest =
            // minimally robust currently (just retry once a second later) but we can improve if needed
            let rec attempt (nextDelay: int) =
                task {
                    try
                        System.IO.File.Copy(src, dest, true)
                    with
                    | err when nextDelay < 2000 ->
                        do! Task.Delay nextDelay
                        return! attempt (nextDelay * 3)
                    }
            attempt 100
            |> fun t -> t.Wait()
        let copyIfNewer (src, dest) =
            if System.IO.File.Exists src then
                let srcInfo = System.IO.FileInfo(src)
                let destInfo = System.IO.FileInfo(dest)
                if srcInfo.LastWriteTime > destInfo.LastWriteTime then
                    robustCopy src dest
        let copyBack (src, gameName) =
            let dest = Path.Combine(@"C:\Users\wilso\AppData\Roaming\Dominions5\savedGames", gameName)
            robustCopy src dest
        let fs = FileSystem(
                        Dom5.getTempDirPath,
                        copyIfNewer,
                        copyBack,
                        fun this ->
                            let watcher = Dom5.setupNewWatcher @"C:\Users\wilso\AppData\Roaming\Dominions5\savedGames" (this.New, this.Updated)
                            ()
                    )

        let engine = ExecutionEngine fs
        Elmish.Program.mkProgram init (update(fs, engine)) view
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
        |> Program.withConsoleTrace
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
