module MainApp

open System
open System.Windows
open System.Windows.Controls

open FSharp.Control

open XamlTypes
open UndoRecorder

type NewNodeCommand = SetPosition of Point | CreateNode    

let setup_undo_redo (window : MainWindow) undo redo =
    window.UndoMenuItem.Click
    |> Observable.subscribe (fun _ -> undo()),
    window.RedoMenuItem.Click
    |> Observable.subscribe (fun _ -> redo())

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

    let new_node_action node () =
        node |> window.NodeCanvas.Children.Add |> ignore

    let del_node_action node () =
        node |> window.NodeCanvas.Children.Remove |> ignore

    let new_node_command node = { redo=new_node_action node; undo=del_node_action node }

    let create_handler = 
        let node_stream = 
            Observable.merge new_pos_updater creation_updater
            |> Observable.pairwise
            |> Observable.choose    (function SetPosition(p), CreateNode -> Some(p) | _ -> None)
            |> Observable.map       (Node.new_node window undo_recorder >> new_node_command)
        node_stream
        |> undo_recorder.RecordAndSubscribe

    window.Root

[<STAThread>]
(new Application()).Run(loadWindow()) |> ignore