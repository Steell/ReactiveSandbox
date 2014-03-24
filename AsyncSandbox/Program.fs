// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

module Main

open System
open System.Threading

let run_test func =
    ignore <| func()
    printfn "Press any key to quit."
    Console.ReadKey true |> ignore
    printfn ""

let rec a (m : System.Numerics.BigInteger) (n : System.Numerics.BigInteger) =
    if   m = 0I then n + 1I
    elif n = 0I then a (m - 1I) 1I
    else             a (m - 1I) <| a m (n - 1I)

let child_task name =
    ignore <| try Some(a 3I 3I) with :? System.StackOverflowException -> None
    Thread.Sleep(2000)
    printfn "Task \"%s\" finished" name

let basic() =
    printfn "Starting child"
    child_task "basic"
    printfn "Child started"

let all_sync() =
    async {
        printfn "Starting child"
        child_task "all_sync"
        printfn "Child started"
    } |> Async.RunSynchronously |> ignore

let cancel() =
    async {
        printfn "Starting child"
        let child = async { child_task "cancel" }
        let! _ = Async.StartChild child
        printfn "Child started"
    } |> Async.StartImmediate |> ignore

let interleave() =
    async {
        printfn "Starting child"
        let child = async { child_task "interleave" }
        let! result = Async.StartChild child
        printfn "Child started"
        do! result
    } |> Async.RunSynchronously |> ignore
    

let async_tests() =
    [
        basic
        all_sync
        cancel
        interleave
    ] |> List.iter run_test

//async_tests()

//let task = Async.StartAsTask <| async { do! Async.Sleep 1000 }

let task = new Tasks.Task<unit>(fun () -> ignore <| Thread.Sleep 1000)
async {
    let! _ = Async.AwaitTask task
    Console.WriteLine "done"
} |> Async.StartImmediate
task.Start()
ignore <| Console.ReadKey true