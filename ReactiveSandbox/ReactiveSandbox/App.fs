module MainApp

open System
open System.Windows
open System.Windows.Controls
open FSharpx

type MainWindow = XAML<"MainWindow.xaml">

type World = { drag_offset : Point option; position : Point }

let world0 = { drag_offset=None; position=new Point(0., 0.) }

let loadWindow() =
    let window = MainWindow()
     
    let move_update =
        let move_handler (args : Input.MouseEventArgs) =
            let new_pos = args.GetPosition window.Canvas
            fun { drag_offset=offset } -> { position=new_pos; drag_offset=offset }
        window.Canvas.MouseMove |> Observable.map move_handler

    let mouse_down_update = 
        let mouse_down_handler (args : Input.MouseButtonEventArgs) =
            let new_offset = args.GetPosition window.Rectangle
            fun { position=pos } -> { drag_offset=Some(new_offset); position=pos }
        window.Rectangle.MouseDown |> Observable.map mouse_down_handler

    let mouse_up_update =
        let mouse_up_handler (args : Input.MouseButtonEventArgs) { position=pos } = 
            { drag_offset=None; position=pos }
        window.Rectangle.MouseUp |> Observable.map mouse_up_handler

    let all_updates = [ move_update; mouse_down_update; mouse_up_update ] |> List.reduce Observable.merge

    let rect_drag_handle = 
        all_updates
        |> Observable.scan (fun world f -> f world) world0
        |> Observable.filter (fun world -> world.drag_offset.IsSome)
        |> Observable.map (fun world -> world.position - world.drag_offset.Value)
        |> Observable.subscribe (
            fun new_position -> 
                Canvas.SetLeft(window.Rectangle, new_position.X)
                Canvas.SetTop(window.Rectangle, new_position.Y))
   
    window.Root

[<STAThread>]
(new Application()).Run(loadWindow()) |> ignore