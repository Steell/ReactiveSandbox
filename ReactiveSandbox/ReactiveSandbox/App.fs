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

let initialize_drag (node : NodeUI) (window : MainWindow) (undo_record : UndoRecorder) (position : Point) =
    
    let update_pos (position : Point) =
        Canvas.SetLeft(node.Root, position.X)
        Canvas.SetTop(node.Root, position.Y)

    update_pos position
    
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

    let move_node_action position () = update_pos position

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
    
    let drag_command_handler =
        drag_command_stream
            |> undo_record.RecordStreamState "Moved node" (get_current_position())
            |> Observable.subscribe update_pos

    let continuous_drag_handler =
        Observable.combineLatest start_stop_update move_update
            |> Observable.choose    (function Some(offset), position -> Some(position - offset) | _ -> None)
            |> Observable.subscribe (
                // update rectangle position
                fun new_position -> 
                    Canvas.SetLeft(node.Root, new_position.X)
                    Canvas.SetTop(node.Root, new_position.Y))

    IDisposable.merge drag_command_handler continuous_drag_handler

let loadWindow() =
    let window = MainWindow()
    let undo_recorder = new UndoRecorder()
    
    let undo_handler =
        window.UndoMenuItem.Click
            |> Observable.subscribe (fun _ -> undo_recorder.PerformUndo())
    
    let redo_handler =
        window.RedoMenuItem.Click
            |> Observable.subscribe (fun _ -> undo_recorder.PerformRedo())

    let new_pos_updater =
        window.NodeCanvas.ContextMenuOpening
            |> Observable.map (fun args -> SetPosition(new Point(args.CursorLeft, args.CursorTop)))

    let creation_updater =
        window.NewNodeMenuItem.Click
            |> Observable.map (fun _ -> CreateNode)

    let make_node (position : Point) =
        let node = Node.new_node window undo_recorder
        initialize_drag node window undo_recorder position |> ignore
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