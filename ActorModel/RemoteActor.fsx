#time "on"
#load "./Utils.fsx"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit
open Akka.Remote
open Utils

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    port = 0
                    hostname = 127.0.0.1
                }
            }
        }")

let system = ActorSystem.Create("StringDigger", configuration)

(*/ Union for actor messages /*)
type Information = 
    | TaskSize of (int64)
    | Input of (int64*int64*int64)
    | Output of (list<string * string>)
    | Done of (string)

(*/ Print results and send them to server /*)
let postMan (mailbox:Actor<_>) =
    let mutable res = []
    let rec loop () = actor {
        let! message = mailbox.Receive()
        // printfn "worker acotr receive msg: %A" message
        let remoteServer = system.ActorSelection("akka.tcp://StringDigger@127.0.0.1:8778/user/server")

        let printAndSend resList = 
            printfn "-------------RESULT-------------" 
            resList |> List.iter(fun (str, sha256) -> printfn $"{str}\t{sha256}")
            remoteServer <! Output(resList)
            res <- []
            printfn "--------------------------------" 
        match message with
        | Output(resList) -> 
            if res.Length >= 100
                then 
                    res |> printAndSend
                else
                    res <- res @ resList
        | Done(completeMsg) -> 
            printfn "worker acotr receive msg: %A" completeMsg
            if res.Length > 0 then printAndSend res
            remoteServer <! Done(completeMsg)
        | _ -> ()
        return! loop()
    }
    loop()
let postManRef = spawn system "PostMan" postMan

(*/ Worker Actors
    Takes input from remoteActor, calculate results and pass the result to PostMan Actor
 /*)
let worker (mailbox:Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive()
        // printfn "worker acotr receive msg: %A" message
        let boss = mailbox.Sender()
        match message with
        | Input(start, k, zeros) -> 
            // printerRef <! startind
            // printfn "Starting working with %d %d %d" start k zeros
            let res = getValidStr (start, k, zeros)
            // printfn "%A %A" res.IsEmpty res
            if res.IsEmpty then boss <! Done("NotFound")
                else boss <! Done("Found")
                     postManRef <! Output(res)
        | _ -> ()
        return! loop()
    }
    loop()
 

let remoteActor (mailbox:Actor<_>) = 
    let actcount = System.Environment.ProcessorCount |> int64
    let totalWorkers = actcount*125L

    printfn "ProcessorCount: %d" actcount
    printfn "totalWorker: %d" totalWorkers

    let workersPool = 
            [1L .. totalWorkers]
            |> List.map(fun id -> spawn system (sprintf "Local_%d" id) worker)

    let workerenum = [|for lp in workersPool -> lp|]
    let workerSystem = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(workerenum)))
    let mutable completedWorkerNum = 0L
    let mutable actorNum = totalWorkers
    let mutable taskSize = 1E6 |> int64

// Assign tasks to worker
    let rec loop () = actor {
        let! message = mailbox.Receive()
        // printfn $"Boss received {message}"
        match message with 
        | TaskSize(size) -> taskSize <- size
        | Input(n,k,t) -> 
            // task init
            let totalTasks = k - n
            let taskNum = 
                if totalTasks % taskSize = 0L then totalTasks / taskSize else totalTasks / taskSize + 1L
            // printfn "taskNum %d" taskNum
            let assignTasks (size, actors) = 
                [1L..actors] |> List.iteri(fun i x -> 
                    printfn $"Initialize No.{i} actor of {actors}: {int64 i * size} {int64 i * size + size - 1L}"
                    workerSystem <! Input(int64 i * size, int64 i * size + size - 1L, t)
                )
            // assign tasks based on actor number
            match taskNum with
            | _ when taskNum > actorNum ->
                // resize taskSize to match actor number
                taskSize <- if totalTasks % actorNum = 0L then totalTasks / actorNum else totalTasks / actorNum + 1L
                assignTasks(taskSize, actorNum)
            | _ when taskNum = actorNum -> 
                assignTasks(taskSize, actorNum)
            | _ when taskNum < actorNum -> 
                // reduce actor numbers
                actorNum <- taskNum
                assignTasks(taskSize, taskNum)
            | _ -> failwith "wrong taskNum"
        | Done(completeMsg) ->
            completedWorkerNum <- completedWorkerNum + 1L
            printfn $"Complete msg: {completeMsg}! \tcompleted:{completedWorkerNum} \ttotal:{actorNum}" 
            if completedWorkerNum = actorNum then
                postManRef <! Done($"RemoteDone@{System.Environment.MachineName}")
                mailbox.Context.System.Terminate() |> ignore
        | _ -> ()

        return! loop()
    }
    loop()

let client = spawn system "remoteActor" remoteActor
// Input from Command Line
let N = fsi.CommandLineArgs.[1] |> int64
let K = fsi.CommandLineArgs.[2] |> int64
let T = fsi.CommandLineArgs.[3] |> int64
// client <! TaskSize(int64 1E5)
client <! Input(N, K, T)
// Wait until all the actors has finished processing
system.WhenTerminated.Wait()