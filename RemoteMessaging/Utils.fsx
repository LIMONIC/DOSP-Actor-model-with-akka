module Utils

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
    // |> printfn "%A" //show all the index and corresponding string

 /// print result
let printRes targetZeros =
    let iterations = (100 * (pown 10 targetZeros)) |> int64
    printfn "%d" iterations
    getValidStr (0L, iterations, targetZeros |> int64)
    |> List.iter(fun (str, sha256) -> printfn $"{str}\t{sha256}")

let targetZeros = fsi.CommandLineArgs.[1] |> int
// printRes targetZeros


