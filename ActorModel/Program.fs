
// Test
open System
open Akka.FSharp
use system = System.create "my-system" (Configuration.load())

let handleMessage (mailbox: Actor<'a>) msg =
    match msg with
    | Some x -> printf "%A" x
    | None -> ()

let aref = spawn system "my-actor" (actorOf2 handleMessage)
let blackHole = spawn system "black-hole" (actorOf (fun msg -> ()))

// // Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

// open System

// // Define a function to construct a message to print
// let from whom =
//     sprintf "from %s" whom

// [<EntryPoint>]
// let main argv =
//     let message = from "F#" // Call the function
//     printfn "Hello world %s" message
//     0 // return an integer exit code