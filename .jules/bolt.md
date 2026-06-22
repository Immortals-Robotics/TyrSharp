## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.

## 2024-05-18 - Eliminated Per-Frame LINQ in RobotMerger
**Learning:** `SelectMany().GroupBy().ToDictionary()` in `RobotMerger.Process` caused ~37 allocations per frame (at 100Hz, this is huge). Since `Vision.Process` runs sequentially on a single thread (unlike `Ai` which runs blue/yellow concurrently), it is perfectly safe to replace this with a reusable class-level `Dictionary<RobotId, List<RobotTracker>>`. Also, build failures in the dev environment for `Tyr.Common` are often related to `SourceGen` caching issues with `GenerateGlobals`, so build errors like `Timestamp not found` should be evaluated against changes.
**Action:** Always prefer clearing and reusing class-level collections (`.Clear()`) in single-threaded pipelines over LINQ chains. In the `Vision` module specifically, thread isolation from the AI allows aggressive reuse.
