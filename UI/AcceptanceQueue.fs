module UI.AcceptanceQueue

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
    queue: Map<string, Version2h list list>
    }

let init _ = {
    queue = Map.empty
    }

let update (msg: Msg) (model: Model) : Model =
    match msg with
    | FileChanged file ->
        { model with queue = model.queue.Add (file, []) }
    | Approve (file, versions) -> notImpl()

let view model signals dispatch =
    View.DockPanel [
        View.TextBlock "AcceptanceQueue"
        for KeyValue(file, versions) in model.queue do
            View.StackPanel [
                View.TextBlock file
                for version2h in versions |> List.concat do
                    match version2h with
                    | Version2h(fileName, time, descr) ->
                        View.TextBlock fileName
                        let onClick = thunk1 dispatch (Approve (file, version2h))
                        View.Button ("Approve", onClick)
                ]
        ]
