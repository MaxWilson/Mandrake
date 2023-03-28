module DataTypes

open System
type FileName = string // E.g. "mid_marignon.2h", not a full path.
type FullPath = string

type OrdersVersion = {
    id: Guid
    fileName: FileName
    time: DateTime // file system is kept in datetimes, no offset
    nickName: string option
    description: string option
    approvedForExecution: bool
    copiedFileLocation: FullPath
    }

// game turn output from Dominions5.exe. Has ftherlnd, .trn file, etc., and is just waiting around for .2h files.
type GameTurn = {
    id: Guid
    name: string
    originalDirectory: FullPath
    originalFiles: FileName list
    copiedDirectory: FullPath option
    turnTime: DateTime // file system is kept in datetimes, no offset
    orders: OrdersVersion list
    }

// Has specific .2h files, ready for dominions5.exe execution. Includes Mandrake metadata about which .2h versions created this.
type ExecutableGameTurn = {
    inputs: GameTurn * OrdersVersion list
    executionDirectory: FullPath
    descriptionParagraphs: string list
    }
