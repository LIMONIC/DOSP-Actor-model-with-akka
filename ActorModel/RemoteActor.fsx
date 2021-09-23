// RemoteActor.fsx
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.Remote
let config =
    Configuration.parse
        @"akka {
            actor.provider = remote
            dot-netty.tcp {
                hostname = ""127.0.0.1""
                port = 9001
            }
        }"
let system = System.create "RemoteFSharp" config
let echoServer = 
    spawn system "EchoServer"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                
                match box message with
                | :? string -> 
                    printfn "echoServer called"
                    sender <! sprintf "Echo: %s" message
                    return! loop()
                | _ ->  failwith "Unknown message"
            }
        loop()
Console.ReadLine() |> ignore
