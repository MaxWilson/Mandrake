open System

type FullPath = FullPath of string
type OrdersId = OrdersId of Guid
type GameId = GameId of Guid

let notImpl() = failwith "not implemented"

[<AutoOpen>]
module Interface =
    type Cmd =
        | DeleteGame of FullPath * notify : (unit -> unit)
        | StartProcessing of gameName: string

[<AutoOpen>]
module Hard =
    type HardReport =
        | ReportNew2hCreated
        | ReportGameDeleted
    type HardInterface = Interface.Cmd MailboxProcessor
    // HardCmd is used to represent user commands, which change the HardModel (as opposed
    // to the SoftModel, which is updated via SoftCmd).
    // Generally we expect the user to use the View to inform HardControl of changes
    // that the user has requested, and then update the SoftModel's mirror via a message.
    type HardCmd =
        | ToggleAutoApprove of bool
        | SetAutoApproveFilter of string
        | ApproveOrders of OrdersId
        | DeleteOrders of OrdersId
        | DeleteGame of GameId
    type HardModel = {
        autoApprove: bool
        autoApproveFilter: string option
        games: Map<GameId, FullPath>
        }
        with static member fresh = { autoApprove = false; autoApproveFilter = None; games = Map.empty }
    type HardMsg =
        | Report of HardReport
        | Command of onFinish:(HardModel -> unit) * HardCmd
        | ContinueWith of (HardModel -> unit)
    let update (inbox: _ MailboxProcessor) (system: HardInterface) (hardModel: HardModel) (hardMsg: HardMsg) =
        match hardMsg with
        | Report r -> notImpl()
        | Command (finisher, c) ->
            async {
                let model' =
                    match c with
                    | ToggleAutoApprove b ->
                        { hardModel with autoApprove = b }
                    | SetAutoApproveFilter s -> { hardModel with autoApproveFilter = Some s }
                    | ApproveOrders _ -> notImpl()
                    | DeleteOrders _ -> notImpl()
                    | DeleteGame id ->
                        let afterwards() =
                            inbox.Post(ContinueWith finisher)
                        printfn "Deleting game %A" id
                        let ignore = system.PostAndTryAsyncReply(fun _ -> Interface.Cmd.DeleteGame(hardModel.games[id], afterwards))
                        printfn "Deleted game %A" id
                        hardModel
                finisher model'
                return model'
                }
        | ContinueWith f ->
            async {
                f hardModel
                return hardModel
                }
    let create update (system: HardInterface) (initialModel: HardModel) =
        MailboxProcessor.Start (fun inbox ->
           let rec loop hardModel =
                async {
                    let! msg = inbox.Receive()
                    printfn "Received %A" msg
                    let! model' = (update inbox system hardModel msg)
                    printfn "Processed %A" msg
                    return! loop model'
                }
           loop initialModel)

[<AutoOpen>]
module Soft =
    // Note that SoftMsg is emitted only to update the SoftModel, not the HardModel,
    // so most user commands are HardCmds instead which emit SoftMsg at the end of the process.
    type Page =
        | Home
        | OrdersView
        | QueueView
        | ResultsView
    type SoftMsg =
        | Mirror of HardModel
        | NavigateTo of Page
    // softModel needs to be sufficient to render to the screen, can have extra data like focus
    type SoftModel = {
        mirror: HardModel
        currentPage: Page
        }
    let hardCmd cmdCtor (hard:MailboxProcessor<Hard.HardMsg>) dispatch ctorArg =
        task {
            let complete (reply: _ AsyncReplyChannel) model' =
                printfn "Completing"
                reply.Reply()
                printfn "Mirroring"
                dispatch (Mirror model')
            do! hard.PostAndAsyncReply ((fun reply -> Hard.Command (complete reply, cmdCtor ctorArg)), 1000) // 1 second timeout is more than enough for any real scenario. It should actually be instantaneous.
            }
    let toggleCmd =
        hardCmd Hard.ToggleAutoApprove
    let deleteCmd =
        hardCmd Hard.DeleteGame


let hardLogic =
    MailboxProcessor.Start(fun this ->
        async {
            while true do
                let! msg = this.Receive()
                printfn "hardLogic received %A" msg
                match msg with
                | Interface.DeleteGame _ ->
                    printfn "hardLogic Sleeping"
                    do! Async.Sleep 10
                    printfn "hardLogic Just woke up"
                | _ -> ()
            })

let mockHardSystem = Hard.create update hardLogic Hard.HardModel.fresh
let mutable model = Hard.HardModel.fresh
let dispatch = function Mirror m -> model <- m | _ -> ()
false = model.autoApprove
(deleteCmd mockHardSystem dispatch (System.Guid.NewGuid() |> GameId)).Result

// concurrency seems fine for mb1 and mb2, so why not with mockHardSystem?
let mb1 = MailboxProcessor.Start <| fun self -> async {
    let mutable me = 0
    while true do
        let! msg, after = self.Receive()
        do! Async.Sleep (msg: int)
        me <- me + msg
        after me
    }
let mb2 = MailboxProcessor.Start <| fun self -> async {
    while true do
        let! msg = self.Receive()
        printfn "Received %A" <| msg
        mb1.Post(msg, fun resp ->
            printfn "Processed %A and got %A" msg resp)
    }
mb2.Post(1000)

let mockHardSystem =
    MailboxProcessor<HardMsg>.Start (fun inbox ->
           let rec loop hardModel =
                async {
                    let! msg = inbox.Receive()
                    printfn "Received %A" msg
                    printfn "update2 start"
                    //let ignore = hardLogic.PostAndTryAsyncReply(fun _ -> Interface.Cmd.DeleteGame(FullPath "fakepath", ignore))
                    printfn "update2 end"
                    printfn "Processed %A" msg
                    match msg with
                    | Hard.Command(onFinish, cmd) ->
                        onFinish(hardModel)
                    | _ -> ()
                    return! loop hardModel
                }
           loop Hard.HardModel.fresh)
let dispatch = function Mirror m -> () | _ -> ()

(deleteCmd mockHardSystem dispatch (System.Guid.NewGuid() |> GameId)).Result
mockHardSystem.Post(HardMsg.Command((fun _ -> printfn "\n\n\n\nOK, done!"), HardCmd.DeleteGame(System.Guid.NewGuid() |> GameId)))
