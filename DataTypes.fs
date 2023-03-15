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

type GameTurn = {
    id: Guid
    name: string // file system is kept in datetimes, no offset
    originalDirectory: FullPath
    originalFiles: FileName list
    copiedDirectory: FullPath option
    turnTime: DateTime
    orders: OrdersVersion list
    }
