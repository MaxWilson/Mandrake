[<AutoOpen>]
module UICommon
open System
open Avalonia.Controls
open Avalonia.FuncUI.DSL

type Version2h = Version2h of fileName2h: string * DateTimeOffset * description: string option
type Id = Guid

type View =
    static member DockPanel (children: _ list) = DockPanel.create [DockPanel.children children]
    static member TextBlock (txt) = TextBlock.create [TextBlock.text txt]
