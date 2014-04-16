namespace Bacon

open System

type Event<'a> = Next of (unit -> 'a) | End

type SinkResult = More | NoMore
type Sink<'a> = Event<'a> -> SinkResult

type Observable<'a> =
    abstract OnValue : ('a -> unit) -> IDisposable
    abstract OnEnd : (unit -> unit) -> IDisposable
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
    
type Bus<'a>() = 
    member this.x = ()

module Observable =
    let not (observable: Observable<bool>) : Observable<bool> = failwith "Not Implemented"