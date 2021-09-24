// System02 simulates a requester
open Akka.FSharp
open Akka.Remote
open Akka.Configuration

let configuration = ConfigurationFactory.ParseString(@"
        akka {
            actor.provider = remote
            remote {
                dot-netty.tcp {
                    port = 9091
                    hostname = localhost
                }
            }
        }
    ")
//type of message
type Information = 
    | TaskSize of (int64)
    | Input of (int64*int64*int64)
    | Output of (list<string * string>)
    | Done of (string)
    
    
// Akka System
let sys02 = System.create "sys02" (configuration)

// actor
let smith = sys02.ActorSelection("akka.tcp://sys01@localhost:9090/user/smith")

[<EntryPoint>]
let main argv =
    System.Threading.Thread.Sleep(10000) // wait for System01 starting

    let task = smith <? 1000000L

    let response = Async.RunSynchronously(task, 30000)

    printfn "response info: %s" (string(response))

    System.Console.Read() |> ignore
    0 // return an integer exit code