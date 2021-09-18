open System
open System.Security.Cryptography
open System.Text

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
            countHeadingZeros t cnt+1
          else cnt

/// check if an input string has target number of zeros
/// return 1 if true; 0 if false 
let validateSHAStr str target = 
    let num = countHeadingZeros (str |> Seq.toList) 0
    if num = target then 1 else 0


// Test: print out SHA256 String
let str = "7wLfA2pSBgg2e6A"
let res = generateSHA256Str str
printfn "%s" res
// Test: check if SHA256 of a string is valid
validateSHAStr res 2 |> printfn "%d"


//comment out the method of randomly generating strings
(*
//Create string random with Gator link ID as prefix
let prefix = "hongru.liu;"
let lengthOfStr = Console.ReadLine()

//transfer type of lengthOfSting from string to int
let parse (s: string) =
    match (System.Int32.TryParse(s)) with
    | (true, value) -> value
    | (false, _) -> failwith "Invalid int"
let length = parse(lengthOfStr)

//put all the element in chars
let chars = "abcdefghijklmnopqrstuvwxyz0123456789"
let charsLen = chars.Length

//function: get randomString
let randomStr = 
    let random = System.Random()
    fun len -> 
        let randomChars = [|for i in 0..len -> chars.[random.Next(charsLen)]|]
        new System.String(randomChars)

let randomString = prefix + randomStr(length)
printfn "%s" randomString


//test output
let res1 = generateSHA256Str randomString
printfn "%s" res1
// Test: check if SHA256 of a string is valid
validateSHAStr res1 2 |> printfn "%d"
*)

let prefix = "hongru.liu;"
let chars = "0123456789abcdefghigklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"

//parameters
let start = int 100000
let iteration = int 100


 //iteration to get all the strings
let seq1 =
    seq {
        for i in start..start+iteration ->
        let mutable startStr = ""
        let mutable divider = i
        let mutable reminder = 0
        while divider > 0 do
            reminder <- divider%62
            startStr <- string(chars.[reminder]) + startStr
            divider <- divider/62
        (i,prefix + startStr)
    }


//show all the index and corresponding string
for (a, astr) in seq1 do
    printfn "%d squared is %s" a astr
    

