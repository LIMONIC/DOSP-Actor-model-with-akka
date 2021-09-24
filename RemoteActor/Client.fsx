#time "on"
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
                    transport-protocol = tcp
                    port = 9200
                    hostname = ""192.168.1.22""
                }
            }
        }"
        )

let remoteSystem = System.create "RemoteStringDigger" (configuration)

type Information = 
    | TaskSize of (int64)
    | Input of (int64*int64*int64)
    | Output of (list<string * string>)
    | Done of (string)
    | ServerInfo of (string)
    | ClientInfo of (int64)

let coordinator (mailbox:Actor<_>) =
    let actcount = System.Environment.ProcessorCount |> int64
    printfn $"[INFO]: ProcessorCount: {actcount}"
    let rec loop () = actor {
        let! (message) = mailbox.Receive()
        printfn "worker acotr receive msg: %A" message
        let sender = mailbox.Sender()
        match message with
        | ServerInfo (info) -> 
            sender <! ClientInfo(actcount)
        | _ -> ()
        return! loop()
    }
    loop()


spawn remoteSystem "coordinator" coordinator
System.Console.Title <- "Remote : " + System.Diagnostics.Process.GetCurrentProcess().Id.ToString()
Console.ForegroundColor <- ConsoleColor.Green
printfn "Remote Actor %s listening..." remoteSystem.Name
System.Console.ReadLine() |> ignore
0
// remoteSystem.Terminate().Wait()
// Console.ReadLine() |> ignore