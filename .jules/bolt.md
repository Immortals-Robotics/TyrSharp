## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.

## 2026-07-18 - Replacing LINQ allocations in Role Assignment safely
**Learning:** `RoleAssignmentSolver.Solve` was instantiating several LINQ queries and anonymous arrays within the per-frame hot path (e.g. `Where`, `FirstOrDefault`, `ToArray`), creating several implicit allocations in the GC. While using class-level backing `List<T>` buffers removes the allocations, they present a thread-safety hazard when shared across blue/yellow thread isolates. Using `ArrayPool<T>.Shared.Rent` provides thread safety safely inside scoped function allocations while preserving GC pause performance.
**Action:** Always prefer `System.Buffers.ArrayPool<T>` (paired with length tracking variables for dynamic count structures) instead of class-level buffers when optimizing local variables that might exist across parallel processing channels.
