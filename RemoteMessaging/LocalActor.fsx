#time "on"
#load "./Utils.fsx"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
#r "nuget: Akka.Serialization.Hyperion"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit
open Akka.Remote
open Akka.Serialization
open Utils

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                serializers {
                    hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
                }
                serialization-bindings {
                    ""System.Object"" = hyperion
                } 
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    port = 8778
                    hostname = ""127.0.0.1""
                }
            }
        }")

let system = ActorSystem.Create("StringDigger", configuration)

(*/ Union for actor messages /*)
type Information = 
    | TaskSize of (int64)
    | Input of (int64*int64*int64)
    | Output of (list<string * string>)
    | Register of (string)
    | Done of (string)

let receiver (mailbox:Actor<_>) =
    printfn "receiver on!"
    let rec loop () = actor {
        let! (message) = mailbox.Receive()
        // let sender = mailbox.Sender()
        // printfn $"Remote actor registired! info: {message}"
        // sender <! sprintf "Echo: received"
        // if (message :? string) then
        //     printfn $"Remote actor registired! info: {message}"
        // sender <! sprintf "Echo: %A" message
        match message with
        | Register(info) -> 
            printfn $"register info: {info}"
            // sender <! sprintf "Echo: %s" info
        | _ -> ()
        return! loop()
    }
    loop()

(*/ Print results and send them to server /*)
let printer (mailbox:Actor<_>) =
    let mutable res = []
    let rec loop () = actor {
        let! message = mailbox.Receive()
        // printfn "worker acotr receive msg: %A" message
        let printRes resList = 
            printfn "-------------RESULT-------------" 
            resList |> List.iter(fun (str, sha256) -> printfn $"{str}\t{sha256}")
            res <- []
            printfn "--------------------------------" 
        match message with
        | Output(resList) -> 
            if res.Length >= 100
                then 
                    res |> printRes
                else
                    res <- res @ resList
        | Done(completeMsg) -> 
            printfn $"[INFO][DONE]: {completeMsg}"
            if res.Length > 0 then printRes res
        | _ -> ()
        return! loop()
    }
    loop()
let printerRef = spawn system "printer" printer

(*/ Worker Actors
    Takes input from remoteActor, calculate results and pass the result to PostMan Actor
 /*)
let worker (mailbox:Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive()
        // printfn "worker acotr receive msg: %A" message
        let outBox = mailbox.Sender()
        let tid = Threading.Thread.CurrentThread.ManagedThreadId
        match message with
        | Input(start, k, zeros) -> 
            // printerRef <! startind
            // printfn "Starting working with %d %d %d" start k zeros
            let res = getValidStr (start, k, zeros)
            // printfn "%A %A" res.IsEmpty res
            if res.IsEmpty 
                then 
                    outBox <! Done($"[TID: {tid}]\tNotFound!")
                else 
                    outBox <! Output(res)
                    outBox <! Done($"[TID: {tid}]\tFound!\t")
        | _ -> ()
        return! loop()
    }
    loop()
 

let localActor (mailbox:Actor<_>) = 
    let actcount = System.Environment.ProcessorCount |> int64
    let totalWorkers = actcount*125L

    printfn "ProcessorCount: %d" actcount
    printfn "totalWorker: %d" totalWorkers

    let workersPool = 
            [1L .. totalWorkers]
            |> List.map(fun id -> spawn system (sprintf "Local_%d" id) worker)

    let workerenum = [|for i = 1 to workersPool.Length do (sprintf "/user/Local_%d" i)|]
    let workerSystem = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(workerenum)))
    let mutable completedLocalWorkerNum = 0L
    let mutable completedRemoteWorkerNum = 0L
    let mutable localActorNum = totalWorkers
    let mutable remoteActorNum = 0L
    let mutable taskSize = 1E6 |> int64

// Assign tasks to worker
    let rec loop () = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        // printfn $"Boss received {message}"
        match message with 
        | TaskSize(size) -> taskSize <- size
        | Register info -> 
            remoteActorNum <- remoteActorNum + 1L
            printfn $"[INFO]: Remoter actor No.: {remoteActorNum}"
            sender <! Register "Acknowledged"
        | Input(n,k,t) -> 
            // task init
            let totalTasks = k - n
            let requiredActorNum = 
                if totalTasks % taskSize = 0L then totalTasks / taskSize else totalTasks / taskSize + 1L
            // printfn "taskNum %d" taskNum
            let assignTasks (size, actors) = 
                printfn $"[DEBUG]: Task size: {size}"
                [1L..actors] |> List.iteri(fun i x -> 
                    printfn $"- Initialize actor [{i + 1}/{actors}]: \t{int64 i * size + n} - {(int64 i + 1L)* size + n - 1L}"
                    workerSystem <! Input(int64 i * size + n, size, t)
                )
            // assign tasks based on actor number
            match requiredActorNum with
            | _ when requiredActorNum > localActorNum ->
                // resize taskSize to match actor number
                if (totalTasks % localActorNum = 0L) then taskSize <- totalTasks / localActorNum else taskSize <- totalTasks / localActorNum + 1L
                assignTasks(taskSize, localActorNum)
            | _ when requiredActorNum = localActorNum -> 
                assignTasks(taskSize, localActorNum)
            | _ when requiredActorNum < localActorNum -> 
                // reduce actor numbers
                localActorNum <- requiredActorNum
                // printfn $"totalTasks: {totalTasks}  actorNum: {localActorNum}"
                if totalTasks < taskSize then assignTasks(totalTasks, requiredActorNum) else assignTasks(taskSize, requiredActorNum)
            | _ -> failwith "[ERROR] wrong taskNum"
            // printfn "End Input"
        | Output (res) -> 
            printerRef <! Output(res)
        | Done(completeMsg) ->
            let containPrefix (p:string) (s:string) = s.StartsWith(p)
            if completeMsg |> containPrefix "RemoteDone"
                then
                    completedRemoteWorkerNum <- completedRemoteWorkerNum + 1L
                    printfn $"> {completeMsg} \tcompleted:{completedRemoteWorkerNum} \ttotal:{remoteActorNum}" 
                else
                    completedLocalWorkerNum <- completedLocalWorkerNum + 1L
                    printfn $"> {completeMsg} \tcompleted:{completedLocalWorkerNum} \ttotal:{localActorNum}" 
            if completedLocalWorkerNum = localActorNum && completedRemoteWorkerNum = remoteActorNum then
                printerRef <! Done($"All tasks completed! local: {completedLocalWorkerNum}, remote: {completedRemoteWorkerNum}")
                mailbox.Context.System.Terminate() |> ignore
        // | _ -> ()
        return! loop()
    }
    loop()

let client = spawn system "localActor" localActor
// spawn system "localActor" localActor
// spawn system "receiver" receiver
// system.ActorOf(Props.Empty.WithRouter(new Akka.Routing.RoundRobinGroup("/user/receive")), "StringDigger");
// Input from Command Line
let N = fsi.CommandLineArgs.[1] |> int64
let K = fsi.CommandLineArgs.[2] |> int64
let T = fsi.CommandLineArgs.[3] |> int64
// client <! TaskSize(int64 2.5E6)
client <! Input(N, K, T)
// Wait until all the actors has finished processing
system.WhenTerminated.Wait()