## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.
## 2026-05-29 - Avoid LINQ Grouping and Dictionary Allocations
**Learning:** LINQ methods like `SelectMany`, `GroupBy`, `ToDictionary`, and `ToList` allocate heavy intermediate structures (`IGrouping`, new `Dictionary`, new `List`) every time they are called. In the per-frame Vision pipeline (`RobotMerger.Process`), this results in dozens of allocations per frame (~1.6k allocs/sec at 100Hz).
**Action:** Replace dynamic per-frame dictionary allocations with a class-level pre-allocated and reused `Dictionary<K, List<V>>`. Clear the inner lists at the start of each frame and populate them with explicit loops.
