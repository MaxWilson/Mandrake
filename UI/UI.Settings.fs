module UI.Settings

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
open Settings
open System.IO

type Signals = {
    ok: FileSettings -> unit
    }

// This component is simple, don't bother with elmishness
let view (signals: Signals, settings: FileSettings) = Component.create("Settings", fun ctx ->
    let exePath = ctx.useState settings.exePath
    let exePathValid = ctx.useState (settings.exePath.IsSome && File.Exists settings.exePath.Value)
    let dataDir = ctx.useState settings.dataDirectory
    let dataDirValid = ctx.useState (settings.dataDirectory.IsSome && File.Exists settings.dataDirectory.Value)
    View.StackPanel [
        TextBlock.create [
            TextBlock.classes ["title"]
            TextBlock.text $"Path to Dominion5.exe"
            ]
        TextBox.create [
            TextBox.classes [if exePathValid.Current then "valid" else "invalid"]
            TextBox.text (defaultArg exePath.Current emptyString)
            TextBox.onTextChanged (fun txt -> exePath.Set (Some txt); exePathValid.Set ((String.IsNullOrWhiteSpace txt |> not) && File.Exists txt))
            ]
        TextBlock.create [
            TextBlock.classes ["title"]
            TextBlock.text $"Path to Dominion5 data directory"
            ]
        TextBox.create [
            TextBox.classes [if dataDirValid.Current then "valid" else "invalid"]
            TextBox.text (defaultArg dataDir.Current emptyString)
            TextBox.onTextChanged (fun txt -> dataDir.Set (Some txt); dataDirValid.Set ((String.IsNullOrWhiteSpace txt |> not) && Directory.Exists txt))
            ]
        if exePathValid.Current && dataDirValid.Current then
            Button.create [
                Button.content "OK"
                Button.onClick (fun _ -> signals.ok { exePath = exePath.Current; dataDirectory = dataDir.Current })
                ]
        ]
    )
