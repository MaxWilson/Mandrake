module UI.Main

open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.Types
open Elmish
open DataTypes.UI
open Avalonia.Layout

let init _ = { games = Map.empty }

let justUnlocked (gameName: string, game: Game) =
    let trns = game.files |> List.filter (function { detail = Trn } -> true | _ -> false) |> List.groupBy _.Nation
    let orders = game.files |> List.filter (function { detail = Orders _ } -> true | _ -> false) |> List.groupBy _.Nation
    ()

let update (fs: FileSystem, ex:ExecutionEngine) msg model =
    match msg with
    | FileSystemMsg(NewGame(game)) ->
        { model with games = Map.change game (Option.orElse (Some { name = game; files = [] })) model.games }
    | FileSystemMsg(NewFile(game, path)) ->
        let game = model.games |> Map.tryFind game |> Option.defaultValue { name = game; files = [] }
        let detail =
            match Path.GetExtension path with
            | ".trn" -> Trn
            | ".2h" ->
                let priorIx = game.files |> List.collect (function { detail = Orders { index = ix } } -> [ ix ] | _ -> []) |> List.append [0] |> List.max
                Orders { name = None; approved = false; index = priorIx + 1 }
            | _ -> Other
        let file = { frozenPath = path; detail = detail }
        { model with games = Map.add game.name { game with files = file :: game.files } model.games }
    | Approve(gameName, ordersName) ->
        let game = {
            model.games[gameName]
            with
                files =
                    model.games[gameName].files
                    |> List.map (function
                        | { detail = Orders det } as f when f.Name = ordersName -> { f with detail = Orders { det with approved = true } }
                        | otherwise -> otherwise
                    )
            }
        { model with games = Map.add gameName game model.games }


let view (model: Model) dispatch : IView =
    View.DockPanel [
        TextBlock.create [
            TextBlock.classes ["title"]
            TextBlock.text $"Games"
            ]
        for game in model.games.Values do
            StackPanel.create [
                StackPanel.orientation Orientation.Vertical
                StackPanel.children [
                    TextBlock.create [
                        TextBlock.classes ["subtitle"]
                        TextBlock.text (game.name)
                        // TextBox.onTextChanged (fun txt -> exePath.Set (Some txt); exePathValid.Set ((String.IsNullOrWhiteSpace txt |> not) && File.Exists txt))
                        ]
                    for file in game.files do
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text (file.Name)
                                    ]
                                ]
                            ]
                        ]
                ]
        ]
    // match model.fileSettings with
    // | { exePath = Some exePath
    //     dataDirectory = Some dataDirectory } when not model.showSettings ->
    //     View.DockPanel
    //         [ AcceptanceQueue.view
    //               model.acceptance
    //               ({ AcceptanceQueue.approved = ExecutionQueue.Queue >> Execution >> dispatch
    //                  AcceptanceQueue.showSettings = thunk1 dispatch ShowSettings })
    //               (dispatch << Acceptance)
    //           ExecutionQueue.view
    //               model.acceptance
    //               ({ ExecutionQueue.finished = notImpl Executed.Queue >> Results >> dispatch })
    //               (dispatch << Execution)
    //           Executed.view model.acceptance (dispatch << Results) ]
    // | _ ->
    //     let onSend settings =
    //         Settings.saveFileSettings settings
    //         dispatch (ReceiveFileSettings settings)

    //     UI.Settings.view ({ ok = onSend }, model.fileSettings)
