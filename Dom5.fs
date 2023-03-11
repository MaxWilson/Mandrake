module Dom5

open System.Threading.Tasks
let r = System.Random()

let fakeHost gameName feedback = task {
    let mutable ticks = 0
    // pretend to run C:\usr\bin\steam\steamapps\common\Dominions5\win64\dominions5.exe  -c -T -g <name>
    let fakeHost = task {
        do! Task.Delay (r.Next 10 * 1000)
    }
    let timer = task {
        while(fakeHost.IsCompleted |> not) do
            do! Task.Delay 1000
            ticks <- ticks + 1
            feedback ticks
    }
    do! fakeHost
    feedback 100
    return ticks
    }