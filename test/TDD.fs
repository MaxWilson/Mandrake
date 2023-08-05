module TDD

open System
open Expecto

type FullPath = FullPath of string
type OrdersId = OrdersId of Guid
type GameId = GameId of Guid

let notImpl() = failwith "not implemented"

// for now we're just thinking out loud, trying to model the domain on both sides
// and make sure we're not overlooking any cases. We already did this on paper,
// just re-doing it in form suitable for github checkin.

// the "hard" model is an interface to the actual Dom5 game (file system, processes). It is actor-based.
// the "soft" model is an interface to the user's needs and desires (GUI). It is Elmish-based.
// actors and Elmish both are message-based. They are similar but not identical.

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
                        system.Post(Interface.Cmd.DeleteGame(hardModel.games[id], afterwards))
                        hardModel
                finisher model'
                return model'
                }
        | ContinueWith f ->
            async {
                f hardModel
                return hardModel
                }
    let create (system: HardInterface) (initialModel: HardModel) =
        MailboxProcessor.Start (fun inbox ->
           let rec loop hardModel =
                async {
                    let! msg = inbox.Receive()
                    let! model' = (update inbox system hardModel msg)
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
                reply.Reply()
                dispatch (Mirror model')
            do! hard.PostAndAsyncReply ((fun reply -> Hard.Command (complete reply, cmdCtor ctorArg)), 1000) // 1 second timeout is more than enough for any real scenario. It should actually be instantaneous.
            }
    let toggleCmd =
        hardCmd Hard.ToggleAutoApprove
    let deleteCmd =
        hardCmd Hard.DeleteGame

[<Tests>]
let tests = testList "TDD" [
    testAsync "AutoApprove message should toggle on mirror" {
        let mockHardInterface =
            MailboxProcessor.Start(fun this ->
                async {
                    while true do
                        let! msg = this.Receive()
                        match msg with
                        | _ -> ()
                    })

        use mockHardSystem = Hard.create mockHardInterface Hard.HardModel.fresh
        let mutable model = Hard.HardModel.fresh
        let dispatch = function Mirror m -> model <- m | _ -> ()
        Expect.isFalse model.autoApprove "AutoApprove should default to false"
        do! (toggleCmd mockHardSystem dispatch true |> Async.AwaitTask)
        Expect.isTrue  model.autoApprove "AutoApprove should have been set to true by user"
        }
    testAsync "AutoApprove message should toggle even while long operations are ongoing" {
        let mockHardInterface =
            MailboxProcessor.Start(fun this ->
                async {
                    while true do
                        let! msg = this.Receive()
                        match msg with
                        | Interface.DeleteGame (_, notify) ->
                            do! Async.Sleep 1000
                            notify()
                        | _ -> ()
                    })
        use mockHardSystem = Hard.create mockHardInterface Hard.HardModel.fresh
        let mutable model = Hard.HardModel.fresh
        let dispatch = function Mirror m -> model <- m | _ -> ()
        Expect.isFalse model.autoApprove "AutoApprove should default to false"
        let deletion =
            deleteCmd mockHardSystem dispatch (System.Guid.NewGuid() |> GameId) |> Async.AwaitTask
        do! (toggleCmd mockHardSystem dispatch true |> Async.AwaitTask)
        Expect.isTrue  model.autoApprove "AutoApprove should have been set to true by user"
        do! deletion // we do expect deletion to complete eventually
        }
    ]
