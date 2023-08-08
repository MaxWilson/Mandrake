module HardSystem

type FullPath = FullPath of string
type FileReport =
    | Updated of FullPath list
    | Deleted of FullPath list
type FileSystem =
    abstract OnReceive: FileReport -> Async<unit>
    abstract Copy: {| from: FullPath list; destDirectory: FullPath |} -> Async<unit>
    abstract Delete: FullPath -> Async<unit>

type ActualFileSystem(basePath: FullPath) =
    interface FileSystem with
        member this.OnReceive _ = notImpl()
        member this.Copy _ = notImpl()
        member this.Delete _ = notImpl()
