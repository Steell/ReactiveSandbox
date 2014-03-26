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

    let add_node = window.NodeCanvas.Children.Add >> ignore

    Observable.merge new_pos_updater creation_updater
    |> Observable.pairwise
    |> Observable.choose    (function SetPosition(p), CreateNode -> Some(p) | _ -> None)
    |> Observable.subscribe (new_node window >> add_node)

let loadWindow() =
    let window = MainWindow()
    let create_handler = initialize_menu window
    window.Root

[<STAThread>]
(new Application()).Run(loadWindow()) |> ignore