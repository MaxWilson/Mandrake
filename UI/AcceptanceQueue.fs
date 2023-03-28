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
open DataTypes
open Dom5

type Msg =
    | Initialize of GameTurn list
    | NewVersionReceived of game:Id * OrdersVersion
    | Approve of game:Id * orders: Id

type Signals = {
    approved: ExecutableGameTurn -> unit
    showSettings: unit -> unit
    }

type Model = {
    gameTurns: GameTurn list option
    }

let init (settings: Settings.FileSettings) : _ * _ Cmd =
    let gameTurns = Settings.getGameTurns()
    {
        gameTurns = gameTurns
        },
    [fun dispatch ->
        backgroundTask {
            let! gameTurns = Dom5.setup gameTurns settings
            do! Dispatcher.UIThread.InvokeAsync (fun () ->
                dispatch (Initialize gameTurns))
            } |> ignore
        ]

let update (msg: Msg) (model: Model) : Model =
    let updateGame id f model =
       { model with
            gameTurns = model.gameTurns
                |> Option.map(
                    (List.map (fun game ->
                        if game.id = id then f game else game)))
            }
    let updateOrders f (game: GameTurn) =
        { game with orders = f game.orders }
    let updateOrder id f orders =
        orders |> List.map (fun (order: OrdersVersion) ->
                if order.id = id then f order else order
                )
    let model =
        match msg with
        | Initialize gameTurns -> { model with gameTurns = Some gameTurns }
        | NewVersionReceived (gameId, orders) ->
            model |> (updateGame gameId (updateOrders (fun allorders -> orders::allorders)))
        | Approve (gameId, ordersId) ->
            let approve order = { order with approvedForExecution = true }
            model |> (updateGame gameId (updateOrders (updateOrder ordersId approve)))
    match model.gameTurns, msg with
    | Some turns, (NewVersionReceived _ | Approve _) ->
        backgroundTask { turns |> Settings.saveGameTurns } |> ignore
    | _ -> ()
    model

let permutations (groups: 't list list) =
    let rec recur (lst: 't list list): 't list list =
        match lst with
        | [] -> []
        | [final] ->
            // if there are N options in the final list item, then we want to return N lists.
            // [[a;b;c]] becomes [[a];[b];[c]]
            final |> List.map (fun item -> [item])
        | head::tail ->
            let (result: 't list list) = [
                for item in head do
                    for (rest: 't list) in recur (tail: 't list list) do
                        item::rest
                ]
            result
    recur groups

let approve dispatch (signals: Signals) (game: GameTurn) (orders: OrdersVersion) =
    // update the acceptance queue UI
    dispatch (Approve (game.id, orders.id))
    // now, send items to execution queue, grouped with suitable approved opponent .2h files
    let partnerGroups =
        game.orders |> List.filter (fun o -> o.fileName <> orders.fileName && o.approvedForExecution)
        |> List.groupBy (fun o -> o.fileName)
        |> List.map snd
    for (opponents: OrdersVersion list) in partnerGroups |> permutations do
        let participants = orders::opponents
        signals.approved(game, opponents)

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
                                let onClick _ =
                                    approve dispatch signals game orders
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
