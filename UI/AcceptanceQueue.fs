module UI.AcceptanceQueue

open System
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
open System.IO
open System.Threading.Tasks
open Avalonia.Threading
open Dom5

type Msg =
    | Initialize of GameTurn list
    | NewVersionReceived of game:Id * OrdersVersion
    | Approve of game:Id * orders: Id

type Signals = {
    approved: string * Version2h list list -> unit
    showSettings: unit -> unit
    }

type Model = {
    gameTurns: Dom5.GameTurn list option
    }

let init (settings: Settings.FileSettings) : _ * _ Cmd =
    {
        gameTurns = None
        },
    [fun dispatch ->
        task {
            let! gameTurns = Dom5.setup settings
            do! Dispatcher.UIThread.InvokeAsync (fun () ->
                dispatch (Initialize gameTurns))
            } |> ignore
        ()
        ]

let update (msg: Msg) (model: Model) : Model =
    let updateGame id f model =
       { model with
            gameTurns = model.gameTurns
                |> Option.map(
                    (List.map (fun game ->
                        if game.id = id then f game else game)))
            }
    let updateOrders f (game: Dom5.GameTurn) =
        { game with orders = f game.orders }
    let updateOrder id f orders =
        orders |> List.map (fun (order: Dom5.OrdersVersion) ->
                if order.id = id then f order else order
                )
    match msg with
    | Initialize gameTurns -> { model with gameTurns = Some gameTurns }
    | NewVersionReceived (gameId, orders) ->
        model |> (updateGame gameId (updateOrders (fun allorders -> orders::allorders)))
    | Approve (gameId, ordersId) ->
        let approve order = { order with approvedForExecution = true }
        model |> (updateGame gameId (updateOrders (updateOrder ordersId approve)))

let view (model: Model) signals dispatch =
    View.ScrollViewer <|
        View.StackPanel [
            match model.gameTurns with
            | None -> View.TextBlock "Searching for saved games..."
            | Some gameTurns ->
                View.StackHorizontal [
                    TextBlock.create [
                        TextBlock.classes ["title"]
                        TextBlock.text $"AcceptanceQueue ({gameTurns.Length})"
                        ]
                    View.Button("Settings", thunk1 signals.showSettings ())
                    ]
                for game in gameTurns do
                    View.StackPanel [
                        TextBlock.create [
                            TextBlock.classes ["subtitle"]
                            TextBlock.text $"{game.name} ({game.orders.Length})"
                            ]
                        for orders in game.orders do
                            View.StackHorizontal [
                                CheckBox.create [
                                    CheckBox.content $"{defaultArg orders.nickName orders.fileName}: {orders.description}"
                                    CheckBox.classes [if orders.approvedForExecution then "approved"]
                                    CheckBox.isChecked orders.approvedForExecution
                                    CheckBox.isEnabled false
                                    ]
                                let onClick = thunk1 dispatch (Approve (game.id, orders.id))
                                Button.create [
                                    Button.content "Approve"
                                    Button.onClick onClick
                                    Button.classes [if orders.approvedForExecution then "approved"]
                                    ]
                                ]
                        ]
            ]

let subscribe (gameTurns: GameTurn list) (settings: Settings.FileSettings) : _ Sub =
    [   [match settings.dataDirectory with Some v -> v | None -> ()],
        fun dispatch ->
            let watcher = new FileSystemWatcher (System.IO.Path.GetFullPath settings.dataDirectory.Value)
            let trigger (file: FileSystemEventArgs) = backgroundTask {
                if Dom5.ignoreThisFile file.FullPath |> not then
                    let! gameId, orders = Dom5.receiveOrders gameTurns file.FullPath
                    do! Dispatcher.UIThread.InvokeAsync (fun _ ->
                        dispatch (NewVersionReceived (gameId, orders))
                        )
                }
            watcher.Changed.Add (trigger >> ignore)
            watcher.Created.Add (trigger >> ignore)
            watcher.EnableRaisingEvents <- true

            { new System.IDisposable with
                member this.Dispose() = watcher.Dispose()
                }
        ]
