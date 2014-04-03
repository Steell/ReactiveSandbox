module Node

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Input

open FSharp.Control
open FSharp.Control.Observable

open XamlTypes
open UndoRecorder

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

let new_node (window : MainWindow) (undo_record : UndoRecorder) : NodeUI =
    let node = NodeUI()

    let color_handler = initialize_color_change node undo_record
    let delete_handler = initialize_deletion node window undo_record

    node