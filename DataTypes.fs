[<AutoOpen>]
module DataTypes

type FullPath = string
type DirectoryPath = string

type FileSystemMsg =
| NewGame of gameName:string
| NewFile of gameName: string * FullPath

type Path = System.IO.Path

type FileSystem(copy: string * FullPath * FullPath -> unit, initialize) as this =
    let mutable excludedDirectories = []
    let mutable listeners = []
    let dispatch msg = for dispatch in listeners do dispatch msg
    let mutable gameDests: Map<string, DirectoryPath> = Map.empty
    let getDir gameName =
        match gameDests |> Map.tryFind gameName with
            | Some dest -> dest, false
            | None ->
                let dest = Path.Combine (Path.GetTempPath(), gameName)
                let dir = System.IO.Directory.CreateDirectory dest
                if not dir.Exists then shouldntHappen "Couldn't create temp directory"
                gameDests <- Map.add gameName dest gameDests
                dest, true

    do initialize this

    member this.exclude dir = excludedDirectories <- dir :: excludedDirectories
    member this.exclusions = excludedDirectories
    member this.register listener = listeners <- listener :: listeners

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

type ExecutionEngine(fs: FileSystem) =
    class
    end

module UI =
    type OrdersDetail = {
        index: int
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
        with member this.Name = (match this.detail with Orders detail -> detail.name |> Option.defaultValue (Path.GetFileNameWithoutExtension this.frozenPath + detail.index.ToString()) | Trn | Other -> Path.GetFileName this.frozenPath) // defaults to file path but can be renamed to describe the kind of orders, e.g. kamikaze vs. cautious. Will show up in name of generated games.

    type Game = {
        name: string
        files: GameFile list
        }
    type Model = {
        games: Map<string, Game>
        }
    type Msg =
        | FileSystemMsg of FileSystemMsg
        | Approve of gameName: string * ordersName: string