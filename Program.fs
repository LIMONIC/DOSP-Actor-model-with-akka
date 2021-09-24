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
            countHeadingZeros t cnt+1
          else cnt

/// check if an input string has target number of zeros
/// return 1 if true; 0 if false 
let validateSHAStr str target = 
    let num = countHeadingZeros (str |> Seq.toList) 0
    if num = target then true else false

/// generate string
/// decimal to 62-decimal 
let decimalToStr (decimal: int) = 
    let num = ref decimal
    let resArr = ref ""
    let getQuotient x = x / 62
    let getRem x = x % 62

    if !num = 0 then resArr := "0"
    while (!num) <> 0 do
        let r = ref (getRem !num)
        resArr := string STR_DICT.[!r] + !resArr
        num := getQuotient !num
    STR_PREFIX + !resArr

let worker (start, iteration, zeros) = 
    seq {
        for i = 0 to iteration do
            let str = (start + i |> decimalToStr)
            let sha256Str = (str |> generateSHA256Str)
            if (validateSHAStr sha256Str zeros) then (str, sha256Str)
    }
    |> Seq.toList
    |> printfn "%A" //show all the index and corresponding string

worker (0, 1000000, 4)



//  //iteration to get all the strings
// let seq1 =
//     seq {
//         for i in 0..0+1000 ->
//         let mutable startStr = ""
//         let mutable divider = i
//         let mutable reminder = 0
//         while divider > 0 do
//             reminder <- divider%62
//             startStr <- string(STR_DICT.[reminder]) + startStr
//             divider <- divider/62
//         (i,STR_PREFIX + startStr)
//     }


// //show all the index and corresponding string
// for (a, astr) in seq1 do
//     printfn "%s" astr
    


//comment out the method of randomly generating strings
(*
//Create string random with Gator link ID as prefix
let prefix = "hongru.liu;"
let lengthOfStr = Console.ReadLine()

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

//test output
let res1 = generateSHA256Str randomString
printfn "%s" res1
// Test: check if SHA256 of a string is valid
validateSHAStr res1 2 |> printfn "%d"
*)




// // Test: print out SHA256 String
// let str = "7wLfA2pSBgg2e6A"
// let res = generateSHA256Str str
// printfn "%s" res
// // Test: check if SHA256 of a string is valid
// validateSHAStr res 2 |> printfn "%d"


