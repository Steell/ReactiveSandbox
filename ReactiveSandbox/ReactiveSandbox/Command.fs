namespace RxSandbox.Command

open System
open System.Windows.Input

type FuncCommand (doExec : (obj -> unit), ?canExec : (obj -> bool)) =
    let cecEvent = new DelegateEvent<EventHandler>()
    interface ICommand with
        [<CLIEvent>]
        member x.CanExecuteChanged = cecEvent.Publish
        member x.CanExecute arg = 
            match canExec with
            | Some(f) -> f arg
            | None -> true
        member x.Execute arg = doExec arg