# DOSP-projects
Implement actor model with F# and AKKA.net.

## Part 1

**Input:** The number of leading zeros required for a valid SHA256 string

**Output** \<Input string>  \<Corresponding SHA256 hash>

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

### 8 Zeros
0 - 15000000: not found