module MainApp

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Input

open FSharp.Control

open XamlTypes
open UndoRecorder

type NewNodeCommand = SetPosition of Point | CreateNode    

type DragCommand = Start | Stop

let setup_undo_redo (window : MainWindow) undo redo =
    window.UndoMenuItem.Click
    |> Observable.subscribe (fun _ -> undo()),
    window.RedoMenuItem.Click
    |> Observable.subscribe (fun _ -> redo())

let initialize_drag (node : NodeUI) (window : MainWindow) (undo_record : UndoRecorder) =
    // produces a world updater for when the mouse moves
    let move_update =
        window.NodeCanvas.MouseMove
        |> Observable.choose (
            fun args -> 
                if args.LeftButton = MouseButtonState.Pressed 
                then Some(args.GetPosition window.Root) 
                else None)

    let start_update = 
        node.NodeRect.MouseDown
        |> Observable.filter (fun args -> args.ChangedButton = MouseButton.Left)
        |> Observable.map    (fun args -> Some(args.GetPosition node.NodeRect))
    let stop_update =
        Observable.merge node.NodeRect.MouseUp window.NodeCanvas.MouseUp
        |> Observable.filter (fun args -> args.ChangedButton = MouseButton.Left)
        |> Observable.map    (fun _ -> None)

    // produces a world updater for drag flag when mouse is pressed
    let start_stop_update = Observable.merge start_update stop_update

    let update_pos (position : Point) =
        Canvas.SetLeft(node.Root, position.X)
        Canvas.SetTop(node.Root, position.Y)

    let move_node_action position () = update_pos position

    //let move_node_command origin position = { redo=position; undo=origin }

    let get_current_position() =
        new Point(Canvas.GetLeft node.Root, Canvas.GetTop node.Root)

    let drag_command_stream = 
        node.NodeRect.MouseDown
        |> Observable.filter (fun args -> args.ChangedButton = MouseButton.Left)
        |> Observable.map    (fun args -> Start)
        |> Observable.merge (
            Observable.merge node.NodeRect.MouseUp window.NodeCanvas.MouseUp
            |> Observable.filter (fun args -> args.ChangedButton = MouseButton.Left)
            |> Observable.map    (fun _ -> Stop))
        |> Observable.pairwise
        |> Observable.choose (
            function 
                | Start, Stop -> Some(get_current_position())
                | _ -> None)
    
    drag_command_stream
    |> undo_record.RecordStreamState "Moved node" (get_current_position())
    |> Observable.subscribe update_pos
    |> ignore

    // one event that is fired if any of the events are fired
    Observable.combineLatest start_stop_update move_update
    |> Observable.choose    (function Some(offset), position -> Some(position - offset) | _ -> None)
    |> Observable.subscribe (
        // update rectangle position
        fun new_position -> 
            Canvas.SetLeft(node.Root, new_position.X)
            Canvas.SetTop(node.Root, new_position.Y))

let loadWindow() =
    let window = MainWindow()
    let undo_recorder = new UndoRecorder()
    
    let undo_handler, redo_handler = setup_undo_redo window undo_recorder.PerformUndo undo_recorder.PerformRedo

    let new_pos_updater =
        window.NodeCanvas.ContextMenuOpening
        |> Observable.map (fun args -> SetPosition(new Point(args.CursorLeft, args.CursorTop)))

    let creation_updater =
        window.NewNodeMenuItem.Click
        |> Observable.map (fun _ -> CreateNode)

    let make_node (position : Point) =
        let node = Node.new_node window undo_recorder
        Canvas.SetLeft(node.Root, position.X) // - node.NodeCanvas.Width / 2.)
        Canvas.SetTop(node.Root, position.Y)  // - node.NodeCanvas.Height / 2.)
        initialize_drag node window undo_recorder |> ignore
        node

    let add_node = window.NodeCanvas.Children.Add >> ignore
    let del_node = window.NodeCanvas.Children.Remove

    let create_handler = 
        Observable.merge new_pos_updater creation_updater
        |> Observable.pairwise
        |> Observable.choose (function SetPosition(p), CreateNode -> Some(p) | _ -> None)
        |> Observable.map make_node
        |> undo_recorder.RecordStream "New Node"
        |> Observable.subscribe (function Redo(node) -> add_node node.Root | Undo(node) -> del_node node.Root)

    window.Root

[<STAThread>]
(new Application()).Run(loadWindow()) |> ignore