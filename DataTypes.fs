[<AutoOpen>]
module DataTypes
open System.Threading.Tasks

type FullPath = string
type DirectoryPath = string

type FileSystemMsg =
| NewGame of gameName:string
| NewFile of gameName: string * FullPath * nation:string * fileName: string * lastModified: System.DateTimeOffset

type Path = System.IO.Path

type FileSystem(getTempFilePath, copy: FullPath * FullPath -> unit Task, copyBack: string * FullPath * string -> unit Task, deleteByGameName: string -> unit, initialize: _ -> unit) =
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

            (copy (path, fileDest)).Wait()
            if isNewGame then
                dispatch (NewGame(gameName))

            dispatch (NewFile(gameName, fileDest, nation, fileName, System.IO.FileInfo(path).LastWriteTimeUtc |> System.DateTimeOffset))

    member this.Updated(path: FullPath) =
        if not (excludedDirectories |> List.contains (Path.GetFileName (Path.GetDirectoryName path))) then
            let fileName = Path.GetFileName path
            let gameName = (System.IO.Directory.GetParent path).Name
            let nation = Path.GetFileNameWithoutExtension path
            let fileDest, isNewGame = getTempFilePath gameName path

            (copy (path, fileDest)).Wait()

            // inform the model that a new version just came in
            dispatch (NewFile(gameName, fileDest, nation, fileName, System.IO.FileInfo(path).LastWriteTimeUtc |> System.DateTimeOffset))

    member this.CopyBackToGame(gameName, src: FullPath, destFileName: string) = copyBack(gameName, src, destFileName)
    member this.Delete(gameName: string) = deleteByGameName gameName

type ExecutionEngine(fs: FileSystem) =
    member this.Execute (gameName: string, hostCmd) =
        hostCmd gameName

module UI =
    type OrdersDetail = {
        index: int
        approved: bool
        editing: bool
        nation: string
        name: string option // defaults to nation but can be renamed to describe the kind of orders, e.g. kamikaze vs. cautious. Will show up in name of generated games.
        }
    type FileDetail =
        | Trn of nation: string
        | Orders of OrdersDetail
        | Other
    type GameFile = {
        fileName: string
        lastWriteTime: System.DateTimeOffset
        frozenPath: FullPath // not subject to change directly by Dom5.exe because it's in a temp directory, hence "frozen"
        detail: FileDetail
        }
        with
        member this.Name = (match this.detail with Orders detail -> detail.name |> Option.defaultValue (if detail.index = 1 then detail.nation else detail.nation + detail.index.ToString()) | Trn _ | Other -> this.fileName) // defaults to file path but can be renamed to describe the kind of orders, e.g. kamikaze vs. cautious. Will show up in name of generated games.
        member this.Nation = match this.detail with Orders n -> Some n.nation | Trn nation -> Some nation | Other -> None
    type Status = NotStarted | InProgress | Complete | ErrorState of msg:string
    type Permutation = {
        name: string
        status: Status
        }
    type Game = {
        name: string
        replicationCount: int
        files: GameFile list
        children: Permutation list
        }
        with
        static member create name = { name = name; replicationCount = 3; files = []; children = [] }
    type 't Validated = Valid of 't | Invalid of string option
        with
        member this.validValue = match this with Valid v -> v | Invalid _ -> shouldntHappen "You should only call validValue if you already know it's a valid value"
        member this.isValid = match this with Valid _ -> true | Invalid _ -> false
    type SettingsModel = {
        dominionsExePath: FullPath Validated
        userDataDirectoryPath: FullPath Validated
        }
        with
        static member fresh: SettingsModel = {
            dominionsExePath = Invalid None
            userDataDirectoryPath = Invalid None
            }
    type GlobalModel = {
        games: Map<string, Game>
        autoApprove: bool
        settings: SettingsModel
        }
        with
        static member fresh settings: GlobalModel = {
            games = Map.empty
            autoApprove = false
            settings = settings
            }
    type SettingsMsg =
        | UserDataDirectoryPathChanged of string
        | DomExePathChanged of string // reinitialize fileSystemWatcher!
    type GlobalMsg =
        | SaveAndCloseSettingsDialog of SettingsModel
        | FileSystemMsg of FileSystemMsg
        | SetAutoApprove of bool
        | Approve of gameName: string * ordersName: string
        | ReplicationCountChange of gameName: string * replicationCount: int
        | DeleteOrders of gameName: string * ordersName: string
        | SetName of gameName: string * ordersName: string * name: string
        | SetEditingStatus of gameName: string * ordersName: string * editing: bool
        | UpdatePermutationStatus of gameName: string * permutationName: string * status: Status
        | DeletePermutation of gameName: string * permutationName: string
