// ----------------------------------------------------------------------------
// F# async extensions (Observable.fs)
// (c) Tomas Petricek, 2011, Available under Apache 2.0 license.
// ----------------------------------------------------------------------------
#nowarn "40"
namespace FSharp.Control

open System
open System.Threading

open FSharp.Control
open FSharp.Control.Observable

module Observable =
    let interval (timespan : TimeSpan) obs =
        let timer = new System.Timers.Timer(timespan.TotalMilliseconds)
        timer.Elapsed |> Observable.scan (fun state _ -> state + 1) -1

    let timestamp obs = 
        Observable.map (fun x -> x, DateTime.Now) obs