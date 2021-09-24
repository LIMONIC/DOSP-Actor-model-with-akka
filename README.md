# Distributed Operating System Projects 1
This project implented distributed computing systems based on actor model with F# and Akka.NET to handel intense computational tasks. To simulate high-intensity computaional tasks, the project takes reference from the Bitcoin's concept of finding some input string whose output values matches a specefic pattern after a cryptographic hashing computation.
## Problem Definition
Given a string, use SHA-256 to produce a hash value. Check if the heading part of the hash value contains a specefic number of consecutive zeros. Only the string's hash value that matches the rule are considered valid. The higher the number of zeros, the more difficult it is to find a valid string.

For example:   
An input string,"hongru.liu;jrs", gives a SHA- 256 hash value of "000000E9EF6A44DC2F5BA00873909A682032E9329FB29C95FE199482B352B923", which matches with the rule of six zeros.

* All of the input string are starting with "hongru.liu;" to ensure their specificity. It cna be omitted or replaced with any string.

## Part 1 - String Generation and Validation

**Input:** The number of leading zeros required for a valid SHA256 string

**Output** \<Input string>  \<Corresponding SHA256 hash>
### File Path
```
```
### Usage
```Console

```

## Part 2
### Data Structure
`<ID>;<String>`

Example: </br>
  **input string:** 12345678;abcdefg </br>
  **SHA256:** 3814A8C95BBEB45FB42265B97ABDE8BBE26467B218415E5B9CB1FE4B078B7201


### 6 Zeros:
0L, 10000000L

"hongru.liu;jrs",
  "000000E9EF6A44DC2F5BA00873909A682032E9329FB29C95FE199482B352B923"

Real: 00:05:28.639, CPU: 00:07:47.046, GC gen0: 24160, gen1: 1250, gen2: 5
Real: 00:14:39.383, CPU: 00:17:25.046, GC gen0: 29551, gen1: 2319, gen2: 9
Real: 00:11:24.845, CPU: 00:14:13.328, GC gen0: 29550, gen1: 614, gen2: 7

hongru.liu;1queD        000000F07D86CD6F4BE79ED6A5C19DDEEDDC5B9ADD0DDF3355F0D50CE80A0D60
hongru.liu;30VFn        000000240530A7C57B3825953318475E0128482D497EE506ED6249691364A536
hongru.liu;31iv4        000000EC5D92B8796FCC61F9B2D139C578046D50ECC239CDCE8DE74172E9E325

### 7 Zeros

hongru.liu;fOy5h        0000000B8288B442DE93893F6BCFE766A218A0B1C3AFAFCC5237780B5B9BBE11
### 8 Zeros
0 - 15000000: not found

0 5000000000 8 
hongru.liu;2a1sqw       0000000037A4530C0D13C1050B7D3F3921B150B25C6DE013B79CDF5A80485663

### 9 Zeros

15000000 70000000 7

### 10 Zeros
0 125000000
Real: 01:09:52.756, CPU: 01:16:53.390, GC gen0: 68919, gen1: 3015, gen2: 43

125000000 250000000
Real: 00:28:20.337, CPU: 00:13:52.375, GC gen0: 68375, gen1: 4398, gen2: 5

250000000 375000000
Real: 00:28:18.032, CPU: 00:13:43.453, GC gen0: 68344, gen1: 3434, gen2: 5

375000000 500000000
Real: 00:27:18.314, CPU: 00:13:43.937, GC gen0: 68367, gen1: 4630, gen2: 5

500000000 6000000000 10


Size of the work unit that you determined results in the best performance for your implementation and an explanation of how you determined it.
7

The size of the work unit refers to the number of sub-problems that a worker gets in asingle request from the boss.


The result of running your program for input 4

The running time for the above as reported by time for the above and report the time.  The ratio of CPU time to REAL TIME tells you how many cores were effectively used in the computation.  If you are close to 1 you have almost no parallelism (points will be subtracted).


The coin with the most 0s you managed to find.


The largest number of working machines you were able to run your code with.