namespace Bacon

open System

type Event<'a> = Next of (unit -> 'a) | End

type SinkResult = More | NoMore
type Sink<'a> = Event<'a> -> SinkResult

type Subscription<'a>(sink : Sink<'a>) =
    member this.sink = sink

type UpdateBarrier() = 
    
    let mutable rootEvent = None
    let mutable waiters = []
    let mutable afters = []
    let afterTransaction f = if rootEvent.IsSome then afters <- List.concat [afters; [f]] else f()
    let independent waiter = not <| List.exists (fun other -> waiter.obs.dependsOn other.obs) waiters
    let findIndependent () =
        while not <| independent waiters.[0] do
            waiters <- List.concat [waiters.Tail; [waiters.Head]]
        let tmp = waiters.Head
        waiters <- waiters.Tail
        tmp
    let flush () =
        while not waiters.IsEmpty do
            findIndependent().f()

    static member Instance = new UpdateBarrier()

    member this.whenDoneWith (obs: Observable<_>) (f: (unit -> unit)) : unit =
        if rootEvent.IsSome
        then waiters <- List.concat [waiters; [{obs=obs; f=f}]]
        else f()

    member this.inTransaction (event: Event<_>) (f: ('a -> 'b)) (arg: 'a) : 'b option = 
        let mutable result = None
        if rootEvent.IsSome
        then Some(f arg)
        else
            rootEvent <- Some(event)
            try
                result <- Some(f arg)
                flush()
            finally
                rootEvent <- None
                while not afters.IsEmpty do
                    afters.Head()
                    afters <- afters.Tail
            result

    member this.currentEventId () : obj option = 
        if rootEvent.IsSome then Some(rootEvent.Value.id) else None

    member this.wrappedSubscribe (obs: Observable<'a>) (sink: Sink<'a>) () : unit -> unit = 
        let unsubd = ref false
        let doUnsub = ref (fun () -> ())
        let unsub () =
            unsubd := true
            (!doUnsub)()
        if not !unsubd
        then
            doUnsub := obs.subscribeInternal (
                fun event -> 
                    afterTransaction (
                        fun () -> 
                            if not subsubd
                            then
                                if sink event = NoMore then unsub()))
        unsub

and Observable<'a>() =
    
    member this.OnValue (handler: ('a -> unit)) : IDisposable =
        let subscriber : Event<'a> -> unit = function
            | Next(value) -> handler <| value()
            | _ -> ()
        this.Subscribe subscriber

    member this.OnEnd (handler: (unit -> unit)) : IDisposable =
        let subscriber : Event<'a> -> unit = function
            | End -> handler()
            | _ -> ()
        this.Subscribe subscriber

    abstract Map : ('a -> 'b) -> Observable<'b>
    abstract MapEnd : (unit -> 'a) -> Observable<'a>
    abstract Filter : ('a -> bool) -> Observable<'a>
    abstract FilterByProperty : Property<'a> -> Observable<'a>
    abstract TakeWhile : ('a -> bool) -> Observable<'a>
    abstract TakeWhileByProperty : Property<bool> -> Observable<'a>
    abstract Take : int -> Observable<'a>
    abstract TakeUntil : Observable<_> -> Observable<'a>
    abstract Skip : int -> Observable<'a>
    abstract SkipDuplicates : ('a -> 'a -> bool) -> EventStream<'a>
    abstract Delay : TimeSpan -> Observable<'a>
    abstract Throttle : TimeSpan -> Observable<'a>
    abstract Debounce : TimeSpan -> Observable<'a>
    abstract DebounceImmediate : TimeSpan -> Observable<'a>
    abstract DoAction : ('a -> unit) -> Observable<'a>
    abstract FlatMap : ('a -> Observable<'a>) -> EventStream<'a>
    abstract FlatMapLatest : ('a -> Observable<'a>) -> EventStream<'a>
    abstract FlatMapFirst : ('a -> Observable<'a>) -> EventStream<'a>
    abstract Scan : 'b -> ('b -> 'a -> 'b) -> Property<'b>
    abstract Reduce : 'b -> ('b -> 'a -> 'b) -> Property<'b>
    abstract Diff : 'a -> ('a -> 'a -> 'b) -> Property<'b>
    abstract Zip : Observable<'b> -> ('a -> 'b -> 'c) -> Observable<'c>
    abstract SlidingWindow : int -> int -> Observable<'a list>
    abstract Combine : Observable<'b> -> ('a -> 'b -> 'c) -> Observable<'c>
    abstract WithStateMachine : 'b -> ('b -> Event<'a> -> 'b * Event<'c> list) -> Observable<'c>
    abstract Split : ('a -> Choice<'b, 'c>) -> Observable<'b> * Observable<'c>
    abstract Awaiting : Observable<_> -> Observable<bool>

    abstract Subscribe : (Event<'a> -> unit) -> IDisposable
    //abstract WithHandler 


and Property<'a>(initial: 'a) =
    
    static member Constant x = failwith "Not Implemented"
    
    member this.Changes : EventStream<'a> = failwith "Not Implemented"
    member this.ToEventStream () : EventStream<'a> = failwith "Not Implemented"
    
    member this.SampledByStream (stream: EventStream<_>) : EventStream<'a> = failwith "Not Implemented"
    member this.SampledByProperty (property: Property<_>) : Property<'a> = failwith "Not Implemented"
    member this.SampledByObservable (observable: Observable<'b>) (f: 'a -> 'b -> 'c) : EventStream<'c> = failwith "Not Implemented"

    member this.Subscribe (handler: Event<'a> -> unit) : IDisposable = failwith "Not Implemented"
    member this.Sample (interval: TimeSpan) : EventStream<'a> = failwith "Not Implemented"
    member this.SkipDuplicates (equalityTest: ('a -> 'a -> bool)) : EventStream<'a> = failwith "Not Implemented"


and EventStream<'a>(subscribe: Event<'a> -> unit) =

    static member Once x = failwith "Not Implemented"
    static member Never () = failwith "Not Implemented"

    static member FromBinder (binder: Sink<'a> -> IDisposable) : EventStream<'a> =
        failwith "Not Implemented"

    static member FromObservable (observable: IObservable<'a>) : EventStream<'a> = 
        failwith "Not Implemented"

    static member FromCallback (f: ('a -> unit) -> unit) : EventStream<'a> =
        failwith "Not Implemented"

    static member FromPoll (interval: TimeSpan) (f: TimeSpan -> 'a) : EventStream<'a> =
        failwith "Not Implemented"

    static member Later (delay: int) (value: 'a) : EventStream<'a> =
        failwith "Not Implemented"

    static member FromSeq (seq: 'a seq) : EventStream<'a> =
        failwith "Not Implemented"

    static member Interval (interval: TimeSpan) (x: 'a) : EventStream<'a> =
        failwith "Not Implemented"

    static member Sequentially (interval: TimeSpan) (seq: 'a seq) : EventStream<'a> =
        failwith "Not Implemented"

    static member Repeatedly (interval: TimeSpan) (seq: 'a seq) : EventStream<'a> =
        failwith "Not Implemented"

    member this.MapProperty (property: Property<'b>) : EventStream<'b> =
        property.SampledByStream this

    member this.Concat (otherStream: Observable<'a>) : EventStream<'a> =
        failwith "Not Implemented"

    member this.Merge (otherStream: Observable<'a>) : EventStream<'a> =
        failwith "Not Implemented"

    member this.StartWith (value: 'a) : EventStream<'a> =
        EventStream<'a>.Once value |> this.Concat

    member this.SkipWhile (predicate: 'a -> bool) : EventStream<'a> =
        failwith "Not Implemented"

    member this.SkipWhileByProperty (property: Property<'a>) : EventStream<'a> =
        failwith "Not Implemented"

    member this.SkipUntil (stream: EventStream<_>) : EventStream<'a> =
        failwith "Not Implemented"

    member this.BufferWithTime (delay: TimeSpan) : EventStream<'a list> =
        failwith "Not Implemented"

    member this.Buffer (defer: Action -> unit) : EventStream<'a list> =
        failwith "Not Implemented"

    member this.BufferWithCount (count: int) : EventStream<'a list> =
        failwith "Not Implemented"

    member this.BufferWithTimeOrCount (delay: TimeSpan) (count: int) : EventStream<'a list> =
        failwith "Not Implemented"

    member this.ToProperty () : Property<'a> =
        failwith "Not Implemented"

    member this.ToPropertyWithInitialValue (initialValue: 'a) : Property<'a> =
        failwith "Not Implemented"

and Dispatcher<'a>(subscribe, ?handleEvent) as this =
    let mutable subscriptions : Subscription<'a> list = []
    let mutable queue : Event<'a> list = []
    let mutable ended = false
    let mutable pushing = false
    let mutable unsubscribeFromSource = fun () -> ()
    let removeSub subscription = 
        subscriptions <- List.filter ((=) subscription >> not) subscriptions
    let mutable waiters = None
    let done' () =
        if waiters.IsSome
        then
            for w in waiters.Value do w()
            waiters <- None
    let pushIt event =
        if not pushing
        then
            let mutable success = false
            try
                pushing <- true
                let tmp = subscriptions
                for sub in tmp do
                    match sub.sink event with
                    | NoMore -> removeSub sub
                    | _ -> 
                        match event with
                        | End -> removeSub sub
                        | _ -> ()
                success <- true
            finally
                pushing <- false
                queue <- if success then [] else queue
            success <- true
            while not queue.IsEmpty do
                let event = List.head queue
                queue <- List.tail queue
                this.Push event
            done'()
            if this.HasSubscribers
            then NoMore
            else
                unsubscribeFromSource()
                NoMore
        else
            queue <- List.concat [ queue; [event] ]
            More

    let handleEvent' = if handleEvent.IsSome then handleEvent.Value else this.Push

    abstract Push : Event<'a> -> unit
    default this.Push event = UpdateBarrier.Instance.inTransaction event this pushIt [event]
    
    member this.HasSubscribers with get() = List.length subscriptions > 0
    
    member this.Ended 
        with get() = ended 
        and set value = ended <- value
    
    member this.HandleEvent event =
        match event with
        | End -> this.Ended <- true
        | _ -> ()
        handleEvent' event

    abstract Subscribe : Sink<'a> -> IDisposable
    default this.Subscribe (sink : Sink<'a>) =
        if this.Ended
        then
            sink End |> ignore
            { new IDisposable with member x.Dispose() = () }
        else
            let subscription = new Subscription<'a>(sink)
            subscriptions <- List.concat [subscriptions; [subscription]]
            if subscriptions.Length = 1
            then
                let unsubSrc = subscribe this.HandleEvent
                unsubscribeFromSource <- 
                    fun () ->
                        unsubSrc()
                        unsubscribeFromSource <- fun () -> ()
            { new IDisposable with
                member x.Dispose () =
                    removeSub subscription
                    if not this.HasSubscribers then unsubscribeFromSource() }

type PropertyDispatcher<'a>(p, subscribe, handleEvent) as this =
    inherit Dispatcher<'a>(subscribe, handleEvent)

    let mutable current = None
    let mutable currentValueRootId = None
    let mutable push = this.Push
    let mutable subscrive = this.Subscribe
    
    override this.Push event =
        match event with
        | End -> this.Ended <- true
        | Next(value) -> 
            current <- Some(event)
            currentValueRootId <- UpdateBarrier.Instance.currentEventId()
        base.Push event

    override this.Subscribe sink =
        let mutable initSent = false
        let reply = ref More

        let maybeSubSource () =
            if !reply = NoMore
            then { new IDisposable with member x.Dispose() = () }
            elif this.Ended
            then
                sink End |> ignore
                { new IDisposable with member x.Dispose() = () }
            else
                base.Subscribe sink

        if current.IsSome && (this.HasSubscribers || this.Ended)
        then
            let dispatchingId = UpdateBarrier.Instance.currentEventId()
            let valId = currentValueRootId
            if not this.Ended && valId.IsSome && dispatchingId.IsSome && dispatchingId.Value <> valId.Value
            then
                UpdateBarrier.whenDoneWith p (fun () -> reply := sink <| Next(current.Value); !reply)
                maybeSubSource()
            else
                UpdateBarrier.inTransaction None this (fun () -> reply := sink <| Next(current.Value); !reply) []
                maybeSubSource()
        else
            maybeSubSource()

type Bus<'a>() = 
    member this.x = ()

module Bacon =
    let not (observable: Observable<bool>) : Observable<bool> = failwith "Not Implemented"