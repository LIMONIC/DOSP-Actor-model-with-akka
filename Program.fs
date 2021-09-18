open System
open System.Security.Cryptography
open System.Text

let ENCODING_STR = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"

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

/// generate string
// decimal to 62-decimal 
let decimalToStr (decimal: int) = 
    let num = ref decimal
    let getQuotient x = x / 62
    let getRem x = x % 62
    let resArr = ref ""

    if !num = 0 then resArr := "0"
    while (!num) <> 0 do
        let r = ref (getRem !num)
        resArr := string ENCODING_STR.[!r] + !resArr
        num := getQuotient !num
    "tianyu.zhang;" + !resArr


/// iterate string


// worker main logic: iterate string and find valid ones
let worker (start, iteration, zeros) = 
    // convert start number to string
    // let startStr = start |> decimalTo62
    for i = 0 to iteration do
        let str = (i |> decimalToStr)
        printfn "%s" str
        // validateSHAStr (str |> generateSHA256Str) zeros
    // printfn ""

worker (0, 100, 10)

/// **************TEST*****************
// // Test: print out SHA256 String
// let str = "7wLfA2pSBgg2e6A"
// let res = generateSHA256Str str
// printfn "%s" res
// // Test: check if SHA256 of a string is valid
// validateSHAStr res 2 |> printfn "%d"
/// ***********************************

