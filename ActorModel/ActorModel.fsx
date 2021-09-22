#time "on"
#load "./Utils.fsx"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Utils
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("StringDigger", Configuration.defaultConfig())
// Use Actor system for naming
// let system = System.create "my-system" (Configuration.load())
type Information = 
    | Input of (int64*int64*int64)
    | Output of (list<string * string>)
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
        // receiving the message sent to the actor
        let! message = mailbox.Receive()
        // handle message
        printfn "worker acotr receive msg: %A" message
        let boss = mailbox.Sender()
        let tid = Threading.Thread.CurrentThread.ManagedThreadId
        match message with
        | Input(start, k, zeros) -> 
            // printerRef <! startind
            printfn "Start working with param %d %d %d at tid #%d" start k zeros tid
            let res = getValidStr (start, k, zeros)
            printfn "%A %A" res.IsEmpty res
            if res.IsEmpty then boss <! Done("NotFound")
                else boss <! Done("Found")
                     boss <! Output(res)
        | _ -> ()
        return! loop()
    }
    loop()

// Boss - Takes input from command line and spawns the actor pool. Splits the tasks based on cores count and allocates using Round-Robin
let BossActor (mailbox:Actor<_>) =
    let actcount = System.Environment.ProcessorCount |> int64
    printfn "ProcessorCount: %d" actcount
    let totalActors = actcount//*125L
    printfn "totalactors: %d" totalActors

    
    // let split = totalactors*2L
    // printfn "split: %d" split
    let workerActorsPool = 
            [1L .. totalActors]
            |> List.map(fun id -> spawn system (sprintf "Local_%d" id) WorkerActor)

    let workerenum = [|for lp in workerActorsPool -> lp|]
    let workerSystem = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(workerenum)))
    // let workerProps = Props.Create(for i in workerenum do i).WithRouter(Akka.Routing.RoundRobinPool(int totalActors))
    // let workerSystem = system.ActorOf(workerProps, "worker")

    let mutable completed = 0L
    let mutable actorNum = totalActors

    let rec loop () = actor {
        let! message = mailbox.Receive()
        printfn $"Boss received {message}"
        match message with 
        | Input(n,k,t) -> 
            // task init\
            let mutable taskSize = 1E5 |> int64

            let totalTasks = k - n
            let taskNum = 
                if totalTasks % taskSize = 0L then totalTasks / taskSize else totalTasks / taskSize + 1L
            // printfn "taskNum %d" taskNum
            let assignTasks (size, actors) = 
                [1L..actors] |> List.iteri(fun i x -> 
                    printfn $"Initialize No.{i} actor of {actors}: {int64 i * size + n} {int64 i * (size + 1L) + n - 1L}"
                    workerSystem <! Input(int64 i * size + n, int64 i * (size + 1L) + n - 1L, t)
                )
            // assign tasks based on actornumber
            match taskNum with
            | _ when taskNum > actorNum ->
                // resize taskSize to match actor number
                taskSize <- if totalTasks % actorNum = 0L then totalTasks / actorNum else totalTasks / actorNum + 1L
                assignTasks(taskSize, actorNum)
            | _ when taskNum = actorNum -> 
                assignTasks(taskSize, actorNum)
            | _ when taskNum < actorNum -> 
                // recalculate actor numbers
                actorNum <- taskNum
                assignTasks(taskSize, taskNum)
                // [1L..(actorNum - taskNum + 1L)] |> List.iter(fun x -> workerSystem <! Input(0L, 0L, t))
            | _ -> failwith "wrong taskNum"
        | Done(completeMsg) ->
            completed <- completed + 1L
            printfn $"Complete msg: {completeMsg}! \tcompleted:{completed} \ttotal:{actorNum}" 
            if completed = actorNum then
                mailbox.Context.System.Terminate() |> ignore
        | _ -> ()
       
        return! loop()
    }
    loop()

let boss = spawn system "server" BossActor
// Input from Command Line
let N = fsi.CommandLineArgs.[1] |> int64
let K = fsi.CommandLineArgs.[2] |> int64
let T = fsi.CommandLineArgs.[3] |> int64
boss <! Input(N, K, T)
// Wait until all the actors has finished processing
system.WhenTerminated.Wait()