[<AutoOpen>]
module Common
open System.Threading.Tasks

let flip f x y = f y x
let random = System.Random()
let rand x = 1 + random.Next x
let thunk v _ = v
let thunk1 f arg _ = f arg
let thunk2 f arg1 arg2 _ = f arg1 arg2
let thunk3 f arg1 arg2 arg3 _ = f arg1 arg2 arg3
let tuple2 x y = x,y
let matchfail v = sprintf "No match found for %A. This is a bug." v |> invalidOp
let ignoreM (_, monad) = (), monad
exception BugException of msg: string
/// Placeholder while we're doing type-focused development, before implementation
let notImpl _ = failwith "Not implemented yet. Email Max if you want this feature."
let shouldntHappen arg =
    $"This shouldn't ever happen. If it does there's a bug. Details: {arg}" |> BugException |> raise
let emptyString = System.String.Empty
let toString x = x.ToString()
let betweenInclusive a b n = min a b <= n && n <= max a b
/// invoke f without requiring parens
let inv f = f()
let chooseRandom (options: _ seq) =
    options |> Seq.skip (random.Next (Seq.length options)) |> Seq.head

// iterate a mutable value
let iter (data: byref<_>) f =
    data <- f data
    data
/// iter and ignore the result
let iteri (data: byref<_>) f = data <- f data

let shuffleCopy =
    let swap (a: _[]) x y =
            let tmp = a.[x]
            a.[x] <- a.[y]
            a.[y] <- tmp
    fun a ->
        let a = Array.map id a // make a copy
        a |> Array.iteri (fun i _ -> swap a i (random.Next(i, Array.length a)))
        a // return the copy

module Tuple2 =
    let create x y = x,y
    let mapfst f (x,y) = (f x, y)
    let mapsnd f (x,y) = (x, f y)

[<AutoOpen>]
module Ctor =
    type Constructor<'args, 'Type> = {
        create: 'args -> 'Type
        extract: 'Type -> 'args option
        name: string option
        }
        with
        static member (=>) (lhs: Constructor<_, 't1>, rhs: Constructor<'t1, _>) =
            {   create = rhs.create << lhs.create
                extract = rhs.extract >> Option.bind lhs.extract
                name =  match lhs.name, rhs.name with
                        | Some lhs, Some rhs -> Some ($"{rhs} ({lhs})")
                        | Some lhs, _ -> Some lhs
                        | _, Some rhs -> Some rhs
                        | _ -> None
                }

    let ctor(create, extract) = { create = create; extract = extract; name = None }
    let namedCtor(name, create, extract) = { create = create; extract = extract; name = Some name }

// generic place for overloaded operations like add. Can be extended (see below).
type Ops =
    static member add(key, value, data: Map<_,_>) = data |> Map.add key value
    static member addTo (data:Map<_,_>) = fun key value -> Ops.add(key, value, data)

module String =
    let oxfordJoin = function
        | _::_::_::_rest as lst -> // length 3 or greater
            match List.rev lst with
            | last::rest ->
                sprintf "%s, and %s" (System.String.Join(", ", List.rev rest)) last
            | _ -> shouldntHappen()
        | [a;b] -> sprintf "%s and %s" a b
        | [a] -> a
        | [] -> emptyString
    let join delimiter strings = System.String.Join((delimiter: string), (strings: string seq))
    let equalsIgnoreCase lhs rhs = System.String.Equals(lhs, rhs, System.StringComparison.InvariantCultureIgnoreCase)
    let firstWord input =
        match Option.ofObj input with
        | Some(v:string) -> v.Trim().Split(' ') |> Seq.head
        | None -> input
    let trim (s:string) = s.Trim()

    // turn camel casing back into words with spaces, for display to user
    let uncamel (str: string) =
        let caps = ['A'..'Z'] |> Set.ofSeq
        let lower = ['a'..'z'] |> Set.ofSeq
        let mutable spaceNeededBefore = []
        let mutable inWord = true
        for i in 1..str.Length-1 do
            match str[i] with
            | ' ' -> inWord <- false
            // When multiple caps are in a row, no spaces should be used, except before the last one if it's followed by a lowercase.
            // E.g. MySSNNumber => My SSN Number, but MySSN => My SSN not My SS N
            | letter when caps.Contains letter && inWord && ((caps.Contains str[i-1] |> not) || i+1 < str.Length && lower.Contains str[i+1])->
                spaceNeededBefore <- i::spaceNeededBefore
            | letter when System.Char.IsLetterOrDigit letter -> inWord <- true
            | _ -> ()
        let rec recur workingCopy spacesNeeded =
            match spacesNeeded with
            | [] -> workingCopy
            | index::rest ->
                recur $"{workingCopy[0..(index-1)]} {workingCopy[index..]}" rest
        recur str spaceNeededBefore

module List =
    let join delimiter (lst: _ list) =
        match lst with
        | [] | [_] -> lst
        | head::tail ->
            head :: (tail |> List.collect (fun x -> [delimiter; x]))
    let ofOption = function
        | None -> []
        | Some v -> [v]
    let every f =
        List.exists (f >> not) >> not
    let rec tryMapFold f state lst =
        match lst with
        | [] -> Ok state
        | h::t -> match f state h with
                    | Ok state' -> tryMapFold f state' t
                    | e -> e
    let rec maxBy' f (lst: _ list) = lst |> Seq.map f |> Seq.max
    let rec minBy' f (lst: _ list) = lst |> Seq.map f |> Seq.min
    let containsAll values lst : bool = values |> every (flip List.contains lst)

module Array =
    let rec maxBy' f (lst: _ array) = lst |> Seq.map f |> Seq.max
    let rec minBy' f (lst: _ array) = lst |> Seq.map f |> Seq.min

module Map =
    let keys (m:Map<_,_>) = m |> Seq.map(fun (KeyValue(k,_)) -> k)
    let values (m:Map<_,_>) = m |> Seq.map(fun (KeyValue(_,v)) -> v)
    let addForce key f (m: Map<_,_>) =
        match m |> Map.tryFind key with
        | Some v ->
            let v' = f v
            if v = v' then m
            else m |> Map.add key v'
        | None ->
            m |> Map.add key (f Map.empty)
    let findForce key (m: Map<_,_>) =
        m |> Map.tryFind key |> Option.defaultValue Map.empty
    let (|Lookup|_|) key map =
        map |> Map.tryFind key
    let ofListBy f lst = lst |> List.groupBy f |> List.map (fun (key, values) -> key, values) |> Map.ofList
    let addMulti key value map =
        map |> Map.change key (Option.orElse (Some []) >> Option.map (List.append [ value ]))
module Queue =
    type 't d = 't list
    let append item queue = queue@[item]
    let empty = []
    let read (queue: _ d) = queue

type Ops with
    static member add(item, data: _ Queue.d) = Queue.append item data
    static member addTo (data:_ Queue.d) = fun item -> Ops.add(item, data)

module Task =
    let map f t = task { let! v = t in return f v }
    let ignore t = t |> map ignore
    let wait (t: _ System.Threading.Tasks.Task) = t.Wait()
    let runSynchronously (t: _ System.Threading.Tasks.Task) = t.Result
    let waitAll (tasks: _ Task seq) = Task.WhenAll(tasks |> Array.ofSeq |> Array.map (fun t -> t :> Task))
    let waitFirst (tasks: _ Task seq) = Task.WaitAny(tasks |> Array.ofSeq |> Array.map (fun t -> t :> Task))

let inline trace v =
#if DEBUG
    printfn "Trace: %A" v
#endif
    v

