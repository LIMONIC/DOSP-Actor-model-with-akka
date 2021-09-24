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
                    port = 0
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

(*/ Print results and send them to server /*)
let postMan (mailbox:Actor<_>) =
    let mutable res = []
    let remoteServer = system.ActorSelection("akka.tcp://StringDigger@127.0.0.1:8778/user/localActor")
    // let sender = mailbox.Sender()
    let rec loop () = actor {
        let! message = mailbox.Receive()
        // printfn "worker acotr receive msg: %A" message
        let sender = mailbox.Sender()
        let printAndSend resList = 
            printfn "-------------RESULT-------------" 
            resList |> List.iter(fun (str, sha256) -> printfn $"{str}\t{sha256}")
            remoteServer <! Output(resList)
            res <- []
            printfn "--------------------------------" 
        match message with
        | Register (info) -> 
            // printfn $"Remote: send register {info}"
            let response =  (Async.RunSynchronously (remoteServer <? Register(info)))
            if response = (Register "Acknowledged") then 
                sender <! ("Acknowledged")
                printfn "[INFO] Registration Success!"
            else
                failwith "[ERROR] Unsuccessful registration!"
        | Output(resList) -> 
            if res.Length >= 100
                then 
                    res |> printAndSend
                else
                    res <- res @ resList
        | Done(completeMsg) -> 
            printfn $"[INFO][DONE]: {completeMsg}" 
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
                    outBox <! Done($"[TID: {tid}]\tNotFound")
                else 
                    outBox <! Output(res)
                    outBox <! Done($"[TID: {tid}]\tFound\t")
        | _ -> ()
        return! loop()
    }
    loop()


let remoteActor (mailbox:Actor<_>) = 
    let actcount = System.Environment.ProcessorCount |> int64
    let totalWorkers = actcount*125L
    (Async.RunSynchronously (postManRef <? Register($"RemoteStart@{System.Environment.MachineName}"))) |> ignore
    // postManRef <! Register($"RemoteStart@{System.Environment.MachineName}")

    printfn "ProcessorCount: %d" actcount
    printfn "totalWorker: %d" totalWorkers

    let workersPool = 
            [1L .. totalWorkers]
            |> List.map(fun id -> spawn system (sprintf "Local_%d" id) worker)

    let workerenum = [|for i = 1 to workersPool.Length do (sprintf "/user/Local_%d" i)|] // (workersPool |> List.mapi(fun id _ -> (sprintf "/user/workers/Local_%d" id)))
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
            // reigster remote acter
            // postManRef <! Register($"RemoteStart@{System.Environment.MachineName}")
            // task init
            let totalTasks = k - n
            let taskNum = 
                if totalTasks % taskSize = 0L then totalTasks / taskSize else totalTasks / taskSize + 1L
            // printfn "taskNum %d" taskNum
            let assignTasks (size, actors) = 
                printfn $"[DEBUG]: Task size: {size}"
                [1L..actors] |> List.iteri(fun i x -> 
                    printfn $"- Initialize actor [{i + 1}/{actors}]: \t{int64 i * size + n } - {(int64 i + 1L)* size + n - 1L}"
                    workerSystem <! Input(int64 i * size + n, size, t)
                )
            // assign tasks based on actor number
            match taskNum with
            | _ when taskNum > actorNum ->
                // resize taskSize to match actor number
                if (totalTasks % actorNum = 0L) then taskSize <- totalTasks / actorNum else taskSize <- totalTasks / actorNum + 1L
                assignTasks(taskSize, actorNum)
            | _ when taskNum = actorNum -> 
                assignTasks(taskSize, actorNum)
            | _ when taskNum < actorNum -> 
                // reduce actor numbers
                actorNum <- taskNum
                // printfn $"totalTasks: {totalTasks}  actorNum: {actorNum}"
                if totalTasks < taskSize then assignTasks(totalTasks, taskNum) else assignTasks(taskSize, taskNum)
            | _ -> failwith "[ERROR] wrong taskNum"
        | Output (res) -> 
            postManRef <! Output(res)
        | Done(completeMsg) ->
            completedWorkerNum <- completedWorkerNum + 1L
            printfn $"> {completeMsg} \tcompleted:{completedWorkerNum} \ttotal:{actorNum}" 
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
client <! TaskSize(int64 1E5)
client <! Input(N, K, T)
// Wait until all the actors has finished processing
system.WhenTerminated.Wait()