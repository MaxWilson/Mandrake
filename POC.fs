module POC

type FullPath = string
type DirectoryPath = string

type FileSystemMsg =
| NewGame of gameName:string
| NewFile of gameName: string * FullPath

type Path = System.IO.Path

type FileSystem(copy: string * FullPath * FullPath -> unit, initialize) as this =
    let mutable listeners = []
    let dispatch msg = for dispatch in listeners do dispatch msg
    let mutable gameDests: Map<string, DirectoryPath> = Map.empty
    do initialize this
    let getDir gameName =
        match gameDests |> Map.tryFind gameName with
            | Some dest -> dest, false
            | None ->
                let dest = Path.Combine (Path.GetTempPath(), gameName)
                let dir = System.IO.Directory.CreateDirectory dest
                if not dir.Exists then shouldntHappen "Couldn't create temp directory"
                gameDests <- Map.add gameName dest gameDests
                dest, true

    member this.New(path: FullPath) =
        let gameName = (System.IO.Directory.GetParent path).Name
        let destDir, isNewGame = getDir gameName

        copy (gameName, path, destDir)
        if isNewGame then
            dispatch (NewGame(gameName))

        let path = Path.Combine (destDir, Path.GetFileName path)
        dispatch (NewFile(gameName, path))

    member this.Updated(path: FullPath) =
        let gameName = (System.IO.Directory.GetParent path).Name
        copy (gameName, path, getDir gameName |> fst)
        // I can't think of any changes needed to model state at this time. Later on maybe we might want to unapprove changed files?

    member this.register(listener) = listeners <- listener :: listeners

type ExecutionEngine(fs: FileSystem) =
    class
    end

module UI =
    type OrdersDetail = {
        approved: bool
        name: string option // defaults to file path but can be renamed to describe the kind of orders, e.g. kamikaze vs. cautious. Will show up in name of generated games.
        }
    type FileDetail =
        | Trn
        | Orders of OrdersDetail
        | Other
    type GameFile = {
        frozenPath: FullPath // not subject to change directly by Dom5.exe because it's in a temp directory, hence "frozen"
        detail: FileDetail
        }
        with member this.Name = (match this.detail with Orders detail -> detail.name | Trn | Other -> None) |> Option.defaultValue (Path.GetFileNameWithoutExtension this.frozenPath) // defaults to file path but can be renamed to describe the kind of orders, e.g. kamikaze vs. cautious. Will show up in name of generated games.

    type Game = {
        name: string
        exclude: bool
        files: GameFile list
        }
    type Model = {
        games: Map<string, Game>
        }

    let init _ = { games = Map.empty }
    let update msg model =
        match msg with
        | NewGame(game) ->
            { model with games = Map.change game (Option.orElse (Some { name = game; exclude = false; files = [] })) model.games }
        | NewFile(game, path) ->
            let game = model.games |> Map.tryFind game |> Option.defaultValue { name = game; exclude = false; files = [] }
            let detail =
                match Path.GetExtension path with
                | ".trn" -> Trn
                | ".ord" -> Orders { name = None; approved = false }
                | _ -> Other
            let file = { frozenPath = path; detail = detail }
            { model with games = Map.add game.name { game with files = file :: game.files } model.games }

module TestElmish =
    let simpleSynchronous init update =
        let model = ref (init ())
        let dispatch msg = model.Value <- update msg model.Value
        model, dispatch
