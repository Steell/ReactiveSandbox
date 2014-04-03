module UndoRecorder

open System
open FSharp.Control
open FSharp.Control.Observable

type Entry<'a, 'b> = { redo: 'a; undo: 'b; description: string }
type Update<'a, 'b> = Redo of 'a | Undo of 'b

type private RecorderAction = unit -> unit
type private RecorderCommand  = { redo_comm: RecorderAction; undo_comm: RecorderAction; info: string }

type private CommandStack = RecorderCommand list
type private RecorderState = { undo_stack: CommandStack; redo_stack: CommandStack }

type private ScanUpdate<'a, 'b> = UndoRedo of 'a | Original of 'b

/// Records data from Observables and gives a way to re-send old data.
type UndoRecorder() =
    
    let mutable undo_stack : RecorderCommand list = []
    let mutable redo_stack : RecorderCommand list = []

    let record command =
        undo_stack <- command::undo_stack
        redo_stack <- []
        
    member x.UndoStack = undo_stack |> List.map (fun c -> c.info)
    member x.RedoStack = redo_stack |> List.map (fun c -> c.info)

    /// Perfoms an Undo operation
    member x.PerformUndo() =
        match undo_stack with
        | command::stack ->
            undo_stack <- stack
            redo_stack <- command::redo_stack
            command.undo_comm()
        | _ -> ()

    /// Performs a Redo operation
    member x.PerformRedo() = 
        match redo_stack with
            | command::stack ->
                undo_stack <- command::undo_stack
                redo_stack <- stack
                command.redo_comm()
            | _ -> ()

    /// Records an Entry (does not actually perform it until undo/redo occurs)
    member x.Record (command : Entry<unit -> unit, unit -> unit>) : unit =
        record { 
            redo_comm=command.redo;
            undo_comm=command.undo;
            info=command.description }
       
    /// Creates a new Observable out of an Observable of Entrys. As Entrys come in via
    /// the original Observable, they are recorded for undo/redo and the redo Action is
    /// produced in the result Observable. If an Entry produced by the original Observable
    /// is later undone or redone, the respective data is produced in the result Observable.
    member x.RecordEntryStream (obs : IObservable<Entry<'a, 'b>>) : IObservable<Update<'a, 'b>> =
        { new IObservable<Update<'a, 'b>> with
            member self.Subscribe observer = 

                let recorder data =                        
                    record { 
                        redo_comm=(fun () -> observer.OnNext <| Redo(data.redo));
                        undo_comm=(fun () -> observer.OnNext <| Undo(data.undo));
                        info=data.description }
                    observer.OnNext <| Redo(data.redo)

                obs |> Observable.subscribe recorder }

    /// Shorthand for RecordEntryStream that doesn't require data to be packed into an Entry,
    /// but uses the same data for Undo and Redo.
    member x.RecordStream (description : string) (obs : IObservable<'a>) : IObservable<Update<'a, 'a>> =
        { new IObservable<Update<'a, 'a>> with
            member self.Subscribe observer = 

                let recorder data =
                    record { 
                        redo_comm=(fun () -> observer.OnNext <| Redo(data));
                        undo_comm=(fun () -> observer.OnNext <| Undo(data));
                        info=description }
                    observer.OnNext <| Redo(data)

                obs |> Observable.subscribe recorder }
        
    /// Similar to Observable.scan, but records all states for undo/redo. If an undo or redo
    /// occurs for one of these state changes, the result Observable will be passed the
    /// recorded state.
    member x.RecordStreamAndScan 
        (description   : string)
        (state_updater : 'a -> 'b -> 'a)
        (initial_state : 'a)
        (obs           : IObservable<'b>)
        : IObservable<'a> =
            { new IObservable<'a> with
                member self.Subscribe observer = 

                    let state = ref initial_state
                    let rec recorder data =
                        match data with
                        | Original(b) ->
                            let old_state = !state
                            let new_state = state_updater old_state b
                            record { 
                                redo_comm=(fun () -> recorder <| UndoRedo(new_state));
                                undo_comm=(fun () -> recorder <| UndoRedo(old_state));
                                info = description }
                            observer.OnNext new_state
                            state := new_state
                        | UndoRedo(new_state) ->
                            observer.OnNext new_state
                            state := new_state
                
                    obs |> Observable.subscribe (Original >> recorder) }

    /// Records all data coming from the given Observable for undo/redo. Undoing or redoing
    /// will cause recorded data to be propagated to the result Observable.
    member x.RecordStreamState (description : string) (initial_state : 'a) (obs : IObservable<'a>) : IObservable<'a> =
        x.RecordStreamAndScan description (fun _ x -> x) initial_state obs

    /// Similar to Observable.scanAccumulate, but records all states for undo/redo. If an undo
    /// or redo occurs for one of these state changes, the result Observable will be passed the
    /// recorded state.
    member x.RecordStreamAndScanAccum (description : string) (initial_state : 'a) (obs : IObservable<'a -> 'a>) : IObservable<'a> =
        x.RecordStreamAndScan description (fun state f -> f state) initial_state obs