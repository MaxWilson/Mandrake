module TDD

open Expecto
open ExpectoFsCheck
open System
open FsCheck

type FullPath = FullPath of string
type OrdersId = OrdersId of Guid
type GameId = GameId of Guid

// for now we're just thinking out loud, trying to model the domain on both sides
// and make sure we're not overlooking any cases. We already did this on paper,
// just re-doing it in form suitable for github checkin.

// the "hard" model is an interface to the actual Dom5 game (file system, processes). It is actor-based.
// the "soft" model is an interface to the user's needs and desires (GUI). It is Elmish-based.
// actors and Elmish both are message-based. They are similar but not identical.

[<AutoOpen>]
module Interface =
    type Cmd =
        | DeleteGame of FullPath * notify: (unit -> unit)
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

    type HardModel =
        { autoApprove: bool
          autoApproveFilter: string option
          games: Map<GameId, FullPath> }

        static member fresh =
            { autoApprove = false
              autoApproveFilter = None
              games = Map.empty }

    type HardMsg =
        | Report of HardReport
        | Command of onFinish: (HardModel -> unit) * HardCmd
        | ContinueWith of (HardModel -> unit)

    let create (system: HardInterface) (initialModel: HardModel) =
        MailboxProcessor.Start(fun inbox ->
            let rec loop hardModel =
                async {
                    let! msg = inbox.Receive()

                    try
                        match msg with
                        | Report r -> notImpl ()
                        | Command(finisher, c) ->
                            match c with
                            | ToggleAutoApprove b ->
                                let model' = { hardModel with autoApprove = b }
                                finisher model'
                                return! loop model'
                            | SetAutoApproveFilter s ->
                                let model' =
                                    { hardModel with
                                        autoApproveFilter = Some s }

                                finisher model'
                                return! loop model'
                            | ApproveOrders _ -> notImpl ()
                            | DeleteOrders _ -> notImpl ()
                            | DeleteGame id ->
                                let afterwards () = inbox.Post(ContinueWith finisher)

                                match hardModel.games |> Map.tryFind id with
                                | Some gameId -> system.Post(Interface.Cmd.DeleteGame(gameId, afterwards))
                                | None -> afterwards ()

                                return! loop hardModel
                        | ContinueWith f ->
                            f hardModel
                            return! loop hardModel
                    with _ ->
                        return! loop hardModel
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
    type SoftModel =
        { mirror: HardModel; currentPage: Page }

    let hardCmd cmdCtor (hard: MailboxProcessor<Hard.HardMsg>) dispatch ctorArg =
        task {
            let complete (reply: _ AsyncReplyChannel) model' =
                reply.Reply()
                dispatch (Mirror model')

            do! hard.PostAndAsyncReply((fun reply -> Hard.Command(complete reply, cmdCtor ctorArg)), 1000) // 1 second timeout is more than enough for any real scenario. It should actually be instantaneous.
        }

    let toggleCmd = hardCmd Hard.ToggleAutoApprove
    let deleteCmd = hardCmd Hard.DeleteGame

type User =
    { Id: int
      FirstName: string
      LastName: string }

type SmallPrime = SmallPrime of int

type SmallPrimeGen() =
    static let arbSmallPrime =
        Arb.generate<int>
        |> Gen.filter (fun x -> x < 10)
        |> Gen.map (fun i -> SmallPrime i)
        |> Arb.fromGen

    static member SmallPrime() = arbSmallPrime
//Gen.elements [2;3;5;7] |> Gen.map SmallPrime |> Arb.fromGen
[<Tests>]
let tests =
    testLabel "Mandrake"
    <| testList
        "TDD"
        [ testAsync "AutoApprove message should toggle on mirror" {
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

              let dispatch =
                  function
                  | Mirror m -> model <- m
                  | _ -> ()

              Expect.isFalse model.autoApprove "AutoApprove should default to false"
              do! (toggleCmd mockHardSystem dispatch true |> Async.AwaitTask)
              Expect.isTrue model.autoApprove "AutoApprove should have been set to true by user"
          }
          testAsync "AutoApprove message should toggle even while long operations are ongoing" {
              let mockHardInterface =
                  MailboxProcessor.Start(fun this ->
                      async {
                          while true do
                              let! msg = this.Receive()

                              match msg with
                              | Interface.DeleteGame(_, notify) ->
                                  do! Async.Sleep 1000
                                  notify ()
                              | _ -> ()
                      })

              use mockHardSystem = Hard.create mockHardInterface Hard.HardModel.fresh
              let mutable model = Hard.HardModel.fresh

              let dispatch =
                  function
                  | Mirror m -> model <- m
                  | _ -> ()

              Expect.isFalse model.autoApprove "AutoApprove should default to false"

              let deletion =
                  deleteCmd mockHardSystem dispatch (System.Guid.NewGuid() |> GameId)
                  |> Async.AwaitTask

              do! (toggleCmd mockHardSystem dispatch true |> Async.AwaitTask)
              Expect.isTrue model.autoApprove "AutoApprove should have been set to true by user"
              do! deletion // we do expect deletion to complete eventually
          }
          testProperty "Addition is commutative" <| fun a b -> a + b = b + a
          testPropertyWithConfig
              { FsCheckConfig.defaultConfig with
                  arbitrary = [ typeof<SmallPrimeGen> ] }
              "All smallprimes are small"
          <| fun (SmallPrime(p: int)) -> (p < 10)
          testSequenced
          <| testList
              "New game detected gets added to list"
              [ test "M sets of N orders all get detected and copied to unique folders" { () }
                test "All approved orders generate permutations in the queue" { () }
                test "All permutations eventually execute and go to results queue" { () }
                test
                    "Advancing base game does auto-cleanup of non-queued copied orders, and cleans up
                generated games if settings.autoCleanupGeneratedGames is set" {
                    ()
                }
                test "Otherwise, wait for Discard command on generated game and then delete" { () } ] ]
