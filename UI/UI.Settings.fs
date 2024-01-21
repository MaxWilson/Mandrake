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
open Avalonia.FuncUI.Elmish.ElmishHook

let validWhen (predicate: 't -> bool) (value: 't) =
    if predicate value then Valid value else Invalid None

let validate (model: SettingsModel) =
    match model.dominionsExePath, model.userDataDirectoryPath with
    | Valid userDataPath, Valid exePath -> true
    | _ -> false

let saveSynchronously (fs: FileSystem) (model: SettingsModel) =
    if not (validate model) then shouldntHappen "Should validate model on the settings page, before allowing user to click the save button"
    if domExePath = Some model.dominionsExePath.validValue && userDataDirectory = Some model.userDataDirectoryPath.validValue then () // no changes
    else
        domExePath <- Some model.dominionsExePath.validValue
        userDataDirectory <- Some model.userDataDirectoryPath.validValue
        saveFileSettings()
        fs.initialize()
let init _ = SettingsModel.fresh, Cmd.Empty

let update msg (model: SettingsModel) =
    match msg with
    | DomExePathChanged path ->
        { model with dominionsExePath = validWhen System.IO.File.Exists path }, Cmd.Empty
    | UserDataDirectoryPathChanged path ->
        { model with userDataDirectoryPath = validWhen (fun userDataPath -> System.IO.Directory.Exists (Path.Combine(userDataPath, "savedgames"))) path }, Cmd.Empty

let cmdSaveAndCloseSettings fs dispatch settings _ =
    if not (validate settings) then shouldntHappen "Should validate model on the settings page, before allowing user to click the save button"
    backgroundTask { saveSynchronously fs settings; dispatch (SaveAndCloseSettingsDialog settings) }

let viewSettings cmdSaveAndClose : IView =
    Component.create("settings", fun ctx ->
        let model, dispatch = ctx.useElmish((fun _ -> SettingsModel.fresh, Cmd.Empty), update)
        StackPanel.create [
            StackPanel.children [
                TextBlock.create [
                    TextBlock.classes ["title"]
                    TextBlock.text $"Setup"
                    ]
                TextBlock.create [
                    TextBlock.classes ["subtitle"]
                    TextBlock.text $@"Path to Dominions executable, e.g. c:\usr\bin\steam\steamapps\common\Dominions6\Dominions6.exe"
                    ]
                TextBox.create [
                    TextBlock.classes [if model.dominionsExePath.isValid then "valid" else "invalid"]
                    TextBox.text (domExePath |> Option.defaultValue "")
                    TextBox.onTextChanged (fun txt -> dispatch (DomExePathChanged txt))
                    ]
                TextBlock.create [
                    TextBlock.classes ["subtitle"]
                    TextBlock.text $@"Path to user data directory, e.g. C:\Users\<userName>\AppData\Roaming\Dominions5"
                    ]
                TextBox.create [
                    TextBlock.classes [if model.userDataDirectoryPath.isValid then "valid" else "invalid"]
                    TextBox.text (userDataDirectory |> Option.defaultValue "")
                    TextBox.onTextChanged (fun txt -> dispatch (UserDataDirectoryPathChanged txt))
                    ]
                Button.create [
                    Button.content (match model.dominionsExePath, model.userDataDirectoryPath with Valid exePath, Valid userData when Some exePath = domExePath && Some userData = userDataDirectory -> "OK (no changes)" | _ -> "Save and close")
                    Button.isEnabled (match model.dominionsExePath with Valid path -> true | _ -> false)
                    Button.onClick (cmdSaveAndClose model >> ignore)
                    ]
                ]
            ]
        )