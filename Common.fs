[<AutoOpen>]
module Common
open System.Threading.Tasks

module Task =
    let map f t = task { let! v = t in return f v }
    let ignore t = t |> map ignore
    let wait (t: _ System.Threading.Tasks.Task) = t.Wait()
    let runSynchronously (t: _ System.Threading.Tasks.Task) = t.Result
    let waitAll (tasks: _ Task seq) = Task.WhenAll(tasks |> Array.ofSeq |> Array.map (fun t -> t :> Task))
    let waitFirst (tasks: _ Task seq) = Task.WaitAny(tasks |> Array.ofSeq |> Array.map (fun t -> t :> Task))
