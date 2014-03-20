module MainApp

open System
open System.Windows
open System.Windows.Controls
open FSharpx

type MainWindow = XAML<"MainWindow.xaml">

// offset:   Some(Point) = location on the rectangle the mouse is dragging from, or None if no drag is taking place
// position: Point       = current position of the mouse
type World = { drag_offset : Point option; position : Point }

let world0 = { drag_offset=None; position=new Point(0., 0.) }

let loadWindow() =
    let window = MainWindow()
    
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
            fun { drag_offset=offset } -> { position=new_pos; drag_offset= if dragging then offset else None }
        window.Canvas.PreviewMouseMove |> Observable.map move_handler

    // produces a world updater for drag flag when mouse is pressed
    let mouse_down_update = 
        let mouse_down_handler (args : Input.MouseButtonEventArgs) =
            let new_offset = args.GetPosition window.Rectangle
            // update the world's offset
            fun { position=pos } -> { drag_offset=Some(new_offset); position=pos }
        window.Rectangle.MouseDown |> Observable.map mouse_down_handler

//    let mouse_up_update =
//        let mouse_up_handler (args : Input.MouseButtonEventArgs) { position=pos } = 
//            { drag_offset=None; position=pos }
//        window.Canvas.MouseUp |> Observable.map mouse_up_handler

    // one event that is fired if any of the events are fired
    let all_updates = [ move_update; mouse_down_update; (* mouse_up_update *) ] |> List.reduce Observable.merge

    let rect_drag_handle = 
        all_updates
        |> Observable.scan (fun world f -> f world) world0 // take the result of the fired event and update the world
        |> Observable.filter (fun world -> world.drag_offset.IsSome) // ignore all updates where we're not dragging
        |> Observable.map (fun world -> world.position - world.drag_offset.Value) // calculate the new position of the rectangle
        |> Observable.subscribe (
            // update rectangle position
            fun new_position -> 
                Canvas.SetLeft(window.Rectangle, new_position.X)
                Canvas.SetTop(window.Rectangle, new_position.Y))
   
    window.Root

[<STAThread>]
(new Application()).Run(loadWindow()) |> ignore