module Observable

open System

let scanAccumulate (state : 'a) (obs : IObservable<'a -> 'a>) =
    Observable.scan (fun state' f -> f state') state obs