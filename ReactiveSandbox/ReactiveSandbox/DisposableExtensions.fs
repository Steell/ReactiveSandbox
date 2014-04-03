module IDisposable

open System

let merge (d1 : IDisposable) (d2 : IDisposable) =
    { new IDisposable with
        member x.Dispose() =
            d1.Dispose()
            d2.Dispose() }

let merge_all (ds : IDisposable seq) =
    { new IDisposable with
        member x.Dispose() =
            for d in ds do
                d.Dispose() }