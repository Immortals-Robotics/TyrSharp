# Simulation harness

`Tyr.Cli` is the headless runner. Given a scenario file it launches (or attaches to) grSim and
the SSL game controller, starts the full Tyr stack, scripts the world, runs for a fixed time
while sampling it, and writes machine-readable results. It is meant to be driven by people and
by agents alike: one command in, one directory of results out, exit code says what happened.

## Quick start

```bash
dotnet build
dotnet run --project Cli -- Data/config.toml --scenario Data/scenarios/their-freekick-8-robots.toml
```

Results land in `runs/<utc-timestamp>-<scenario>/` (gitignored):

| File | What |
|---|---|
| `summary.json` | Samples of ball, robots, referee state and our commands at `run.sample_hz`, plus events. Start here. |
| `sessions/<scenario>/<id>/` | The full debug-db recording (draws, plots, logs). Open it in the GUI's session view for a replay. |
| `config.toml` | The exact project config the run used (a copy; the run never writes to `Data/config.toml`). |
| `scenario.toml` | The scenario as run. |

Exit codes: `0` ok, `2` a dependency (grSim process, GC API, vision frames, referee packets) did
not become ready in time, `3` the scenario or a config override is invalid, `1` unexpected error,
`64` bad command line.

Other modes:

```bash
# Full stack headless until Ctrl+C, no scenario (what the old CLI did, plus Soccer/Sender/recorder)
dotnet run --project Cli -- Data/config.toml

# Override any config entry by its TOML path (value in TOML syntax); repeatable
dotnet run --project Cli -- Data/config.toml --scenario s.toml --set Soccer.Runner.RunBlue=true --set "Sender.Simulator.ChipAngle=0.7"

# Shorter run, different output root, also load a user config
dotnet run --project Cli -- Data/config.toml --scenario s.toml --duration 5 --out /tmp/runs --user user.toml
```

Overrides are applied after the config files load and before any module starts, without
notification, so they are never persisted. By default no user config is loaded, so runs are
reproducible from the project config plus the scenario alone.

## Scenario format

TOML. Units follow the repo convention: millimetres, mm/s, degrees. Conversion to grSim's metres
happens at the boundary.

```toml
name = "their-freekick-8-robots"
description = "free text"

[processes.grsim]
mode = "launch"        # none | attach | launch (launch also downloads the fork's release if not cached)
headless = false       # -H; see "Known pain points" before turning this on

[processes.gc]
mode = "launch"        # none | attach | launch
rcon_port = 10011
ui_port = 8081

[config]               # config overrides, keyed by TOML path
"Soccer.Runner.RunYellow" = true

[[setup]]              # ordered steps, executed once everything is ready
action = "gc"
command = "HALT"       # any GC command type: HALT STOP FORCE_START NORMAL_START KICKOFF DIRECT PENALTY BALL_PLACEMENT TIMEOUT
# for_team = "blue"    # for sided commands

[[setup]]
action = "gc_side"     # who defends which half; always set this, GC defaults are not what you think
team = "blue"
positive_half = true

[[setup]]
action = "teleport_ball"
x = -1500
y = 1000
# z = 0, vx = 0, vy = 0

[[setup]]
action = "teleport_robot"
team = "yellow"
id = 3
x = -2400
y = 1800
# orientation_deg = 0

[[setup]]
action = "remove_robot"
team = "yellow"
id = 9

[[setup]]
action = "gc_ball_placement_pos"   # designated position for BALL_PLACEMENT
x = -1000
y = 500

[[setup]]
action = "gc_stage"    # e.g. NORMAL_FIRST_HALF
stage = "NORMAL_FIRST_HALF"

[[setup]]
action = "wait"
seconds = 2.0

[[setup]]
action = "sim_speed"   # grSim simulation speed multiplier; 0 pauses. Resume was not verified, see below.
speed = 1.0

[run]
duration_seconds = 10
sample_hz = 10
readiness_timeout_seconds = 20
```

When grSim is in use the harness forces `Vision.SslVisionDataPublisher.UseSimulator = true`,
`Sender.Simulator.Enabled = true` and disables the NRF and ZMQ senders, so a simulated run can
never talk to real robots. The recorder's root directory and capture label are pointed at the
run directory. Scenario `[config]` entries and `--set` win over these defaults.

## summary.json

```json
{
  "Scenario": "their-freekick-8-robots",
  "StartedUtc": "...", "DurationSeconds": 10.0, "SampleHz": 10,
  "SessionDirectory": ".../sessions/their-freekick-8-robots/...",
  "VisionFrames": 1211,
  "Final": { "...same shape as a sample..." },
  "Samples": [
    { "T": 0.0,
      "Ball": { "X": -1500, "Y": 1000, "Z": 0, "Vx": 0, "Vy": 0 },
      "Robots": [ { "Team": "Yellow", "Id": 0, "X": -4300, "Y": 0, "AngleDeg": 0, "Vx": 0, "Vy": 0 }, "..." ],
      "Referee": { "GameState": "Stop", "Color": "Unknown", "Ready": false, "GcCommand": "Stop", "BlueTeamOnPositiveHalf": true },
      "Commands": { "Yellow": [ { "Id": 0, "Halted": false, "Vx": 0, "Vy": 0, "TargetAngleDeg": 0, "Shoot": 0, "Chip": 0, "Dribbler": 0 } ] } }
  ],
  "Events": [ { "T": 0.1, "Type": "referee", "Text": "Blue FreeKick" }, { "T": 0, "Type": "teleport_ball", "Text": "(-1500, 1000)" } ]
}
```

Events record every setup step and every referee state transition. Warnings and errors the AI
logs during the run are in the process output and in the session's log entries; the harness does
not summarise them yet (a warning histogram is a one-liner over the log, see below).

```powershell
dotnet run --project Cli -- Data/config.toml --scenario s.toml > run.log 2>&1
Get-Content run.log | ? { $_ -match '\| (Warning|Error) \|' } | % { ($_ -replace '^.*?\] ', '') -replace '\d+','#' } | group | sort Count -desc | select -first 10
```

## Architecture

- `Control/` (`Tyr.Control`) holds the process and protocol clients that used to be private to
  the GUI: `GrSimProcess`, `GcProcess`, `SimulatorChannel` (grSim control protocol), `GcApiClient`
  (GC websocket API), `GcRconClient` (team remote control). Both `Gui` and `Cli` reference it.
- `Cli/Harness.cs` composes the stack in the same order as `Gui/Program.cs` (recorder first),
  waits for readiness, executes the setup script, samples, writes results, and stops what it
  launched. `Cli/WorldSampler.cs` subscribes to `Hub.Vision`, `Hub.Referee` and `Hub.Commands`
  in-process; `Cli/Scenario.cs` is the TOML model; `Cli/ConfigOverrides.cs` resolves
  `Path.Type.Entry` to a `ConfigEntry` and assigns inside `SuppressNotifications()`.
- Readiness is observational: grSim is "ready" when tracker frames flow, the GC when referee
  packets arrive and its API reports a match state. There is no acknowledgement for teleports; a
  scenario that needs one should `wait` briefly and the caller checks the first sample.

## Known pain points (found while bringing this up on 2026-09-05)

1. **grSim `-H` (headless) freezes the world.** With the Immortals fork v2.6.1 on Windows, `-H`
   starts, answers on the control port and streams vision, but neither robot commands nor
   teleports have any effect and nothing moves. The same scenario with the window shown works.
   The fork's `MainWindow::update()` does call `glwidget->step()` in that mode, so the cause is
   not obvious from reading; it needs a debugger session in the fork. Until then scenarios use
   `headless = false`. A window pops up and steals focus.
2. **grSim rejects `SimulatorConfig.vision_port`** (`GRSIM_UNSUPPORTED_CONFIG`). The harness
   therefore depends on grSim's own stored settings publishing vision on the port Tyr listens on
   (`Vision.SslVisionDataPublisher.SimulatorAddress`, 10025 in the shipped config). A fresh grSim
   install will need that set once in its UI, or the fork needs the config option implemented.
3. **grSim has no CLI for ports or a config file**; everything is in QSettings. Fine on a dev
   machine, awkward for CI. Options: implement `--config <ini>` in the fork, or drive settings
   over the protocol where it supports them.
4. **GC team sides are not what scenarios assume.** The GC came up with yellow on the positive
   half. `gc_side` now exists and every scenario should set both teams explicitly.
5. **Field geometry comes from grSim** (Division A, 12 m x 9 m by default), not from the
   scenario. Positions in the shipped scenarios were written for a 9 m x 6 m mental model and are
   merely "inside the field". `SimulatorChannel.SendConfig(geometry)` exists but nothing builds a
   `GeometryData` yet; a `field = "div-b"` scenario key would be the natural next step.
6. **`sim_speed`**: pausing (0) worked, resuming did not visibly help in the headless runs that
   were frozen anyway, so resume is unverified. Scenarios avoid it for now.
7. **`RobotControlResponse` from grSim occasionally fails to deserialize** (`Invalid wire-type`,
   1-3 per run, `Sender/Simulator.cs` feedback path). Harmless for the run, worth a look.
8. **No readback of Soccer's decisions.** Role assignments, play, and tactic states are only in
   the debug-db (as logs/draws), not in `summary.json`. A small `Hub` snapshot from Soccer (which
   the GUI audit item G.A1 wants anyway) would let the sampler record them.
9. **The debug-db viewer port (9000) is shared with the GUI.** Running a scenario while the GUI
   is open will collide; give the harness its own port via `--set` or make the default 0 = off.
10. **Autoref is off** (no autoref process is launched), by design: a free kick that nobody takes
    simply times out into `Running` after the GC's 5 s, which is what the first scenario shows.
    Add an autoref launcher only for soak runs.
11. **Nothing asserts anything yet.** The harness records; judging is left to whoever reads
    `summary.json`. A `[[checks]]` section (ball inside rect at t, robot within r of point, no
    warnings matching pattern, ...) with a non-zero exit code on failure is the obvious next step
    for CI use.

## First results

- `their-freekick-8-robots` reproduces audit items NS4/NS5 on the simulator: with 8 of our robots
  seen, `TheirFreeKick` still requests roles for 16 and the log shows ~4 "Required role left
  unfilled: Supporter" warnings per tick (4000 in a 10 s run).
- `ball-placement-already-placed` confirms the Group 1 `BallPlacement` fix: with the ball already on
  the designated position the two placers back off to ~650 mm and ~800 mm from it and the ball
  does not move; before the fix they would have driven to the field centre.
- After the Group 4 change (roles sized from the seen-robot count, generated roles desired rather
  than required) the same free-kick scenario logs zero unfilled-role warnings and the robots do
  the same thing they did before.
- `their-kickoff` shows the kickoff `DefenceWall`: one yellow robot settles at 790 mm from the
  ball on the line to our goal within ~2 s of the kickoff command.
