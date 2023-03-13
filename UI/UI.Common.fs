[<AutoOpen>]
module UI.Common
open System
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types

type Version2h = Version2h of fileName2h: string * DateTimeOffset * description: string option
type Id = Guid

type View =
    static member DockPanel (children: _ list) = DockPanel.create [DockPanel.children children]
    static member StackPanel (children: _ list) = StackPanel.create [StackPanel.children children]
    static member TextBlock (txt) = TextBlock.create [TextBlock.text txt]
    static member Button (txt: string, onClick) = Button.create [Button.content txt; Button.onClick onClick]
    static member ScrollViewer (content: IView) = ScrollViewer.create [ScrollViewer.content content]