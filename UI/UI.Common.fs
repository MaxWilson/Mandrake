[<AutoOpen>]
module UICommon
open System
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types

type Version2h = Version2h of fileName2h: string * DateTimeOffset * description: string option
type Id = Guid

type View =
    static member DockPanel (children: _ list) = DockPanel.create [DockPanel.children children]
    static member StackPanel (children: _ list) = StackPanel.create [StackPanel.children children]
    static member StackPanel' properties (children: _ list) = StackPanel.create ((StackPanel.children children)::properties)
    static member StackHorizontal (children: _ list) = StackPanel.create ([StackPanel.children children; StackPanel.orientation Avalonia.Layout.Orientation.Horizontal])
    static member TextBlock (txt) = TextBlock.create [TextBlock.text txt]
    static member Button (txt: string, onClick) = Button.create [Button.content txt; Button.onClick onClick]
    static member ScrollViewer (content: IView) = ScrollViewer.create [ScrollViewer.content content]

type Button with
    static member onClick logic =
        Button.onClick(logic, SubPatchOptions.Always) // We always want it to use the logic we specify, instead of keeping whatever logic it had before. See https://github.com/fsprojects/Avalonia.FuncUI/issues/379

type TextBlock with
    static member onDoubleTapped logic =
        TextBlock.onDoubleTapped(logic, SubPatchOptions.Always) // We always want it to use the logic we specify, instead of keeping whatever logic it had before. See https://github.com/fsprojects/Avalonia.FuncUI/issues/379
type TextBox with
    static member onDoubleTapped logic =
        TextBox.onDoubleTapped(logic, SubPatchOptions.Always) // We always want it to use the logic we specify, instead of keeping whatever logic it had before. See https://github.com/fsprojects/Avalonia.FuncUI/issues/379
    static member onTextChanged logic =
        TextBox.onTextChanged(logic, SubPatchOptions.Always) // We always want it to use the logic we specify, instead of keeping whatever logic it had before. See https://github.com/fsprojects/Avalonia.FuncUI/issues/379
