## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.
## 2026-07-17 - Avoid heavy LINQ groupings and allocations in hot path

**Learning:** `RobotMerger.Process` ran every frame and used `SelectMany`, `GroupBy`, `ToDictionary`, and `.ToList()` to group trackers by `RobotId`. This resulted in heavy garbage generation (`IGrouping`, `Dictionary`, and new `List` collections per frame).
**Action:** Replace dynamic LINQ dictionary allocations with a pre-allocated fixed array of lists since there is a well-known maximum number of robots (`CommonConfigs.MaxRobots * 2`). Instead of `GroupBy` and `ToDictionary`, explicitly loop over the collections and index into the pre-allocated lists based on `RobotId`, calling `.Clear()` each frame. This achieves 0 per-frame allocations for grouping.
