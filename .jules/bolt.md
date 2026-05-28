## 2026-04-12 - Avoid LINQ in per-frame hot path
**Learning:** LINQ methods like `Where` and `FirstOrDefault` implicitly allocate enumerators and closures when capturing state (e.g., `Context.Color` or lambda expressions). In a 100Hz real-time loop like `Ai.UpdateContext()` and `Ai.Process()`, these allocations stack up quickly, causing significant GC pressure and potential micro-stutters.
**Action:** Replace `LINQ` operations with manual `foreach` or `for` loops in the per-frame hot path to achieve zero-allocation data iteration.

## 2026-05-28 - String Interpolation and LINQ inside Log calls
**Learning:** `Log.ZLogDebug` calls that include string interpolation (using `$""`) combined with complex expressions like `string.Join` and `.Select()` will evaluate these expressions and allocate memory (strings, closures, arrays) *before* the logger's internal log level filter is checked. If debug logging is disabled, these allocations are completely wasted.
**Action:** Always wrap expensive log messages (those doing LINQ, allocations, or complex string concatenations) inside an explicit `if (Log.IsEnabled(LogLevel.Debug))` block to prevent evaluating the arguments when the log level is not active.
