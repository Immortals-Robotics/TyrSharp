## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.

## 2026-07-09 - Avoid LINQ Count with predicate in hot path
**Learning:** The `System.Linq` extension `.Count(predicate)` implicitly allocates an enumerator (and often a closure) in the hot path.
**Action:** Replace `.Count(predicate)` with an explicit `foreach` or `for` loop, manually incrementing a counter to avoid enumerator allocations.
