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
    | Approve of string * Version2h list list

type Signals = {
    approved: string * Version2h list list -> unit
    }

type Model = {
    queue: Map<string, Version2h list list>
    }

let init _ = {
    queue = Map.empty
    }

let update (msg: Msg) (state: Model) : Model =
    match msg with
    | FileChanged file -> notImpl()
    | Approve (file, versions) -> notImpl()

let view model signals dispatch =
    View.DockPanel [
        View.TextBlock "AcceptanceQueue placeholder"
        ]
