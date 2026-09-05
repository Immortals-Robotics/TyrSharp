## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.

## 2026-07-26 - Replace LINQ GroupBy with fixed-size array in RobotMerger
**Learning:** GroupBy and ToDictionary in a per-frame vision processing loop (~100Hz) causes significant GC pressure by allocating dictionaries, closures, and enumerators on the heap.
**Action:** Use a pre-allocated fixed-size array of lists based on Robot ID + Team Color index mapping for zero-allocation grouping.
