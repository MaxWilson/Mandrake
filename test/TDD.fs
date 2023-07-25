module TDD

open System
open Expecto

type FullPath = FullPath of string
type OrdersId = OrdersId of Guid
type GameId = GameId of Guid
// for now we're just thinking out loud, trying to model the domain on both sides
// and make sure we're not overlooking any cases. We already did this on paper,
// just re-doing it in form suitable for github checkin.

// the "hard" model is an interface to the actual Dom5 game (file system, processes). It is actor-based.
// the "soft" model is an interface to the user's needs and desires (GUI). It is Elmish-based.
// actors and Elmish both are message-based. They are similar but not identical.

type HardReport =
    | ReportNew2hCreated
    | ReportGameDeleted
type HardInterface =
    abstract DeleteGame: FullPath -> Async<unit>
    abstract StartProcessing: gameName: string -> Async<unit>
// SoftMsg is used to manipulate the softModel and therefore the GUI output.
// Generally we expect the user to use the View to inform HardControl of changes
// that the user has requested, and then... generate the appropriate SoftMsg?
// Not sure.
type SoftMsg =
    | ToggleAutoApprove of bool
    | SetAutoApproveFilter of string
    | ApproveOrders of OrdersId
    | DeleteOrders of OrdersId
    | DeleteGame of GameId
// softModel needs to be sufficient to render to the screen
type SoftModel = {
    autoApprove: bool
    autoApproveFilter: string option
    }

[<Tests>]
let tests = testList "TDD" [
    testCase "placeholder" <| fun _ ->
        Expect.isTrue true "placeholder"
    test "placeholder2" {
        Expect.equal (1+1) 2 "1+1 should equal 2"
        }
    ]
