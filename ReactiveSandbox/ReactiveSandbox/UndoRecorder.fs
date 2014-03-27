module UndoRecorder

open System
open FSharp.Control.Observable

type Action<'T> = 'T
type Command<'T> = { redo: Action<'T>; undo: Action<'T> }

type private RecorderAction = unit -> unit
type private RecorderCommand  = { redo_comm: RecorderAction; undo_comm: RecorderAction }

type private RecorderUpdate = Undo | Redo | Record of RecorderCommand

type private CommandStack = RecorderCommand list
type private RecorderState = { undo_stack: CommandStack; redo_stack: CommandStack }

let private state0 = { undo_stack=[]; redo_stack=[] }


type UndoRecorder() =
    
    let update_event = new Event<_>()

    let recorder_updated =
        let update_state (_, state) = function
            | Undo ->
                match state.undo_stack with
                | command::stack -> Some(command.undo_comm), { undo_stack=stack; redo_stack=command::state.redo_stack }
                | _              -> None,               state
            | Redo -> 
                match state.redo_stack with
                | command::stack -> Some(command.redo_comm), { undo_stack=command::state.undo_stack; redo_stack=stack }
                | _              -> None,             state
            | Record(command) -> Some(command.redo_comm), { undo_stack=command::state.undo_stack; redo_stack=[] }
        update_event.Publish
        |> Observable.scan update_state (None, state0)
        |> Observable.choose (function (c, _) -> c)
        |> Observable.subscribe (fun c -> c())
        
    member x.Record (obs : IObservable<Command<'a>>) : IObservable<'a> =
        let trigger_event = new Event<'a>()
        let relay = 
            async {
                while true do
                    let! result = Async.AwaitObservable obs
                    let rec_command = 
                        { redo_comm=(fun () -> (trigger_event.Trigger <| result.redo))
                          undo_comm=(fun () -> (trigger_event.Trigger <| result.undo)) }
                    update_event.Trigger <| Record(rec_command) }
        { new IObservable<'a> with
            member self.Subscribe(observer) = 
                let disposer = trigger_event.Publish.Subscribe observer
                let cts = new System.Threading.CancellationTokenSource()
                Async.StartImmediate(relay, cts.Token)
                { new IDisposable with 
                    member this.Dispose() = 
                        cts.Cancel()
                        disposer.Dispose() } }

    member x.RecordAndSubscribe (obs : IObservable<Command<unit -> unit>>) : IDisposable =
        x.Record obs |> Observable.subscribe (fun f -> f())

    member x.AutoRecord (obs : IObservable<'a>) : IObservable<'a> =
        obs
        |> Observable.pairwise
        |> Observable.map (function old, current -> { undo=old; redo=current })
        |> x.Record

    member x.PerformUndo() = update_event.Trigger Undo
    member x.PerformRedo() = update_event.Trigger Redo

    interface IDisposable with
        member x.Dispose() =
            recorder_updated.Dispose()