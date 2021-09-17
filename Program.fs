open System
open System.Security.Cryptography
open System.Text

let stripv3 (stripChars:string) (text:string) =
    text.Split(stripChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries) |> String.Concat

let generateSHA256Str (inputText: string) = 
    inputText
    |> Encoding.ASCII.GetBytes
    |> (new SHA256Managed()).ComputeHash
    |> System.BitConverter.ToString
    |> stripv3 "-"


let rec countHeadingZeros str cnt =  
    match str with
        | [] -> cnt
        | h::t -> 
          if h='0' then 
            countHeadingZeros t cnt+1
          else cnt

let validateSHAStr str target = 
    let num = countHeadingZeros (str |> Seq.toList) 0
    if num = target then 1 else 0

// Test: print out SHA256 String
let str = "7wLfA2pSBgg2e6A"
let res = generateSHA256Str str
printfn "%s" res
// Test: check if SHA256 of a string is valid
validateSHAStr res 2 |> printfn "%d"