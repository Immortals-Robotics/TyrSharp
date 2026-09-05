# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Tyr is the robotics software stack for Immortals, a RoboCup Small-Size League (SSL) team. It is a C# 14 / .NET 10 port of the original C++ codebase. The system receives vision data from SSL cameras and game controller commands, processes them, and sends robot commands via NRF radio or a simulator.

## Commands

```bash
# Build the whole solution
dotnet build

# Run all tests
dotnet test

# Run a single test class/method
dotnet test --filter "FullyQualifiedName~Tyr.Tests.Common.Math.GeometryTests"

# Run the full stack headless (no GUI), until Ctrl+C
dotnet run --project Cli -- Data/config.toml

# Run a simulation scenario against grSim + game controller; results in runs/<timestamp>-<name>/summary.json
# See docs/SIM-HARNESS.md for the scenario format, outputs, and known pain points.
dotnet run --project Cli -- Data/config.toml --scenario Data/scenarios/their-freekick-8-robots.toml

# Run the GUI (includes all modules)
dotnet run --project Gui -- Data/config.toml
```

## Project Structure

The solution (`Tyr.sln`) consists of these projects:

| Project | Description |
|---|---|
| `Common` | Shared math, networking, config, debug, dataflow, and SSL data types |
| `Vision` | Processes raw SSL vision frames; Kalman-filter tracking + ball trajectory modeling |
| `Referee` | Receives game controller (GC) packets and publishes referee state |
| `Soccer` | AI and robot control logic (navigation, skills, etc.) |
| `Sender` | Sends robot commands over NRF radio or to a simulator |
| `Gui` | ImGui/OpenGL visualization and configuration UI |
| `Control` | grSim and game-controller process management and protocol clients (used by Gui and Cli) |
| `Cli` | Headless entry point and simulation harness (`--scenario`) |
| `SourceGen` | Roslyn source generators used at compile time |
| `Tests` | xUnit test project |

## Key Architecture Concepts

### Dataflow (Hub / BroadcastChannel)
Modules communicate through a pub/sub system. `BroadcastChannel<T>` broadcasts to all subscribers. `Hub` (generated via `[GenerateGlobals]`) exposes static typed channels (e.g., `Hub.Vision`, `Hub.Referee`, `Hub.Commands`). Subscribers use `Mode.Latest` (drop-oldest, capacity 1) or `Mode.All` (unbounded).

### Source Generator (`SourceGen`)
Every project has a `Module.cs` with `[GenerateGlobals]`. This triggers `GlobalsGenerator`, which emits a `Globals.g.cs` per assembly providing:
- `Log` — ZLogger-based logger
- `Assert` — assertion helper
- `Draw` — debug drawing
- `Plot` — debug plotting
- `Rand` — random
- A `[ModuleInitializer]` that calls `Config.Registry.Register(<Type>.Configurable)` once for every `[Configurable]` type in the assembly (no reflection, no assembly scan)

`ConfigurableGenerator` emits, for any type marked `[Configurable]`, the implementing partial: each `[ConfigEntry]` static partial property gets a setter with change detection, plus a `Configurable` handle describing every entry. It reports `TYR001`–`TYR003` for non-partial types, malformed entries and unsupported type shapes.

### Configuration System
Types annotated with `[Configurable]` must be `partial` and declare static **partial** `[ConfigEntry]` properties whose initializer is the default value:

```csharp
[Configurable]
public sealed partial class Runner
{
    [ConfigEntry("Frames per second, 0 = unlimited", StorageType.User)]
    private static partial int MaxFps { get; set; } = 0;
}
```

`ConfigurableGenerator` implements each property with change detection: assigning an equal value is a no-op, a different value bumps `Configurable.Version` and raises `Configurable.OnUpdated`. It also emits a per-type `Configurable` handle that lists the entries with typed getters/setters, so the runtime uses no reflection; each assembly's module initializer (in `Globals.g.cs`) registers those handles with `Config.Registry`. Two `Storage` instances are loaded: a project config (`Data/config.toml`) and a user override (`user.toml`). A storage loaded before a module registered its configurables replays its values onto them, and saves merge into the parsed file so tables owned by modules not loaded in this process survive. Config is live-reloaded via file watchers with debouncing. `OnUpdated` fires on the thread that made the change (the watcher thread for reloads); code that must react on a specific thread polls `Configurable.Version` instead. Values edited in place (lists, dictionaries) do not go through a setter, so call `Configurable.MarkChanged(storageType)` or `ConfigEntry.Set` for those. Conversely, values the process re-derives at runtime from an external source must not be written back to disk — wrap those assignments in `using var _ = Configurable.SuppressNotifications();` (see `Vision.Data.BallParameters.Apply`).

### Soccer AI Loop
`Soccer.Runner` creates two `TeamRunner` instances (Yellow and Blue), enabled by config. Each `TeamRunner` subscribes to vision, referee, and field-size channels, then on each tick:
1. Calls `Ai.UpdateContext()` — updates robot states with Kalman-predicted positions at `VisionTime + VisionPredictionTime`
2. Calls `Ai.Process()` — runs game AI / assigns skills to robots
3. Calls `Ai.PublishCommands()` — publishes a `CommandsWrapper` to `Hub.Commands`

`Context` is `AsyncLocal<ContextData>` so blue and yellow AIs run independently on separate threads.

### Soccer Context and Skills
`Context` is a static accessor for the current thread's `ContextData`. Each `Robot` object holds navigation state and a `CurrentCommand`. Skills implement `ISkill` with a single `Execute(Robot robot)` method. Navigation uses trajectory planning (`Planner`, `Trajectory2D`, `TrajectoryBangBang`) with an obstacle map.

### Banned APIs
`Common/BannedSymbols.txt` bans `System.Console` and `Microsoft.Extensions.Logging.LoggerExtensions`. Always use `Log.ZLog***` methods (ZLogger) for logging.

## Conventions

- Namespace root is `Tyr.*` (e.g., `Tyr.Common`, `Tyr.Soccer`)
- All physical units in the codebase are **millimeters** for distances and **mm/s** for speeds
- Angles are represented via the custom `Angle` type; use `Angle.FromDeg()` / `Angle.FromRad()`
- Use `DeltaTime` / `Timestamp` types from `Tyr.Common.Time` for all time values
- Nullable reference types and implicit usings are enabled in every project
- Language version is C# 14
