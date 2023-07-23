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
    | Increment of AsyncReplyChannel<int> * int
    | Query of AsyncReplyChannel<int>
let counter2 =
    MailboxProcessor.Start (fun inbox ->
       let rec loop n =
            async {
                let! msg = inbox.Receive()
                match msg with
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
       loop 0)
counter2.PostAndReply(fun replyChannel -> Increment(replyChannel, 2))
counter2.PostAndReply(fun replyChannel -> Query(replyChannel))
counter2.TryPostAndReply(timeout= 1000, buildMessage=fun replyChannel -> Increment(replyChannel, 10))
counter1.Start()
counter1.Post 2

