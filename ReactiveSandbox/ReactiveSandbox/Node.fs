namespace Node

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Input

open FSharp.Control
open FSharp.Control.Observable

open XamlTypes
open UndoRecorder

type FuncCommand (canExec : (obj -> bool), doExec : (obj -> unit)) =
    let cecEvent = new DelegateEvent<EventHandler>()
    interface ICommand with
        [<CLIEvent>]
        member x.CanExecuteChanged = cecEvent.Publish
        member x.CanExecute arg = canExec(arg)
        member x.Execute arg = doExec(arg)

type NodeViewModel(undo_recorder : UndoRecorder, window : MainWindow) as vm =
    inherit Microsoft.Practices.Prism.ViewModel.NotificationObject()

    let mutable color = Media.Brushes.Black :> Media.Brush
    let mutable menu_text = "Hello"

    let button_event = new Event<_>()
    let menu_event = new Event<_>()
    let menu_open_event = new Event<_>()

    let color_change_handler =
        let node_click = 
            button_event.Publish
                |> Observable.map
                    (fun _ -> 
                        fun (menu_state, button_state) -> menu_state, not button_state)

        let menu_click =
            menu_event.Publish
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
            vm.Color <- new_fill :> Media.Brush
            let text_update = async {
                let! _ = Async.AwaitObservable menu_open_event.Publish
                vm.MenuText <- new_text }
            Async.StartImmediate text_update

        Observable.merge node_click menu_click
            |> undo_recorder.RecordStreamAndScanAccum "Changed Color" (false, false)
            |> Observable.subscribe hello

    let rtn_true _ = true
            
    member vm.ClickMe = new FuncCommand(rtn_true, button_event.Trigger)
    member vm.MenuOpen = new FuncCommand(rtn_true, menu_open_event.Trigger)
    member vm.HelloGoodbye = new FuncCommand(rtn_true, menu_event.Trigger)

    member vm.Color
        with get() = color
        and set (value) =
            color <- value
            vm.RaisePropertyChanged("Color")

    member vm.MenuText
        with get() = menu_text
        and set (value) =
            menu_text <- value
            vm.RaisePropertyChanged("MenuText")


module Node =
    let initialize_deletion (node : NodeUI) (window : MainWindow) (undo_record : UndoRecorder) =
        node.DeleteMenu.Click 
            |> Observable.map (fun _ -> node.Root)
            |> undo_record.RecordStream "Delete Node"
            |> Observable.subscribe (function
                | Redo(node) -> window.NodeCanvas.Children.Remove node
                | Undo(node) -> window.NodeCanvas.Children.Add node |> ignore)

    let new_node (window : MainWindow) (undo_record : UndoRecorder) : NodeUI =
        let node = NodeUI()

        node.Root.DataContext <- new NodeViewModel(undo_record, window) :> obj

        let delete_handler = initialize_deletion node window undo_record

        node