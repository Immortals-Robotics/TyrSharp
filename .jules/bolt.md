## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.

## 2024-05-24 - Avoiding LINQ GroupBy/ToDictionary in Vision Processing
**Learning:** Structural LINQ methods like `SelectMany`, `GroupBy`, and `ToDictionary` in hot-path loops (e.g., `RobotMerger.Process()`) create hidden multi-level allocations, including `IGrouping` objects, enumerators, arrays, and dictionaries every frame. While these are convenient, they trigger significant Gen-0 GC pressure.
**Action:** Replace structural LINQ pipelines with class-level pre-allocated structures (like `Dictionary<RobotId, List<RobotTracker>>`) that are cleared and repopulated explicitly. Because `Vision` processing is strictly sequential on a single thread, mutating instance state is safe and prevents per-frame allocations.
