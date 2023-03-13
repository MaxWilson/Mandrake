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

type Msg =
    | FileChanged of string
    | Approve of game:string * Version2h

type Signals = {
    approved: string * Version2h list list -> unit
    }

type Model = {
    watchDirectory: string option
    queue: Map<string, Version2h list>
    }

let init _ = {
    watchDirectory = None
    queue = Map.empty
    }

let update (msg: Msg) (model: Model) : Model =
    match msg with
    | FileChanged file ->
        let gameDir = System.IO.Path.GetDirectoryName file
        let game = System.IO.Path.GetFileName gameDir
        let version = Version2h(file, DateTimeOffset.Now, None)
        { model with queue = model.queue |> Map.change game (function None -> Some [version] | Some priors -> Some (version::priors)) }
    | Approve (file, versions) -> notImpl()

let view (model: Model) signals dispatch =
    View.ScrollViewer <|
        View.StackPanel [
            View.TextBlock $"AcceptanceQueue ({model.queue.Count})"
            for KeyValue(file, versions) in model.queue do
                View.StackPanel [
                    View.TextBlock file
                    for version2h in versions do
                        match version2h with
                        | Version2h(fileName, time, descr) ->
                            View.TextBlock fileName
                            let onClick = thunk1 dispatch (Approve (file, version2h))
                            View.Button ("Approve", onClick)
                    ]
            ]

let subscribe model : _ Sub =
    [   [match model.watchDirectory with Some v -> v | None -> ()], fun dispatch -> { new System.IDisposable with
            member this.Dispose() = ()
            }
        ]
