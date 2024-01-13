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
open System.Reflection
open UI.Main
open Dom5
open Avalonia.Platform
open Avalonia.FuncUI.Elmish.ElmishHook
open System

type MainWindow() as this =
    inherit HostWindow()
    do
        let bitmap = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri($"avares://{Assembly.GetExecutingAssembly().GetName().Name}/Assets/Mandrake.ico")))
        base.Icon <- WindowIcon(bitmap)
        base.Title <- "Mandrake for Dom5"
        let fs = FileSystem(
                        getTempDirPath,
                        copyIfNewer,
                        copyBack,
                        deleteByGameName,
                        fun this ->
                            match Settings.dom5Saves with
                            | None -> () // delay initialization until we have somewhere to watch
                            | Some path ->
                                Dom5.setupNewWatcher path (debounce this.New, debounce this.Updated)
                    )

        let engine = ExecutionEngine fs
        let memory = tryLoadMemory()
        match memory with
        | None -> ()
        | Some model ->
            for permutation in model.games.Values |> Seq.collect _.children do
                fs.exclude permutation.name
        let comp = Component(fun ctx ->
            let augment f arg v = f arg v, Cmd.Empty
            let init = init memory
            let update = update (fs, engine)
            let subscription model =
                Sub.batch [
                    [[], fun dispatch ->
                        fs.register(fun msg -> Avalonia.Threading.Dispatcher.UIThread.Post(fun () -> DataTypes.UI.FileSystemMsg msg |> dispatch))
                        fs.initialize();
                        { new System.IDisposable with member this.Dispose() = ()}
                        ]
                    ]
            let model, dispatch = ctx.useElmish (init, update, fun p -> p |> Program.withSubscription subscription)
            view model dispatch
            )
        this.Content <- comp

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
