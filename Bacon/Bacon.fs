namespace Bacon

#nowarn "40"

open System
open System.Diagnostics

open FSharp.Control

type KeySource() =
    
    let mutable key = 0

    let mutable queue = []

    member this.GetKey() =
        match queue with
        | h::t ->
            queue <- t
            h
        | [] ->
            let tmp = key
            key <- key + 1
            tmp

    member this.FreeKey k = queue <- k::queue

    member this.Reset() =
        key <- 0
        queue <- []

type Stream<'a> =
    inherit IObservable<'a>
    inherit IDisposable

// Represents a stream of IObserver events. 
type ObservableSource<'a>() =

    let protect function1 =
        let mutable ok = false 
        try 
            function1()
            ok <- true 
        finally
            Debug.Assert(ok, "IObserver method threw an exception.")

    let keys = new KeySource()

    // Use a Map, not a Dictionary, because callers might unsubscribe in the OnNext 
    // method, so thread-safe snapshots of subscribers to iterate over are needed. 
    let mutable subscriptions = Map.empty : Map<int, IObserver<'a>>

    let processSubs f = subscriptions |> Map.iter (fun _ value -> protect (fun () -> f value))
    let next(obs) = processSubs <| fun value -> value.OnNext obs
    let completed() = processSubs <| fun value -> value.OnCompleted()
    let error(err) = processSubs <| fun value -> value.OnError err

    let thisLock = new obj()
    
    let mutable finished = false 
    let mutable disposed = false

    member private this.Disposed with get() = disposed

    // The source ought to call these methods in serialized fashion (from 
    // any thread, but serialized and non-reentrant). 
    member this.Next(obs) =
        Debug.Assert(not finished, "IObserver is already finished")
        next obs

    member this.Completed() =
        Debug.Assert(not finished, "IObserver is already finished")
        finished <- true
        completed()

    member this.Error(err) =
        Debug.Assert(not finished, "IObserver is already finished")
        finished <- true
        error err

    // The IObservable object returned is thread-safe; you can subscribe  
    // and unsubscribe (Dispose) concurrently. 
    member this.AsObservable = this :> IObservable<'a>

    abstract DisposeInternal : unit -> unit
    default this.DisposeInternal() = 
        disposed <- true
        subscriptions <- Map.empty

    abstract OnSubscribed : IObserver<'a> -> unit
    default this.OnSubscribed _ = ()

    interface Stream<'a> with
        member this.Subscribe obs =
            let key1 =
                lock thisLock (fun () ->
                    let key1 = keys.GetKey()
                    subscriptions <- subscriptions.Add(key1, obs)
                    this.OnSubscribed obs
                    key1)
            { new IDisposable with  
                member this.Dispose() = 
                    lock thisLock (fun () -> 
                        subscriptions <- subscriptions.Remove(key1)
                        keys.FreeKey key1) }
        member this.Dispose() = this.DisposeInternal()


type Property<'a>(?initial: 'a) =
    
    let mutable currentValue = initial

    let propertySource = new ObservableSource<'a>()

    static member Constant x = failwith "Not Implemented"
    
    member this.Changes : Stream<'a> = (propertySource :> Stream<'a>)
    
    member this.SampledBy (observable: #IObservable<_>) : IObservable<'a> =
        observable |> Observable.choose (fun _ -> currentValue)

    member this.SampledByAndCombinedWith (f: 'a -> 'b -> 'c) (observable: #IObservable<'b>) : IObservable<'c> = 
        observable 
            |> Observable.choose (
                fun b -> 
                    match currentValue with
                    | Some(a) -> Some(f a b)
                    | None -> None)

    member this.Sample (interval: TimeSpan) : Stream<'a> = 
        let token = new System.Threading.CancellationTokenSource()
        let obs = 
            { new ObservableSource<_>() with
                member this.DisposeInternal() =
                    token.Cancel()
                    this.Completed()
                    base.DisposeInternal() }
        let loop = async {
            while true do
                do! Async.Sleep(interval.TotalMilliseconds |> int)
                if currentValue.IsSome then obs.Next currentValue.Value
        }
        Async.StartImmediate(loop, token.Token)
        obs :> Stream<'a>

    interface Stream<'a> with
        member this.Subscribe obs = 
            if currentValue.IsSome then obs.OnNext currentValue.Value
            (propertySource :> Stream<'a>).Subscribe obs

        member this.Dispose() = (propertySource :> Stream<'a>).Dispose()
        
module Bacon =

    let fromObservable (observable : #IObservable<'a>) : Stream<'a> =
        let newObs : ObservableSource<_> option ref = ref None
        let subscription =
            observable.Subscribe
                { new IObserver<'a> with
                    member this.OnNext x = (!newObs).Value.Next x
                    member this.OnError x = (!newObs).Value.Error x
                    member this.OnCompleted() = (!newObs).Value.Completed() }
        let result = 
            { new ObservableSource<_>() with
                member this.DisposeInternal() =
                    subscription.Dispose()
                    base.DisposeInternal() }
        newObs := Some(result)
        result :> Stream<'a>

    let onValue (handler: 'a -> unit) (observable: #IObservable<'a>) : IDisposable = 
        failwith "Not Implemented"

    let onEnd (handler: unit -> unit) (observable: #IObservable<'a>) : IDisposable =  
        failwith "Not Implemented"

    let map (f: 'a -> 'b) (observable: #IObservable<'a>) : Stream<'b> =  
        Observable.map f observable |> fromObservable

    let mapEnd (f: unit -> 'a) (observable: #IObservable<'a>) : Stream<'a> =  
        let mappedObs : ObservableSource<_> option ref = ref None
        let mapSubscription =
            observable.Subscribe
                { new IObserver<'a> with
                    member this.OnNext x = (!mappedObs).Value.Next x
                    member this.OnError x = (!mappedObs).Value.Error x
                    member this.OnCompleted() =
                        let obs = (!mappedObs).Value
                        obs.Next <| f()
                        obs.Completed() }
        let result = 
            { new ObservableSource<_>() with
                member this.DisposeInternal() =
                    mapSubscription.Dispose()
                    base.DisposeInternal() }
        mappedObs := Some(result)
        result :> Stream<'a>

    let filter (predicate: 'a -> bool) (observable: #IObservable<'a>) : Stream<'a> =  
        Observable.filter predicate observable |> fromObservable

    let filterByProperty (property: Property<bool>) (observable: #IObservable<'a>) : Stream<'a> =  
        Observable.combineLatest property observable
            |> Observable.choose (fun (test, value) -> if test then Some(value) else None)
            |> fromObservable

    let takeWhile (predicate: 'a -> bool) (observable: #IObservable<'a>) : Stream<'a> =  
        Observable.takeWhile predicate observable |> fromObservable

    let takeWhileByProperty (property: Property<bool>) (observable: #IObservable<'a>) : Stream<'a> =  
        Observable.combineLatest property observable
            |> Observable.takeWhile (fun (test, _) -> test)
            |> Observable.map (fun (_, value) -> value)
            |> fromObservable

    let take (n: int) (observable: #IObservable<'a>) : Stream<'a> =  
        let counter = ref n
        let newObs : ObservableSource<_> option ref = ref None
        let rec subscription : IDisposable =
            observable.Subscribe
                { new IObserver<'a> with
                    member this.OnNext x = 
                        (!newObs).Value.Next x
                        counter := !counter - 1
                        if !counter = 0 then this.OnCompleted()
                    member this.OnError x = (!newObs).Value.Error x
                    member this.OnCompleted() = 
                        (!newObs).Value.Completed()
                        subscription.Dispose() }
        let result = 
            { new ObservableSource<_>() with
                member this.DisposeInternal() =
                    subscription.Dispose()
                    base.DisposeInternal() }
        newObs := Some(result)
        result :> Stream<'a>

    let takeUntil (stopper: IObservable<_>) (observable: #IObservable<'a>) : Stream<'a> =  
        let takeStream = fromObservable observable
        
        let stopperSub =
            stopper.Subscribe
                { new IObserver<'a> with
                    member this.OnNext x = takeStream.Dispose()
                    member this.OnError err = takeStream.Error err
                    member this.OnCompleted() = takeStream.OnCompleted() }

    let skip : int -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let skipDuplicates : ('a -> 'a -> bool) -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let delay : TimeSpan -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let throttle : TimeSpan -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let debounce : TimeSpan -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let debounceImmediate : TimeSpan -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let doAction : ('a -> unit) -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let flatMap : ('a -> #IObservable<'a>) -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let flatMapLatest : ('a -> #IObservable<'a>) -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let flatMapFirst : ('a -> #IObservable<'a>) -> #IObservable<'a> -> IObservable<'a> =  
        failwith "Not Implemented"

    let scan : 'b -> ('b -> 'a -> 'b) -> #IObservable<'a> -> Property<'b> =  
        failwith "Not Implemented"

    let reduce : 'b -> ('b -> 'a -> 'b) -> #IObservable<'a> -> Property<'b> =  
        failwith "Not Implemented"

    let diff : 'a -> ('a -> 'a -> 'b) -> #IObservable<'a> -> Property<'b> =  
        failwith "Not Implemented"

    let zip : ('a -> 'b -> 'c) -> #IObservable<'a> -> #IObservable<'b> -> IObservable<'c> =  
        failwith "Not Implemented"

    let slidingWindow : int -> int -> #IObservable<'a> -> IObservable<'a list> =  
        failwith "Not Implemented"

    let combine : ('a -> 'b -> 'c) -> #IObservable<'a> -> #IObservable<'b> -> IObservable<'c> =  
        failwith "Not Implemented"

    let withStateMachine : 'b -> ('b -> Event<'a> -> 'b * Event<'c> list) -> IObservable<'c> =  
        failwith "Not Implemented"

    let split : ('a -> Choice<'b, 'c>) -> #IObservable<'a> -> IObservable<'b> * IObservable<'c> =  
        failwith "Not Implemented"

    let awaiting : #IObservable<_> -> #IObservable<'a> -> IObservable<bool> =  
        failwith "Not Implemented"

    let subscribe : (Event<'a> -> unit) -> #IObservable<'a> -> IDisposable =  
        failwith "Not Implemented"
    
    let not (observable: #IObservable<bool>) : Stream<bool> = map not observable

    let once x = failwith "Not Implemented"
    let never () = failwith "Not Implemented"

    let fromBinder (binder: IObserver<'a> -> IDisposable) : IObservable<'a> =
        failwith "Not Implemented"

    let fromCallback (f: ('a -> unit) -> unit) : IObservable<'a> =
        failwith "Not Implemented"

    let fromPoll (interval: TimeSpan) (f: TimeSpan -> 'a) : IObservable<'a> =
        failwith "Not Implemented"

    let fromSeq (seq: 'a seq) : IObservable<'a> =
        failwith "Not Implemented"

    let later (delay: TimeSpan) (value: 'a) : IObservable<'a> =
        failwith "Not Implemented"

    let interval (interval: TimeSpan) (x: 'a) : IObservable<'a> =
        failwith "Not Implemented"

    let sequentially (interval: TimeSpan) (seq: 'a seq) : IObservable<'a> =
        failwith "Not Implemented"

    let repeatedly (interval: TimeSpan) (seq: 'a seq) : IObservable<'a> =
        failwith "Not Implemented"

    let mapProperty (property: Property<'b>) (observable: #IObservable<_>) : Stream<'b> =
        property.SampledBy observable |> fromObservable

    let concat (obs1: #IObservable<'a>) (obs2: #IObservable<'a>) : IObservable<'a> =
        failwith "Not Implemented"

    let merge (obs1: #IObservable<'a>) (obs2: #IObservable<'a>) : IObservable<'a> =
        failwith "Not Implemented"

    let startWith (value: 'a) (observable: #IObservable<'a>) : IObservable<'a> =
        concat (once value) observable

    let skipWhile (predicate: 'a -> bool) (observable: #IObservable<'a>) : IObservable<'a> =
        failwith "Not Implemented"

    let skipWhileByProperty (property: Property<'a>) (observable: #IObservable<'a>) : IObservable<'a> =
        failwith "Not Implemented"

    let skipUntil (waitFor: IObservable<_>) (observable: #IObservable<'a>) : IObservable<'a> =
        failwith "Not Implemented"

    let bufferWithTime (delay: TimeSpan) (observable: #IObservable<'a>) : IObservable<'a list> =
        failwith "Not Implemented"

    let buffer (defer: Action -> unit) (observable: #IObservable<'a>) : IObservable<'a list> =
        failwith "Not Implemented"

    let bufferWithCount (count: int) (observable: #IObservable<'a>) : IObservable<'a list> =
        failwith "Not Implemented"

    let bufferWithTimeOrCount (delay: TimeSpan) (count: int) (observable: #IObservable<'a>) : IObservable<'a list> =
        failwith "Not Implemented"

    let toProperty (observable: #IObservable<'a>) : Property<'a> =
        failwith "Not Implemented"

    let toPropertyWithInitialValue (initialValue: 'a) (observable: #IObservable<'a>) : Property<'a> =
        failwith "Not Implemented"

    