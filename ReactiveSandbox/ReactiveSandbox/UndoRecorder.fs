module UndoRecorder

open System
open FSharp.Control
open FSharp.Control.Observable

type Action<'T> = 'T
type Command<'T> = { redo: Action<'T>; undo: Action<'T> }

type private RecorderAction = unit -> unit
type private RecorderCommand  = { redo_comm: RecorderAction; undo_comm: RecorderAction }

type private RecorderUpdate = Undo | Redo | Record of RecorderCommand

type private CommandStack = RecorderCommand list
type private RecorderState = { undo_stack: CommandStack; redo_stack: CommandStack }

type private ScanUpdate<'a, 'b> = UndoRedo of 'a | Original of 'b

type UndoRecorder() =
    
    let update_event = new Event<_>()

    let recorder_updated =
        let state0 = { undo_stack=[]; redo_stack=[] }

        let update_state (_, state) = function
            | Undo ->
                match state.undo_stack with
                | command::stack -> Some(command.undo_comm), { undo_stack=stack; redo_stack=command::state.redo_stack }
                | _              -> None, state
            | Redo -> 
                match state.redo_stack with
                | command::stack -> Some(command.redo_comm), { undo_stack=command::state.undo_stack; redo_stack=stack }
                | _              -> None, state
            | Record(command) -> None, { undo_stack=command::state.undo_stack; redo_stack=[] }
        
        update_event.Publish
        |> Observable.scan update_state (None, state0)
        |> Observable.choose (function (c, _) -> c)
        |> Observable.subscribe (fun c -> c())
       
    /// Creates a new Observable out of an Observable of Commands. As Commands come in via
    /// the original Observable, they are recorded for undo/redo and the redo Action is
    /// produced in the result Observable. If a Command produced by the original Observable
    /// is later undone or redone, the respective Action is produced in the result Observable.
    member x.RecordCommand (obs : IObservable<Command<'a>>) : IObservable<'a> =
        { new IObservable<'a> with
            member self.Subscribe(observer) = 

                let relay = 
                    async {
                        while true do
                            let! result = Async.AwaitObservable obs
                            let rec_command = 
                                { redo_comm=(fun () -> (observer.OnNext <| result.redo))
                                  undo_comm=(fun () -> (observer.OnNext <| result.undo)) }
                            update_event.Trigger <| Record(rec_command)
                            observer.OnNext result.redo }

                let cts = new System.Threading.CancellationTokenSource()
                Async.StartImmediate(relay, cts.Token)
                { new IDisposable with member this.Dispose() = cts.Cancel() } }

    /// Given an Observable of Commands of Actions (unit -> unit), record the commands for
    /// undo/redo and then immediately subscribe to them, where the subscription callback
    /// is the produced Action from the Command.
    member x.RecordAndSubscribe (obs : IObservable<Command<unit -> unit>>) : IDisposable =
        x.RecordCommand obs |> Observable.subscribe (fun f -> f())
        
    /// Similar to Observable.scan, but records all states for undo/redo. If an undo or redo
    /// occurs for one of these state changes, the result Observable will be passed the
    /// recorded state.
    member x.RecordScan (f : 'a -> 'b -> 'a) (initial_state : 'a) (obs : IObservable<'b>) : IObservable<'a> =
        { new IObservable<'a> with
            member self.Subscribe(observer) = 
                //Trigger this when you want to propagate 
                let trigger_event = new Event<'a>()
                let undo_redo_stream =  trigger_event.Publish |> Observable.map UndoRedo

                let rec scanner (state : 'a) = 
                    async {
                        let! result = 
                            obs
                            |> Observable.map Original
                            |> Observable.merge undo_redo_stream
                            |> Async.AwaitObservable
                        match result with
                        | Original(b) -> 
                            let new_state = f state b
                            let rec_command = 
                                { redo_comm=(fun () -> (trigger_event.Trigger <| new_state))
                                  undo_comm=(fun () -> (trigger_event.Trigger <| state)) }
                            update_event.Trigger <| Record(rec_command)
                            observer.OnNext new_state
                            return! scanner new_state 
                        | UndoRedo(a) ->
                            observer.OnNext a
                            return! scanner a
                    }

                //let disposer = trigger_event.Publish.Subscribe observer
                let cts = new System.Threading.CancellationTokenSource()
                Async.StartImmediate(scanner initial_state, cts.Token)
                { new IDisposable with 
                    member this.Dispose() = 
                        //disposer.Dispose() 
                        cts.Cancel() } }

    /// Records all data coming from the given Observable for undo/redo. Undoing or redoing
    /// will cause recorded data to be propagated to the result Observable.
    member x.Record (initial_state : 'a) (obs : IObservable<'a>) : IObservable<'a> =
        x.RecordScan (fun _ x -> x) initial_state obs

    /// Similar to Observable.scanAccumulate, but records all states for undo/redo. If an undo
    /// or redo occurs for one of these state changes, the result Observable will be passed the
    /// recorded state.
    member x.RecordScanAccum (initial_state : 'a) (obs : IObservable<'a -> 'a>) : IObservable<'a> =
        x.RecordScan (fun state f -> f state) initial_state obs

    /// Perfoms an Undo operation
    member x.PerformUndo() = update_event.Trigger Undo

    ///Performs a Redo operation
    member x.PerformRedo() = update_event.Trigger Redo

    interface IDisposable with
        member x.Dispose() = recorder_updated.Dispose()