#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
#r "nuget: Akka.Serialization.Hyperion"

open System
open Akka.FSharp
open Akka.Actor
open Akka.Configuration
open System.Security.Cryptography
open System.Text
open Akka.TestKit
open Akka.Remote
open Akka.Serialization


let config =
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
            remote.helios.tcp {
                hostname = ""192.168.1.22""
                port = 9100
            }
        }"
    )
    
let remoteSystemAddressList = ["akka.tcp://RemoteStringDigger@192.168.1.22:9200"; 
                               "akka.tcp://RemoteStringDigger@192.168.1.22:9201"; 
                               "akka.tcp://RemoteStringDigger@192.168.1.22:9202"]
let system = System.create "StringDigger" config

(*/ Union for actor messages /*)
type Information = 
    | TaskSize of (int64)
    | Input of (int64*int64*int64)
    | Output of (list<string * string>)
    | Done of (string)
    | ServerInfo of (string)
    | ClientInfo of (int64)

type RemoteInfo = {
    RemotePath: Address
    ProcessorNum: int64
}

(***********************Utils**************************)
// This block of code takes <start:integer> <itration:integer><zeros:integer> as input
// It reuturns a List<string*string> contans only the valid strings that have designated number of 0's and its SHA256 string
let STR_PREFIX = "hongru.liu;"
let STR_DICT = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
let removeChar (stripChars:string) (text:string) =
    text.Split(stripChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries) |> String.Concat
let generateSHA256Str (inputText: string) = 
    inputText
    |> Encoding.ASCII.GetBytes
    |> (new SHA256Managed()).ComputeHash
    |> System.BitConverter.ToString
    |> removeChar "-"
let rec countHeadingZeros str cnt =  
    match str with
        | [] -> cnt
        | h::t -> 
          if h='0' then 
            countHeadingZeros t cnt+1L
          else cnt
let validateSHAStr str target = 
    let num = countHeadingZeros (str |> Seq.toList) 0L
    num = target
let decimalToStr (decimal: int64) = 
    let num = ref decimal
    let resArr = ref ""
    let getQuotient x = x / 62L
    let getRem x = x % 62L
    if !num = 0L then resArr := "0"
    while (!num) <> 0L do
        let r = ref (getRem !num)
        resArr := string STR_DICT.[!r |> int] + !resArr
        num := getQuotient !num
    STR_PREFIX + !resArr
let getValidStrLocal (start: int64, iteration: int64, zeros: int64) = 
    seq {
        for i = 0L to iteration do
            let str = (start + i |> decimalToStr)
            let sha256Str = (str |> generateSHA256Str)
            if (validateSHAStr sha256Str zeros) then (str, sha256Str)
    }
    |> Seq.toList
(******************************************************)


let getRemoteInfo pathList = 
    let checkPath path = ActorPath.TryParseAddress path 
    // pathList<string> -> recordList<path, processors>
    pathList 
    |> List.map(checkPath)
    |> List.filter(fun (isValid, _) -> isValid)
    |> List.map(fun (_, path) -> 
        let remoteServer = system.ActorSelection (sprintf $"{path}/user/coordinator") //system
        printfn "sending msg %A" (remoteServer)
        let response = (Async.RunSynchronously (remoteServer <? ServerInfo("hello")))
        match response with 
        | ClientInfo(pNum) -> 
            { RemotePath = path; ProcessorNum = pNum }
        | _ -> failwith $"[ERROR]: Remote {path} not response!"
    )

let deployRemoteSystem remoteInfoList =
    remoteInfoList
    |> List.iter(fun {RemotePath = path; ProcessorNum = _} -> Deploy(RemoteScope(path)) |> ignore)

(* Remote deployment
    systemOrContext: system name. Use same name with local system to have location transperency
*)
let spawnRemote systemOrContext remoteSystemAddress actorName expr =
    spawne systemOrContext actorName expr [SpawnOption.Deploy (Deploy(RemoteScope (Address.Parse remoteSystemAddress)))]


(*/ Print results/*)
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

let localWorker (mailbox:Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive()
        // printfn "worker acotr receive msg: %A" message
        let outBox = mailbox.ActorSelection("akka.tcp://StringDigger@192.168.1.22:9100/user/coordinator")
        let tid = Threading.Thread.CurrentThread.ManagedThreadId
        match message with
        | Input(start, k, zeros) -> 
            // printerRef <! startind
            // printfn "Starting working with %d %d %d" start k zeros
            let res = getValidStrLocal (start, k, zeros)
            // printfn $"[Local][INFO]: Task received!\t{start}\t-\t{start + k - 1L}"
            if res.IsEmpty 
                then 
                    let notFoundStr = $"[Local][TID: {tid}]\tNotFound\t@\t{start} - {start + k - 1L}"
                    printerRef <! Done(notFoundStr)
                    outBox <! Done(notFoundStr)
                else 
                    let foundStr = $"[Local][TID: {tid}]\tFound\t@\t{start} - {start + k - 1L}"
                    printerRef <! Output(res)
                    printerRef <! Done(foundStr)
                    outBox <! Done(foundStr)
        | _ -> ()
        return! loop()
    }
    loop()

let actorRef address id = 
    printfn $"[INFO]: Spawn remote actor id: {id} @ {address}"
    spawnRemote system address (sprintf $"Remote_{id}") 
        <@
            fun mailbox ->
                let rec loop (): Cont<Information, unit> = 
                    actor {
                        let! message = mailbox.Receive()
                        let printer = mailbox.ActorSelection("akka.tcp://StringDigger@192.168.1.22:9100/user/printer")
                        let outBox = mailbox.ActorSelection("akka.tcp://StringDigger@192.168.1.22:9100/user/coordinator")
                        let tid = Threading.Thread.CurrentThread.ManagedThreadId
                        (***********************Utils**************************)
                        let STR_PREFIX = "hongru.liu;"
                        let STR_DICT = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
                        let removeChar (stripChars:string) (text:string) =
                            text.Split(stripChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries) |> String.Concat
                        let generateSHA256Str (inputText: string) = 
                            inputText
                            |> Encoding.ASCII.GetBytes
                            |> (new SHA256Managed()).ComputeHash
                            |> System.BitConverter.ToString
                            |> removeChar "-"
                        let rec countHeadingZeros str cnt =  
                            match str with
                                | [] -> cnt
                                | h::t -> 
                                  if h='0' then 
                                    countHeadingZeros t cnt+1L
                                  else cnt
                        let validateSHAStr str target = 
                            let num = countHeadingZeros (str |> Seq.toList) 0L
                            num = target
                        let decimalToStr (decimal: int64) = 
                            let num = ref decimal
                            let resArr = ref ""
                            let getQuotient x = x / 62L
                            let getRem x = x % 62L
                            if !num = 0L then resArr := "0"
                            while (!num) <> 0L do
                                let r = ref (getRem !num)
                                resArr := string STR_DICT.[!r |> int] + !resArr
                                num := getQuotient !num
                            STR_PREFIX + !resArr
                        let getValidStr (start: int64, iteration: int64, zeros: int64) = 
                            seq {
                                for i = 0L to iteration do
                                    let str = (start + i |> decimalToStr)
                                    let sha256Str = (str |> generateSHA256Str)
                                    if (validateSHAStr sha256Str zeros) then (str, sha256Str)
                            }
                            |> Seq.toList
                        (******************************************************)
                        match message with
                        | Input(start, k, zeros) -> 
                            // printfn $"[Remote][INFO]: Task received!\t{start}\t-\t{start + k - 1L}"
                            let res = getValidStr (start, k, zeros)
                            if res.IsEmpty 
                                then 
                                    let notFoundStr = $"[Remote][TID: {tid}]\tNotFound\t@\t{start} - {start + k - 1L}"
                                    printfn $"{notFoundStr}"
                                    printer <! Done(notFoundStr)
                                    outBox <! Done(notFoundStr)
                                else 
                                    let foundStr = $"[Remote][TID: {tid}]\tFound\t@\t{start} - {start + k - 1L}"
                                    printer <! Output(res)
                                    printfn $"{foundStr}"
                                    printer <! (foundStr)
                                    outBox <! Done(foundStr)
                        | _ -> ()
                        return! loop()
                    }   
                loop()
        @>


let coordinator (mailbox:Actor<_>) = 
    let actcount = System.Environment.ProcessorCount |> int64
    let processorToWokerRatio = 1L
    let totLocWorkers = actcount * processorToWokerRatio
    // get remote info
    let remoteInfoList = remoteSystemAddressList |> getRemoteInfo
    // depoly remote system
    remoteInfoList |> deployRemoteSystem 

    let getTotRemWorkerNum list = 
        let mutable sum = 0L
        list |> List.iter(fun {RemotePath = _; ProcessorNum = num} -> sum <- sum + num)
        sum * processorToWokerRatio
    let totRomWorkers = getTotRemWorkerNum remoteInfoList

    let mutable remoteActorList = []
    
    // Randemly spawn remote actors
    let getRemoteActorList recordList= 
        let rnd = System.Random ()
        let idList = [1L..totRomWorkers] |> List.sortBy(fun _ -> rnd.Next(1, int totRomWorkers) |> int64 )
        let idx = ref 0;
        recordList
        |> List.iter(fun({RemotePath = path; ProcessorNum = num}) -> 
            for _ = 0 to int (num * processorToWokerRatio) - 1 do 
                remoteActorList <- ((actorRef (string path) idList.[!idx]) :: remoteActorList)
                incr idx
        )
    getRemoteActorList remoteInfoList
    printfn $"[INFO]: totLocWorkers: {totLocWorkers}; totRomWorkers: {totRomWorkers}"
    let localWorkerPool = 
        [1L .. totLocWorkers]
        |> List.map(fun id -> spawn system (sprintf "Local_%d" id) localWorker)
    let localPathArr = [|for i = 1 to localWorkerPool.Length do (sprintf "/user/Local_%d" i)|]
    // let localWorkerCluster = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(localPathArr)))
    let remoteWorkerPool = 
        [1L .. totRomWorkers]
        |> List.map(fun id -> remoteActorList.[int id - 1]) // spawn system (sprintf "Remote_%d" id)
    let remotePathArr = [|for i = 1 to remoteWorkerPool.Length do (sprintf "/user/Remote_%d" i)|]
    // let remoteWorkerCluster = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(remotePathArr)))

    let pathArr = Array.concat [remotePathArr; localPathArr]
    let workerCluster = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(pathArr)))

    let mutable completedWorkerNum = 0L
    let mutable totalActorNum = totLocWorkers + totRomWorkers
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
            let requiredActorNum = 
                if totalTasks % taskSize = 0L then totalTasks / taskSize else totalTasks / taskSize + 1L

            let assignTasks (size, actors) = 
                printfn $"[DEBUG]: Task size: {size}"
                [1L..actors] |> List.iteri(fun i x -> 
                    printfn $"- Initialize actor [{i + 1}/{actors}]: \t{int64 i * size + n} - {(int64 i + 1L)* size + n - 1L}"
                    workerCluster <! Input(int64 i * size + n, size, t)
                )

            // assign tasks based on actor number
            match requiredActorNum with
            | _ when requiredActorNum > totalActorNum ->
                // resize taskSize to match actor number
                if (totalTasks % totalActorNum = 0L) then taskSize <- totalTasks / totalActorNum else taskSize <- totalTasks / totalActorNum + 1L
                assignTasks(taskSize, totalActorNum)
            | _ when requiredActorNum = totalActorNum -> 
                assignTasks(taskSize, totalActorNum)
            | _ when requiredActorNum < totalActorNum -> 
                // reduce actor numbers
                totalActorNum <- requiredActorNum
                // printfn $"totalTasks: {totalTasks}  actorNum: {localActorNum}"
                if totalTasks < taskSize then assignTasks(totalTasks, requiredActorNum) else assignTasks(taskSize, requiredActorNum)
            | _ -> failwith "[ERROR] wrong taskNum"
            // printfn "End Input"
        | Done(completeMsg) ->
            completedWorkerNum <- completedWorkerNum + 1L
            // printfn $"[DEBUG]: completedWorkerNum: {completedWorkerNum}"
            if completedWorkerNum = totalActorNum then
                printerRef <! Done($"[INFO]: All tasks completed! {totalActorNum}")
                mailbox.Context.System.Terminate() |> ignore
        | _ -> ()
        return! loop()
    }
    loop()

let client = spawn system "coordinator" coordinator
// Input from Command Line
let N = fsi.CommandLineArgs.[1] |> int64
let K = fsi.CommandLineArgs.[2] |> int64
let T = fsi.CommandLineArgs.[3] |> int64
client <! TaskSize(int64 1E7)
client <! Input(N, K, T)

Console.ReadLine() |> ignore
system.WhenTerminated.Wait()


