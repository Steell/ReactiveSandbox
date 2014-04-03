module Node

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Input

open FSharp.Control
open FSharp.Control.Observable

open XamlTypes
open UndoRecorder

type DragCommand = Start | Stop

let initialize_rect_drag (node : NodeUI) (window : MainWindow) (undo_record : UndoRecorder) =
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
        |> Observable.map    (fun args -> Some(args.GetPosition node.Root))
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

let initialize_color_change (node : NodeUI) (undo_recorder : UndoRecorder) =
    let node_click = 
        node.NodeButton.Click
        |> Observable.map
            (fun _ -> 
                fun (menu_state, button_state) -> menu_state, not button_state)

    let menu_click =
        node.HelloMenu.Click 
        |> Observable.map
            (fun _ -> 
                fun (menu_state, button_state) -> not menu_state, button_state)

    let hello (m, b) =
        let new_fill, new_text = 
            if m then 
                let color = if b then Media.Brushes.Green else Media.Brushes.Red
                color, "Goodbye" 
            else 
                let color = if b then Media.Brushes.Gold else Media.Brushes.Black
                color, "Hello"
        node.NodeRect.Fill <- new_fill :> Media.Brush
        let text_update = async {
            let! _ = Async.AwaitObservable node.NodeRect.ContextMenuOpening
            node.HelloMenu.Header <- new_text }
        Async.StartImmediate text_update

    Observable.merge node_click menu_click
    |> undo_recorder.RecordStreamAndScanAccum "Changed Color" (false, false)
    |> Observable.subscribe hello

let initialize_deletion (node : NodeUI) (window : MainWindow) (undo_record : UndoRecorder) =
    node.DeleteMenu.Click 
    |> Observable.map (fun _ -> node.Root)
    |> undo_record.RecordStream "Delete Node"
    |> Observable.subscribe (function
        | Redo(node) -> window.NodeCanvas.Children.Remove node
        | Undo(node) -> window.NodeCanvas.Children.Add node |> ignore)

let new_node (window : MainWindow) (undo_record : UndoRecorder) (position : Point) : UIElement =
    let node = NodeUI()

    Canvas.SetLeft(node.Root, position.X) // - node.NodeCanvas.Width / 2.)
    Canvas.SetTop(node.Root, position.Y)  // - node.NodeCanvas.Height / 2.)

    let drag_handler = initialize_rect_drag node window undo_record
    let color_handler = initialize_color_change node undo_record
    let delete_handler = initialize_deletion node window undo_record

    node.Root :> UIElement