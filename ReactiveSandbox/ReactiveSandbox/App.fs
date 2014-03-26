module MainApp

open System
open System.Windows
open System.Windows.Controls

open FSharp.Control


open XamlTypes
open Node

type NewNodeCommand = SetPosition of Point | CreateNode

let initialize_menu (window : MainWindow) =
    let new_pos_updater =
        window.NodeCanvas.ContextMenuOpening
        |> Observable.map (fun args -> SetPosition(new Point(args.CursorLeft, args.CursorTop)))

    let creation_updater =
        window.NewNodeMenuItem.Click
        |> Observable.map (fun _ -> CreateNode)

    // produces a world updater for when the mouse moves
    let move_update =
        let move_handler (args : Input.MouseEventArgs) =
            // new position relative to the canvas
            let new_pos = args.GetPosition window.NodeCanvas
            // whether or not the mouse is being pressed
            let dragging = 
                match args.LeftButton with
                | Input.MouseButtonState.Pressed -> true
                | _ -> false
            // update the position of the old world, 
            // and if the mouse button was released update the offset
            fun { drag_offset=offset } -> { position=new_pos; drag_offset=if dragging then offset else None }
        window.NodeCanvas.PreviewMouseMove |> Observable.map move_handler

    let add_node = window.NodeCanvas.Children.Add >> ignore

    Observable.merge new_pos_updater creation_updater
    |> Observable.pairwise
    |> Observable.choose    (function SetPosition(p), CreateNode -> Some(p) | _ -> None)
    |> Observable.subscribe (new_node window move_update >> add_node)

let loadWindow() =
    let window = MainWindow()
    let create_handler = initialize_menu window
    window.Root

[<STAThread>]
(new Application()).Run(loadWindow()) |> ignore