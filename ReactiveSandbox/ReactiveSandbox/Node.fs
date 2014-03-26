module Node

open System
open System.Windows
open System.Windows.Controls

open FSharp.Control
open FSharp.Control.Observable

open XamlTypes

// offset:   Some(Point) = location on the rectangle the mouse is dragging from, or None if no drag is taking place
// position: Point       = current position of the mouse
type DragCommandData = { drag_offset : Point option; position : Point }
let world0 = { drag_offset=None; position=new Point(0., 0.) }

let initialize_rect_drag (node : NodeUI) move_update =
    // produces a world updater for drag flag when mouse is pressed
    let mouse_down_update = 
        let mouse_down_handler (args : Input.MouseButtonEventArgs) =
            let new_offset = args.GetPosition node.NodeRect
            // update the world's offset
            fun cd -> { cd with drag_offset=Some(new_offset); }
        node.NodeRect.PreviewMouseDown |> Observable.map mouse_down_handler

    // one event that is fired if any of the events are fired
    Observable.merge move_update mouse_down_update
    |> Observable.scan (fun world f -> f world) world0 // take the result of the fired event and update the world
    |> Observable.filter (fun world -> world.drag_offset.IsSome) // ignore all updates where we're not dragging
    |> Observable.map (fun world -> world.position - world.drag_offset.Value) // calculate the new position of the rectangle
    |> Observable.subscribe (
        // update rectangle position
        fun new_position -> 
            Canvas.SetLeft(node.Root, new_position.X)
            Canvas.SetTop(node.Root, new_position.Y))

let initialize_color_change (node : NodeUI) =
    let node_click = 
        node.NodeButton.Click
        |> Observable.map
            (fun _ -> 
                fun (menu_state, button_state (* , _ *)) -> menu_state, not button_state(* , false *))

    let menu_click =
        node.HelloMenu.Click 
        |> Observable.map
            (fun _ -> 
                fun (menu_state, button_state (* , _ *)) -> not menu_state, button_state(* , true *))

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
            node.HelloMenu.Header <- new_text
        }
        Async.StartImmediate text_update

    Observable.merge node_click menu_click
    |> Observable.scan (fun state f -> f state) (false, false)//, false)
    //|> Observable.filter (fun (_, _, change) -> change)
    //|> Observable.map (fun (m, b(* , _ *)) -> (m, b))
    |> Observable.subscribe hello

let new_node move_update (position : Point) : UIElement =
    let node = NodeUI()

    let drag_handler = initialize_rect_drag node move_update
    let color_handler = initialize_color_change node

    Canvas.SetLeft(node.Root, position.X)
    Canvas.SetTop(node.Root, position.Y)

    node.Root :> UIElement