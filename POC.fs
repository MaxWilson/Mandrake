module POC

type FullPath = string

type FileSystemMsg =
| NewGame of gameName:string * FullPath
| NewFile of gameName: string * FullPath

type FileSystem(copy: string * FullPath -> unit, initialize) as this =
    let mutable listeners = []
    do initialize this

    member this.New(path: FullPath) =
        let gameName = (System.IO.Directory.GetParent path).Name
        copy (gameName, path)

        for dispatch in listeners do
            dispatch (NewGame(gameName, path))

    member this.Updated(path: FullPath) =
        let gameName = (System.IO.Directory.GetParent path).Name
        copy (gameName, path)

        for dispatch in listeners do
            dispatch (NewGame(gameName, path))

    member this.register(listener) = listeners <- listener :: listeners

type ExecutionEngine(fs: FileSystem) =
    class
    end

module UI =
    type OrdersDetail = {
        approved: bool
        }
    type FileDetail =
        | Trn
        | Orders of OrdersDetail
        | Other
    type GameFile = {
        name: string option // defaults to file path but can be renamed to describe the kind of orders, e.g. kamikaze vs. cautious. Will show up in name of generated games.
        path: FullPath
        detail: FileDetail
        }
        with member this.Name = defaultArg this.name (System.IO.Path.GetFileName this.path)

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
        | NewGame(game, path) ->
            { model with games = Map.add game { name = game; exclude = false; files = [] } model.games }
        | NewFile(game, path) ->
            let game = model.games.[game]
            let detail =
                match System.IO.Path.GetExtension path with
                | ".trn" -> Trn
                | ".ord" -> Orders { approved = false }
                | _ -> Other
            let file = { name = None; path = path; detail = detail }
            { model with games = Map.add game.name { game with files = file :: game.files } model.games }

module TestElmish =
    let simpleSynchronous init update =
        let mutable model = init ()
        let dispatch msg = model <- update msg model
        model, dispatch
