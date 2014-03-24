// ----------------------------------------------------------------------------
// F# async extensions (Observable.fs)
// (c) Tomas Petricek, 2011, Available under Apache 2.0 license.
// ----------------------------------------------------------------------------
#nowarn "40"
namespace FSharp.Control

open System
open System.Threading

open FSharpx.Observable

//[<AutoOpen>]
//module ObservableExtensions =
//
//  /// Helper that can be used for writing CPS-style code that resumes
//  /// on the same thread where the operation was started.
//  let internal synchronize f = 
//    let ctx = System.Threading.SynchronizationContext.Current 
//    f (fun g ->
//      let nctx = System.Threading.SynchronizationContext.Current 
//      if ctx <> null && ctx <> nctx then ctx.Post((fun _ -> g()), null)
//      else g() )
//
//  type Microsoft.FSharp.Control.Async with 
//
//    /// Creates an asynchronous workflow that will be resumed when the 
//    /// specified observables produces a value. The workflow will return 
//    /// the value produced by the observable.
//    static member AwaitObservable(ev1:IObservable<'T1>) =
//      synchronize (fun f ->
//        Async.FromContinuations((fun (cont,econt,ccont) -> 
//          let rec finish cont value = 
//            remover.Dispose()
//            f (fun () -> cont value)
//          and remover : IDisposable = 
//            ev1.Subscribe
//              ({ new IObserver<_> with
//                   member x.OnNext(v) = finish cont v
//                   member x.OnError(e) = finish econt e
//                   member x.OnCompleted() = 
//                      let msg = "Cancelling the workflow, because the Observable awaited using AwaitObservable has completed."
//                      finish ccont (new System.OperationCanceledException(msg)) }) 
//          () )))
//

module Observable =
    let fromAsync (a : Async<'a>) = 
        { new IObservable<'a> with
            member x.Subscribe(observer) =
                let task = async {
                    let! result = a
                    observer.OnNext result
                }
                let disposeSource = new CancellationTokenSource()
                Async.StartImmediate(task, disposeSource.Token)
                { new IDisposable with
                    member x.Dispose() = 
                        disposeSource.Cancel() } }

    let head obs : IObservable<'a> =
        let event = new Event<'a>()
        async {
            let! result = Async.AwaitObservable obs
            event.Trigger(result)
        } |> Async.StartImmediate
        event.Publish :> IObservable<'a>