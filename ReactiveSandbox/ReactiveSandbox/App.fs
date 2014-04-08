namespace RxSandbox

open System
open System.Collections.ObjectModel
open System.Windows
open System.Windows.Controls
open System.Windows.Input

open FSharp.Control

open RxSandbox.XamlTypes
open RxSandbox.UndoRecorder
open RxSandbox.Node
open RxSandbox.Command

type NewNodeCommand = SetPosition of Point | CreateNode    

type DragCommand = Start | Stop

type MainWindowContentContainer(element, position: Point) =
    inherit Microsoft.Practices.Prism.ViewModel.NotificationObject()

    let mutable x = position.X
    let mutable y = position.Y

    //TODO: Reference a ViewModel, not a UIElement. 
    //      This will require a change to the XAML as well (DataTemplate?)
    member self.UIElement : UIElement = element

    member self.MouseDownEvent = element.MouseDown
    member self.MouseUpEvent = element.MouseUp
    
    member self.X
        with get() = x
        and set newX = 
            x <- newX
            self.RaisePropertyChanged("X")

    member self.Y
        with get() = y
        and set newY = 
            y <- newY
            self.RaisePropertyChanged("Y")


type MainWindowViewModel(undo_recorder: UndoRecorder, drag_root: IInputElement) =
    
    let new_node_event = new Event<_>()
    let new_node_pos_event = new Event<ContextMenuEventArgs>()
    let mouse_move_event = new Event<MouseEventArgs>()
    let mouse_up_event = new Event<MouseButtonEventArgs>()

    let contents = new ObservableCollection<MainWindowContentContainer>()


    let initialize_drag (node : MainWindowContentContainer) (position : Point) =
    
        let update_pos (position : Point) =
            node.X <- position.X
            node.Y <- position.Y

        let get_current_position() =
            new Point(node.X, node.Y)
    
        // produces a world updater for when the mouse moves
        let move_update =
            mouse_move_event.Publish
                |> Observable.choose (
                    fun args -> 
                        if args.LeftButton = MouseButtonState.Pressed 
                        then Some(args.GetPosition drag_root)
                        else None)

        let start_update = 
            node.MouseDownEvent
                |> Observable.filter (fun args -> args.ChangedButton = MouseButton.Left)
                |> Observable.map    (fun args -> Some(args.GetPosition (args.OriginalSource :?> IInputElement)))
        let stop_update =
            Observable.merge node.MouseUpEvent mouse_up_event.Publish
                |> Observable.filter (fun args -> args.ChangedButton = MouseButton.Left)
                |> Observable.map    (fun _ -> None)

        // produces a world updater for drag flag when mouse is pressed
        let start_stop_update = Observable.merge start_update stop_update

        let move_node_action position () = update_pos position
        
        let drag_command_stream = 
            node.MouseDownEvent
                |> Observable.filter (fun args -> args.ChangedButton = MouseButton.Left)
                |> Observable.map    (fun args -> Start)
                |> Observable.merge (
                    Observable.merge node.MouseUpEvent mouse_up_event.Publish
                        |> Observable.filter (fun args -> args.ChangedButton = MouseButton.Left)
                        |> Observable.map    (fun _ -> Stop))
                |> Observable.pairwise
                |> Observable.choose (
                    function 
                        | Start, Stop -> Some(get_current_position())
                        | _ -> None)
    
        let drag_command_handler =
            drag_command_stream
                |> undo_recorder.RecordStreamState "Moved node" (get_current_position())
                |> Observable.subscribe update_pos

        let continuous_drag_handler =
            Observable.combineLatest start_stop_update move_update
                |> Observable.choose    (function Some(offset), position -> Some(position - offset) | _ -> None)
                |> Observable.subscribe (
                    // update rectangle position
                    fun new_position -> 
                        node.X <- new_position.X
                        node.Y <- new_position.Y)

        IDisposable.merge drag_command_handler continuous_drag_handler


    let initialize_deletion (d: IDeletable) (node : MainWindowContentContainer) =
        d.DeleteEvent
            |> Observable.map (fun _ -> node)
            |> undo_recorder.RecordStream "Delete Node"
            |> Observable.subscribe (function
                | Redo(node) -> contents.Remove node |> ignore
                | Undo(node) -> contents.Add node)


    let new_node_handler =
        let new_pos_updater =
            new_node_pos_event.Publish
                |> Observable.map (fun args -> SetPosition(new Point(args.CursorLeft, args.CursorTop)))

        let creation_updater =
            new_node_event.Publish
                |> Observable.map (fun _ -> CreateNode)

        let make_node (position : Point) =
            let node, vm = Node.new_node undo_recorder
            let container = new MainWindowContentContainer(node.Root, position)
            initialize_drag container position |> ignore
            initialize_deletion (vm :> IDeletable) container |> ignore
            container

        let add_node = contents.Add
        let del_node = contents.Remove >> ignore

        Observable.merge new_pos_updater creation_updater
            |> Observable.pairwise
            |> Observable.choose (function SetPosition(p), CreateNode -> Some(p) | _ -> None)
            |> Observable.map make_node
            |> undo_recorder.RecordStream "New Node"
            |> Observable.subscribe (function Redo(node) -> add_node node | Undo(node) -> del_node node)
        

    member vm.Contents : ObservableCollection<MainWindowContentContainer> = contents

    member vm.MouseUp = new FuncCommand(fun o -> o :?> MouseButtonEventArgs |> mouse_up_event.Trigger)
    member vm.MouseMove = new FuncCommand(fun o -> o :?> MouseEventArgs |> mouse_move_event.Trigger)
    member vm.NewNode = new FuncCommand(new_node_event.Trigger)
    member vm.SetNewNodePos = new FuncCommand(fun o -> o :?> ContextMenuEventArgs |> new_node_pos_event.Trigger)
    member vm.Undo = new FuncCommand(fun _ -> undo_recorder.PerformUndo())
    member vm.Redo = new FuncCommand(fun _ -> undo_recorder.PerformRedo())


module MainApp =

    let loadWindow() =
        let undo_recorder = new UndoRecorder()

        let window = MainWindow()
        window.Root.DataContext <- new MainWindowViewModel(undo_recorder, window.DragCanvas)

        window.Root

    [<STAThread; EntryPoint>]
    let main _ =
        (new Application()).Run(loadWindow())