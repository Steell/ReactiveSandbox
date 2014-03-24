module MainApp

open System
open System.Windows
open System.Windows.Controls
open FSharpx

type MainWindow = XAML<"MainWindow.xaml">

// offset:   Some(Point) = location on the rectangle the mouse is dragging from, or None if no drag is taking place
// position: Point       = current position of the mouse
type DragCommandData = { drag_offset : Point option; position : Point }
let world0 = { drag_offset=None; position=new Point(0., 0.) }

let initialize_rect_drag (window : MainWindow) =
    // produces a world updater for when the mouse moves
    let move_update =
        let move_handler (args : Input.MouseEventArgs) =
            // new position relative to the canvas
            let new_pos = args.GetPosition window.Canvas
            // whether or not the mouse is being pressed
            let dragging = 
                match args.LeftButton with
                | Input.MouseButtonState.Pressed -> true
                | _ -> false
            // update the position of the old world, 
            // and if the mouse button was released update the offset
            fun { drag_offset=offset } -> { position=new_pos; drag_offset=if dragging then offset else None }
        window.Canvas.PreviewMouseMove |> Observable.map move_handler

    // produces a world updater for drag flag when mouse is pressed
    let mouse_down_update = 
        let mouse_down_handler (args : Input.MouseButtonEventArgs) =
            let new_offset = args.GetPosition window.NodeRect
            // update the world's offset
            fun { position=pos } -> { drag_offset=Some(new_offset); position=pos }
        window.NodeRect.PreviewMouseDown |> Observable.map mouse_down_handler

    // one event that is fired if any of the events are fired
    Observable.merge move_update mouse_down_update
    |> Observable.scan (fun world f -> f world) world0 // take the result of the fired event and update the world
    |> Observable.filter (fun world -> world.drag_offset.IsSome) // ignore all updates where we're not dragging
    |> Observable.map (fun world -> world.position - world.drag_offset.Value) // calculate the new position of the rectangle
    |> Observable.subscribe (
        // update rectangle position
        fun new_position -> 
            Canvas.SetLeft(window.NodeCanvas, new_position.X)
            Canvas.SetTop(window.NodeCanvas, new_position.Y))

let initialize_color_change (window : MainWindow) =
    let node_click = 
        window.NodeButton.Click
        |> Observable.map
            (fun _ -> 
                fun (menu_state, button_state (* , _ *)) -> menu_state, not button_state(* , false *))

    let menu_click =
        window.HelloMenu.Click 
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
        window.NodeRect.Fill <- new_fill :> Media.Brush
        let text_update = async {
            let! _ = Async.AwaitEvent window.NodeRect.ContextMenuOpening
            window.HelloMenu.Header <- new_text
        }
        Async.StartImmediate text_update

    Observable.merge node_click menu_click
    |> Observable.scan (fun state f -> f state) (false, false)//, false)
    //|> Observable.filter (fun (_, _, change) -> change)
    //|> Observable.map (fun (m, b(* , _ *)) -> (m, b))
    |> Observable.subscribe hello

let loadWindow() =
    let window = MainWindow()

    let drag_handler = initialize_rect_drag window
    let color_handler = initialize_color_change window

    window.Root

[<STAThread>]
(new Application()).Run(loadWindow()) |> ignore