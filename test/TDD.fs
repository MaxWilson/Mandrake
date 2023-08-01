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
module Hard =
    type HardReport =
        | ReportNew2hCreated
        | ReportGameDeleted
    type HardInterface =
        abstract DeleteGame: FullPath -> Async<unit>
        abstract StartProcessing: gameName: string -> Async<unit>
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
        | Command of AsyncReplyChannel<HardModel> * HardCmd
    let update (system: HardInterface) (hardModel: HardModel) (hardMsg: HardMsg) =
        match hardMsg with
        | Report r -> notImpl()
        | Command (reply, c) ->
            match c with
            | ToggleAutoApprove b ->
                reply.Reply() // this isn't right but we need to reply _somehow_
                { hardModel with autoApprove = b } |> async.Return
            | SetAutoApproveFilter s -> { hardModel with autoApproveFilter = Some s } |> async.Return
            | ApproveOrders _ -> notImpl()
            | DeleteOrders _ -> notImpl()
            | DeleteGame id ->
                async {
                    do! system.DeleteGame(hardModel.games[id]) // hey, is it a problem that this might block the UI from toggling AutoApprove on and off?
                    // maybe we should send a finishedDeleting message or something instead.
                    return hardModel
                    }
    let create (system: HardInterface) (initialModel: HardModel) =
        MailboxProcessor.Start (fun inbox ->
           let rec loop hardModel =
                async {
                    let! msg = inbox.Receive()
                    let! model' = (update system hardModel msg)
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
            let! model' = hard.PostAndAsyncReply (fun reply -> Hard.Command (reply, cmdCtor ctorArg))
            dispatch (Mirror model')
            }
    let toggleCmd =
        hardCmd Hard.ToggleAutoApprove

[<Tests>]
let tests = testList "TDD" [
    testAsync "placeholder" {
        let hardInterface: HardInterface = notImpl()
        let softModel = SoftModel.fresh
        toggleCmd hardInterface dispatch true
        Expect.isTrue true "placeholder"
        }
    ]
