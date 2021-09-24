

open System
open Akka.FSharp
open Akka.Actor
// open Akka.TestKit


// let system = ActorSystem.Create("Woker", Configuration.defaultConfig())
let system = System.create "my-system" (Configuration.load())
type Information = 
    | Input of (int64*int64*int64)
    | Done of (string)
    // | Input of (int64*int64)

// Printer Actor - To print the output
let printerActor (mailbox:Actor<_>) = 
    let rec loop () = actor {
        let! (index:int64) = mailbox.Receive()
        printfn "Printer：%d" index      
        return! loop()
    }
    loop()
let printerRef = spawn system "Printer" printerActor

// Worker Actors - Takes input from Boss and do the processing using sliding window algo and returns the completed message.
let WorkerActor (mailbox:Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive()
        printfn "worker acotr receive msg"
        let boss = mailbox.Sender()
        match message with
        | Input(startind, k, target) -> 
            // printerRef <! startind
            printfn "%d %d %d" startind k target
            boss <! Done("Completed")
        | _ -> ()

        return! loop()
    }
    loop()
             
// Boss - Takes input from command line and spawns the actor pool. Splits the tasks based on cores count and allocates using Round-Robin
let BossActor (mailbox:Actor<_>) =
    let actcount = System.Environment.ProcessorCount |> int64
    printfn "ProcessorCount: %d" actcount
    let totalactors = actcount*125L
    printfn "totalactors: %d" totalactors
    let split = totalactors*1L
    printfn "split: %d" split
    let workerActorsPool = 
            [1L .. totalactors]
            |> List.map(fun id -> spawn system (sprintf "Local_%d" id) WorkerActor)

    let workerenum = [|for lp in workerActorsPool -> lp|]
    let workerSystem = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(workerenum)))
    let mutable completed = 0L

    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with 
        | Input(n,k,t) -> 
            printfn "%d %d %d" n k t
            for id in [1L .. split] do
                workerSystem <! Input(n, k, t)
        | Done(complete) ->
            completed <- completed + 1L
            printfn $"Completed received! {completed} {split}" 
            if completed = split then
                mailbox.Context.System.Terminate() |> ignore
        | _ -> ()
       
        return! loop()
    }
    loop()

let boss = spawn system "boss" BossActor
// Input from Command Line
// let N = fsi.CommandLineArgs.[1] |> int64
// let K = fsi.CommandLineArgs.[2] |> int64
// let T = fsi.CommandLineArgs.[3] |> int64
// boss <! Input(N, K, T)
// Wait until all the actors has finished processing
system.WhenTerminated.Wait()
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