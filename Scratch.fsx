open System
open System.IO
Directory.GetFiles(@"C:\Program Files", "Dominions5.exe", EnumerationOptions(RecurseSubdirectories=true, AttributesToSkip=FileAttributes.System))
Directory.GetFiles(@"C:\usr\bin", "Dom*.exe", EnumerationOptions(RecurseSubdirectories=true, AttributesToSkip=FileAttributes.System))

#r "nuget: Thoth.Json.Net"

let counter =
    MailboxProcessor.Start(fun inbox ->
        let rec loop n =
            async { do printfn "n = %d, waiting..." n
                    let! msg = inbox.Receive()
                    return! loop(n+msg) }
        loop 0)
let counter1 =
    new MailboxProcessor<_>(fun inbox ->
        let rec loop n =
            async { do printfn "n = %d, waiting..." n
                    let! msg = inbox.Receive()
                    return! loop(n+msg) }
        loop 0)
type Msg =
    | Pause of AsyncReplyChannel<unit> * int
    | Increment of AsyncReplyChannel<int> * int
    | Query of AsyncReplyChannel<int>
let agent f =
    MailboxProcessor.Start <| fun inbox ->
        async {
            while true do
                let! (replyChannel: _ AsyncReplyChannel, msg) = inbox.Receive()
                let! resp = f msg
                replyChannel.Reply resp
            }
let waitAgent = agent (fun (ms: int) -> Async.Sleep ms)
let counter2 =
    MailboxProcessor.Start <| fun inbox ->
       let rec loop n =
            async {
                let! msg = inbox.Receive()
                match msg with
                | Pause(replyChannel, ms) ->
                    waitAgent.Post (replyChannel, ms)
                    return! loop n
                | Increment(replyChannel, i) ->
                    let n = n + i
                    if n > 100 then do! (System.InvalidOperationException() |> raise)
                    if n > 60 then do! failwith "Sorry"
                    replyChannel.Reply n
                    return! loop n
                | Query(replyChannel) ->
                    replyChannel.Reply(n)
                    return! loop(n)
            }
       loop 0
counter2.PostAndAsyncReply(fun replyChannel -> Increment(replyChannel, 2))
async {
    let! x = counter2.PostAndAsyncReply(fun replyChannel -> Increment(replyChannel, 2))
    printfn "x = %d" x
    let task = counter2.PostAndAsyncReply(fun replyChannel -> Pause (replyChannel, 2000))
    printfn "waiting..."
    let! x = counter2.PostAndAsyncReply(fun replyChannel -> Increment(replyChannel, 2))
    printfn "x = %d" x
    let! x = counter2.PostAndAsyncReply(fun replyChannel -> Increment(replyChannel, 2))
    printfn "x = %d" x
    let! x = counter2.PostAndAsyncReply(fun replyChannel -> Increment(replyChannel, 2))
    printfn "x = %d" x
    do! task
    let! x = counter2.PostAndAsyncReply(fun replyChannel -> Increment(replyChannel, 2))
    printfn "x = %d" x
    } |> Async.RunSynchronously
counter2.PostAndReply(fun replyChannel -> Increment(replyChannel, 2))
counter2.PostAndReply(fun replyChannel -> Query(replyChannel))
counter2.TryPostAndReply(timeout= 1000, buildMessage=fun replyChannel -> Increment(replyChannel, 10))
counter1.Start()
counter1.Post 2

