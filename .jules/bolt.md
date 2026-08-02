## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.
## 2024-07-28 - RobotMerger LINQ Allocations
**Learning:** `SelectMany`, `GroupBy`, and `ToDictionary` in the vision processing hot path (`RobotMerger.Process`) cause significant allocations (~40 per frame) because they allocate enumerators, groupings, and a new dictionary every tick.
**Action:** Replace these LINQ chains with pre-allocated, class-level collections (like `Dictionary<RobotId, List<RobotTracker>>`) that are cleared and reused each frame.
