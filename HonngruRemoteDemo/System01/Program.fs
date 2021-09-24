// System01 simulates a responsor
open Akka.FSharp
open Akka.Remote
open Akka.Configuration

//function start
open System
open System.Security.Cryptography
open System.Text

let STR_PREFIX = "hongru.liu;"
let STR_DICT = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"


/// Remove certain Character from string
let removeChar (stripChars:string) (text:string) =
    text.Split(stripChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries) |> String.Concat

/// Generate SHA256 of input string
let generateSHA256Str (inputText: string) = 
    inputText
    |> Encoding.ASCII.GetBytes
    |> (new SHA256Managed()).ComputeHash
    |> System.BitConverter.ToString
    |> removeChar "-"

/// count heading zeros of input string
/// Use recursive
/// Use parrten matcher for conditional judgement
/// [] -> Recursive exit: when str become empty, return result
/// h::t -> seperate a string to head and tail parts
///     for example: a string "abcd" can be seperated into "a" and "bcd"
let rec countHeadingZeros str cnt =  
    match str with
        | [] -> cnt
        | h::t -> 
          if h='0' then 
            countHeadingZeros t cnt+1L
          else cnt

/// check if an input string has target number of zeros
/// return 1 if true; 0 if false 
let validateSHAStr str target = 
    let num = countHeadingZeros (str |> Seq.toList) 0L
    num = target

/// generate string
/// decimal to 62-decimal 
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

/// get result
let getValidStr (start: int64, iteration: int64, zeros: int64) = 
    seq {
        for i = 0L to iteration do
            let str = (start + i |> decimalToStr)
            let sha256Str = (str |> generateSHA256Str)
            if (validateSHAStr sha256Str zeros) then (str, sha256Str)
    }
    |> Seq.toList
// function end

let configuration = ConfigurationFactory.ParseString(@"
        akka {
            actor.provider = remote
            remote {
                dot-netty.tcp {
                    port = 9090
                    hostname = localhost
                }
            }
        }
    ")

// Akka System
let sys01 = System.create "sys01" (configuration)


//type of message
type Information = 
    | TaskSize of (int64)
    | Input of (int64*int64*int64)
    | Output of (list<string * string>)
    | Done of (string)
    
    
// actor
let smith  = 
    spawn sys01 "smith"
        (fun mailbox ->
            let rec loop() = actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match box message with
                | :? int64  ->
                    printfn "message received"
                    //set start number as 0, number of zeros looking for as 4
                    let res = getValidStr (0L, message, 4L)
                    if res.IsEmpty 
                        then 
                            printfn "smith not found"
                            sender <! "smith not found"
                        else 
                            let printRes resList = 
                                resList |> List.iter(fun (str, sha256) -> printfn $"{str}\t{sha256}")
                            res |> printRes
                            printfn "smith found"
                            sender <! "Smith found string"
                | _ -> failwith "unknown error"
            } 
            loop())

[<EntryPoint>]
let main argv =
    // simulating a responsor, just waiting
    
    System.Console.Read() |> ignore
    0




