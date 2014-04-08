namespace RxSandbox.Node

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Input

open FSharp.Control
open FSharp.Control.Observable

open RxSandbox.XamlTypes
open RxSandbox.UndoRecorder
open RxSandbox.Command

type IDeletable =
    [<CLIEvent>] abstract DeleteEvent : IEvent<obj>

type NodeViewModel(undo_recorder : UndoRecorder) as vm =
    inherit Microsoft.Practices.Prism.ViewModel.NotificationObject()

    let mutable color = Media.Brushes.Black :> Media.Brush
    let mutable menu_text = "Hello"

    let button_event = new Event<_>()
    let menu_event = new Event<_>()
    let menu_open_event = new Event<_>()
    let mouse_down_event = new Event<MouseButtonEventArgs>()
    let delete_event = new Event<_>()

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
            
    member vm.ClickMe = new FuncCommand(button_event.Trigger)
    member vm.MenuOpen = new FuncCommand(menu_open_event.Trigger)
    member vm.HelloGoodbye = new FuncCommand(menu_event.Trigger)
    member vm.MouseDown = new FuncCommand(fun o -> o :?> MouseButtonEventArgs |> mouse_down_event.Trigger)
    member vm.Delete = new FuncCommand(delete_event.Trigger)

    [<CLIEvent>] member vm.MouseDownEvent = mouse_down_event.Publish

    member vm.Color
        with get() = color
        and set value =
            color <- value
            vm.RaisePropertyChanged("Color")

    member vm.MenuText
        with get() = menu_text
        and set value =
            menu_text <- value
            vm.RaisePropertyChanged("MenuText")

    
    interface IDeletable with
        [<CLIEvent>] member vm.DeleteEvent = delete_event.Publish


module Node =

    let new_node (undo_record : UndoRecorder) : NodeUI * NodeViewModel =
        let node = NodeUI() //TODO: this should not happen here, handle in XAML
        let vm = new NodeViewModel(undo_record)
        node.Root.DataContext <- vm :> obj
        node, vm