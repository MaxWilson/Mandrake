[<AutoOpen>]
module DataTypes

type FullPath = string
type DirectoryPath = string

type FileSystemMsg =
| NewGame of gameName:string
| NewFile of gameName: string * FullPath * nation:string * fileName: string

type Path = System.IO.Path

type FileSystem(getTempFilePath, copy: FullPath * FullPath -> unit, copyBack: string * FullPath * string -> unit, initialize: _ -> unit) =
    let mutable excludedDirectories = []
    let mutable listeners = []
    let dispatch msg = for dispatch in listeners do dispatch msg

    member this.initialize() = initialize this

    member this.exclude dir = excludedDirectories <- dir :: excludedDirectories
    member this.exclusions = excludedDirectories
    member this.register listener = listeners <- listener :: listeners

    member this.New(path: FullPath) =
        if not (excludedDirectories |> List.contains (Path.GetFileName (Path.GetDirectoryName path))) then
            let fileName = Path.GetFileName path
            let gameName = (System.IO.Directory.GetParent path).Name
            let nation = Path.GetFileNameWithoutExtension path
            let fileDest, isNewGame = getTempFilePath gameName path

            copy (path, fileDest)
            if isNewGame then
                dispatch (NewGame(gameName))

            dispatch (NewFile(gameName, fileDest, nation, fileName))

    member this.Updated(path: FullPath) =
        if not (excludedDirectories |> List.contains (Path.GetFileName (Path.GetDirectoryName path))) then
            let fileName = Path.GetFileName path
            let gameName = (System.IO.Directory.GetParent path).Name
            let nation = Path.GetFileNameWithoutExtension path
            let fileDest, isNewGame = getTempFilePath gameName path

            copy (path, fileDest)

            // inform the model that a new version just came in
            dispatch (NewFile(gameName, fileDest, nation, fileName))

    member this.CopyBackToGame(gameName, src: FullPath, destFileName: string) = copyBack(gameName, src, destFileName)

type ExecutionEngine(fs: FileSystem) =
    member this.Execute (gameName: string, hostCmd) =
        hostCmd gameName

module UI =
    type OrdersDetail = {
        index: int
        approved: bool
        nation: string
        name: string option // defaults to nation but can be renamed to describe the kind of orders, e.g. kamikaze vs. cautious. Will show up in name of generated games.
        }
    type FileDetail =
        | Trn of nation: string
        | Orders of OrdersDetail
        | Other
    type GameFile = {
        fileName: string
        frozenPath: FullPath // not subject to change directly by Dom5.exe because it's in a temp directory, hence "frozen"
        detail: FileDetail
        }
        with
        member this.Name = (match this.detail with Orders detail -> detail.name |> Option.defaultValue (if detail.index = 1 then detail.nation else detail.nation + detail.index.ToString()) | Trn _ | Other -> this.fileName) // defaults to file path but can be renamed to describe the kind of orders, e.g. kamikaze vs. cautious. Will show up in name of generated games.
        member this.Nation = match this.detail with Orders n -> Some n.nation | Trn nation -> Some nation | Other -> None
    type Status = NotStarted | InProgress | Complete | Error of msg:string
    type Permutation = {
        name: string
        status: Status
        }
    type Game = {
        name: string
        files: GameFile list
        children: Permutation list
        }
    type Model = {
        games: Map<string, Game>
        }
    type Msg =
        | FileSystemMsg of FileSystemMsg
        | Approve of gameName: string * ordersName: string
        | UpdatePermutationStatus of gameName: string * permutationName: string * status: Status
