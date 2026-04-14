## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.

## 2026-10-27 - Double-buffering to eliminate per-frame dictionary allocations
**Learning:** When a mapping is built in the hot path and needs to be compared against the previous frame (like tracking role or tactic assignments), constructing a `new Dictionary<int, T>()` on every frame is a massive allocation hotspot.
**Action:** Use a double-buffering approach. Pre-allocate two dictionaries (e.g., `_roleMapping` and `_nextRoleMapping`), clear `_nextRoleMapping` at the start of the frame, build the current frame mapping into it, perform any comparisons against `_roleMapping`, and finally swap them: `(_roleMapping, _nextRoleMapping) = (_nextRoleMapping, _roleMapping)`.
