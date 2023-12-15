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
        base.Title <- "Counter Example"
        let copyIfNewer (src, dest) =
            let srcInfo = System.IO.FileInfo(src)
            let destInfo = System.IO.FileInfo(dest)
            if srcInfo.LastWriteTime > destInfo.LastWriteTime then
                System.IO.File.Copy(src, dest, true)
        let fs = FileSystem(
                        Dom5.getTempDirPath,
                        copyIfNewer,
                        fun this ->
                            let watcher = Dom5.setupNewWatcher @"C:\Users\wilso\AppData\Roaming\Dominions5\savedGames" (this.New, this.Updated)
                            ()
                    )

        let engine = ExecutionEngine fs
        Elmish.Program.mkSimple init (update(fs, engine)) view
        |> Program.withHost this
        |> Program.withSubscription (fun model ->
            Sub.batch [
                // match model.acceptance.gameTurns, model.fileSettings with
                // | Some turns, { exePath = Some exePath; dataDirectory = Some dataDirectory } ->
                //     // we want to resubscribe if either the settings change or a new game gets created
                //     let prefix = turns |> List.map (fun gt -> gt.name) |> List.append [dataDirectory; exePath] |> String.concat ";"
                //     Sub.map prefix Acceptance (UI.AcceptanceQueue.subscribe turns model.fileSettings)
                // | _ -> ()
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
