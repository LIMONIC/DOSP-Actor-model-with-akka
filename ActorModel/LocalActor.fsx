//LocalActor.fsx
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.Remote
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor.provider = remote
            remote {
                dot-netty.tcp {
                    port = ""10010""
                    hostname = ""127.0.0.1""
                }
            }
        }")
let system = ActorSystem.Create("RemoteFSharp", configuration)
let echoClient = system.ActorSelection("akka.tcp://RemoteFSharp@127.0.0.1:9001/user/EchoServer")
let task = echoClient <? "hello echo"
let response :obj = Async.RunSynchronously (task, 1000)
printfn "Reply from remote %s" (string(response))
