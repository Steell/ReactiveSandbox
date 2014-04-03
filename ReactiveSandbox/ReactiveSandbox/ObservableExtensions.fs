module Observable

open System

let scanAccumulate (state : 'a) (obs : IObservable<'a -> 'a>) =
    Observable.scan (fun state' f -> f state') state obs

let interval (timespan : TimeSpan) obs =
    let timer = new System.Timers.Timer(timespan.TotalMilliseconds)
    timer.Elapsed |> Observable.scan (fun state _ -> state + 1) -1

let timestamp obs = Observable.map (fun x -> x, DateTime.Now) obs

let never = { new IObservable<_> with member x.Subscribe _ = { new IDisposable with member x.Dispose()=() } }

let merge_all obs = Seq.fold Observable.merge never obs