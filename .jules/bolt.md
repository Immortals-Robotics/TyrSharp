## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.

## 2026-07-13 - LINQ SelectMany, GroupBy, and ToDictionary allocate heavily in real-time loops
**Learning:** Chaining LINQ methods like `SelectMany`, `GroupBy`, and `ToDictionary` in a per-frame hot path allocates memory on every cycle. Specifically, they allocate heap objects for the enumerators, the intermediate groupings, dictionary entries, and new backing arrays/lists. In `RobotMerger.Process`, this adds up to ~100 allocations/frame, causing significant GC pressure over time.
**Action:** Replace structural LINQ operations on collections with pre-allocated class-level collections (e.g., `Dictionary<K, List<V>>`) and explicitly iterate/repopulate them using `foreach` and `.Clear()`.
