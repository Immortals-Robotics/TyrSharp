# RoboCup Small Size League (SSL) — Overview & Technical Reference

This document provides background on the RoboCup Small Size League (SSL) — the competition Tyr is built for — covering the league structure, rules, and the software/communication stack that every SSL team integrates with.

> **Last verified:** March 2026. Always cross-check against the [live rule book](https://robocup-ssl.github.io/ssl-rules/sslrules.html) before a competition.

---

## 1. What Is the Small Size League?

The RoboCup Small Size League (SSL) is one of the oldest and most technically demanding leagues in the RoboCup Soccer competition. Teams field small wheeled robots that play fully autonomous soccer on a shared field. The defining characteristics of the league are:

- **Centralised shared vision** — all teams share a single overhead camera system (SSL-Vision) rather than relying on onboard sensing.
- **Off-field computation** — all AI, planning, and control runs on computers beside the field; robots receive commands over wireless radio.
- **Fully autonomous play** — no human intervention is permitted during a game once it starts.
- **Orange golf ball** — the official ball is a standard orange golf ball.

### Divisions

| | Division A (double-size) | Division B (single-size) |
|---|---|---|
| Max robots per team | 11 | 6 |
| Playing area | 12 m × 9 m | 9 m × 6 m |
| Total field (incl. borders) | 13.4 m × 10.4 m | 10.4 m × 7.4 m |
| Timeouts per team | 6 | 4 |
| Total timeout time | 7 min 30 s | 5 min |

---

## 2. League Future — Important Notice

> ⚠️ **The SSL will no longer appear as a major RoboCup world event after 2027.** The last world championship for the SSL is RoboCup 2027. From 2028 onward, the league continues at regional events (German Open, RoboCup Brazil, Japan Open, etc.) with a possible rotating "super regional" to maintain international presence. Teams wishing to remain on the world stage are being encouraged to transition toward humanoid leagues.
>
> Source: [Major Updates Regarding the Future of the SSL](https://ssl.robocup.org/major-updates-regarding-the-future-of-the-small-size-league/)

---

## 3. Game Rules (current — 2026 edition)

The authoritative rule book is maintained by the SSL community:

- **HTML (live — this is the 2026 edition):** https://robocup-ssl.github.io/ssl-rules/sslrules.html
- **GitHub source:** https://github.com/RoboCup-SSL/ssl-rules
- **Official rules page:** https://ssl.robocup.org/rules/
- **Archived 2025 rules:** https://robocup-ssl.github.io/ssl-rules/2025/sslrules.html

---

### 3.1 Robot Constraints

Every robot must fit inside a **180 mm diameter, 150 mm tall cylinder** at all times. No part of a robot may exceed this envelope at any point during play.

**Hull colour**
Teams must supply **interchangeable bright and dark hulls**. Hulls must:
- Cover at least **6 cm of the robot's height**
- Be **non-reflective**
- Not use colours that interfere with the SSL-Vision fiducial pattern colours

*(Requirement introduced in 2025; language refined in June 2025 for "better clarity and flexibility".)*

---

### 3.2 Field & Defense Area

The field surface is green carpet. Field dimensions may vary by up to ±10 % in each linear dimension at the competition venue.

**Defense area** (the rectangular zone in front of each goal where only the keeper may touch the ball):

| | Division A | Division B |
|---|---|---|
| Width (along goal line) | 3.6 m | 2.0 m |
| Depth (into the field) | 1.8 m | 1.0 m |

> ℹ️ The defense area was enlarged (from a 0.8 m quarter-circle radius / 1.95 m width to the current rectangular shape) to give keepers more room and reduce congestion near the goal.

---

### 3.3 Game Flow

A match consists of **two halves** separated by a half-time break of at most **5 minutes**. The exact half duration is set by the tournament organiser.

**Game states managed by the Game Controller**

| State | Description |
|---|---|
| `HALT` | All robots must stop immediately |
| `STOP` | Robots slow to ≤ 1.5 m/s, keep 0.5 m from ball |
| `RUNNING` (NORMAL_START / FORCE_START) | Normal play |
| `KICKOFF` | Kickoff preparation → NORMAL_START |
| `DIRECT_FREE` / `INDIRECT_FREE` | Free-kick preparation |
| `PENALTY` | Penalty kick preparation → NORMAL_START |
| `BALL_PLACEMENT` | Automated ball placement to target position |

---

### 3.4 Free Kicks

- **Division A:** the team in possession must bring the ball into play within **5 seconds**.
- **Division B:** the limit is **10 seconds**.
- Failure to shoot in time now results in an **indirect free kick for the opponent** (rather than a forced start, as in older rules).
- All non-kicking robots must stay **0.5 m** from the ball during a free kick.

---

### 3.5 Penalty Kicks

- All robots except the kicker and the opponent keeper must position themselves **at least 1 m behind the ball**.
- If the ball remains in play for more than **10 seconds** after the penalty kick is taken, the game is stopped.

---

### 3.6 Timeouts

| | Division A | Division B |
|---|---|---|
| Timeouts per team | 6 | 4 |
| Total time budget | 7 min 30 s | 5 min |

- Timeouts are requested via the communication-flags protocol; the game controller tracks remaining time.
- Robots **touched** during a timeout must leave the field and re-enter **only through the substitution area**. *(2025 rule, still in force.)*

---

### 3.7 Robot Substitution

- Substitutions may happen **at any time** and are unlimited in number.
- Teams signal substitution intent to the game controller; if a substitution is pending just before play resumes after ball placement, the GC **automatically halts** the game to allow it.
- Once halted, the team has **20 seconds** to complete the substitution at the substitution area. *(Extended from 10 s → 20 s in April 2024.)*
- A **dedicated substitution area** on the field boundary was formally defined in the February 2026 rule update.

---

### 3.8 Notable Fouls

| Foul | Consequence |
|---|---|
| Robot too close to opponent defense area (during stop/free kick) | Indirect free kick for opponent; **game immediately halted** if committed twice in one stopped phase |
| Pushing / collision | Indirect free kick for fouled team |
| Ball speed > 6.5 m/s | Indirect free kick for opponent |
| Keeper outside defense area touching ball | Indirect free kick for opponent |
| Dribbling > 1 m without opponent touch | Indirect free kick for opponent |

---

### 3.9 Keeper

- Only one robot per team may be designated keeper at a time.
- The keeper **may touch the ball inside its own defense area**; all other robots may not.
- Keeper may not carry the ball outside its defense area.
- Rules were updated in 2026 with **clearer language and diagrams** on keeper positioning during free kicks and penalty kicks.

---

## 4. Software & Communication Stack

Every SSL team's software must interface with three shared community tools: **SSL-Vision**, the **SSL Game Controller**, and optionally **grSim** for simulation. All communication uses **UDP multicast** over a local network and **Google Protocol Buffers** for serialisation.

```
┌──────────────────────────────────────────────────────────┐
│                   Shared Infrastructure                  │
│                                                          │
│   ┌─────────────┐        ┌──────────────────────┐        │
│   │  SSL-Vision │        │  SSL Game Controller │        │
│   │  (cameras)  │        │  (referee state)     │        │
│   └──────┬──────┘        └──────────┬───────────┘        │
│          │ UDP multicast            │ UDP multicast      │
│          │ 224.5.23.2:10006         │ 224.5.23.1:10003   │
└──────────┼──────────────────────────┼────────────────────┘
           │                          │
           ▼                          ▼
┌──────────────────────────────────────────────────────────┐
│                     Tyr (this codebase)                  │
│                                                          │
│   Vision module          Referee module                  │
│   (Kalman filtering,     (GC packet parsing,             │
│   tracking, prediction)  state machine)                  │
│                                                          │
│   Soccer AI (planning, skills, navigation)               │
│                                                          │
│   Sender (NRF radio ──► robots  OR  grSim simulator)     │
└──────────────────────────────────────────────────────────┘
```

---

### 4.1 SSL-Vision

**Repository:** https://github.com/RoboCup-SSL/ssl-vision

SSL-Vision is the shared vision server that processes overhead camera feeds and publishes robot and ball positions to all teams simultaneously. It does **not** perform tracking or sensor-merging — it delivers raw, per-camera detections. Teams are responsible for fusing detections across cameras, tracking objects over time, and estimating velocities. Tyr's `Vision` module handles this with Kalman filters.

**Network**

| Parameter | Value |
|---|---|
| Protocol | UDP Multicast |
| Multicast address | `224.5.23.2` |
| Port | `10006` |
| Legacy port | `10002` |
| Message format | Google Protocol Buffers |

**Protobuf message types**

All packets use the `SSL_WrapperPacket` envelope (`messages_robocup_ssl_wrapper.proto`), which contains one or both of:

- `SSL_DetectionFrame` (`messages_robocup_ssl_detection.proto`) — per-camera detection results for robots and ball, broadcast immediately when each camera frame completes.
- `SSL_GeometryData` (`messages_robocup_ssl_geometry.proto`) — field dimensions and camera calibration, broadcast every **3 seconds** by default (configurable).

**Important characteristics**
- Packet ordering is **not guaranteed** (each camera runs on its own thread).
- SSL-Vision performs **no tracking or sensor merging** — teams handle this entirely.
- Proto source files live in `src/shared/proto/` in the repository.

> **Note (since RoboCup 2023):** The AutoRef publishes filtered, tracked vision data (including velocities) alongside the raw SSL-Vision stream. New teams are encouraged to consume this tracked data instead of the raw detections.

---

### 4.2 SSL Game Controller

**Repository:** https://github.com/RoboCup-SSL/ssl-game-controller

The SSL Game Controller (introduced at RoboCup 2019, replacing the old `ssl-refbox`) manages all game-state transitions. The human referee interacts via a web UI; the GC then broadcasts state to all teams.

**Referee broadcast (outbound, all teams)**

| Parameter | Value |
|---|---|
| Protocol | UDP Multicast |
| Multicast address | `224.5.23.1` |
| Port | `10003` |
| Message format | `SSL_Referee` (`ssl_gc_referee_message.proto`) |
| Message length prefix | uvarint (length in bytes) |

**Additional interfaces**

| Interface | Transport | Plain port | TLS port | Purpose |
|---|---|---|---|---|
| Team client | TCP | `10008` | `10108` | Send advantage choices, challenge flags, substitution intent |
| Remote control client | TCP | `10011` | `10111` | Human referee remote input |
| AutoRef interface | TCP | `10013` | — | AutoRef apps report rule violations |
| CI mode | TCP | `10009` | — | Deterministic time control for simulated/test games (`ssl_gc_ci.proto`) |

**Role in Tyr**
Tyr's `Referee` module subscribes to the multicast stream and parses `SSL_Referee` packets into internal game-state used by the Soccer AI.

---

### 4.3 grSim — Simulator

**Repository:** https://github.com/RoboCup-SSL/grSim

grSim is the standard physics simulator for the SSL. It acts as a drop-in replacement for real hardware: it publishes SSL-Vision-compatible detection packets (so Tyr's `Vision` module works unchanged) and accepts robot commands in the SSL simulation protocol format.

**Integration notes**
- When using grSim, the game controller can be run in **CI / vision mode** so it synchronises its clock with the simulator rather than wall time.
- Robot commands are sent to grSim via the `ssl-simulation-protocol` protobuf format (separate from the team-specific NRF radio protocol).
- **Simulation protocol repo:** https://github.com/RoboCup-SSL/ssl-simulation-protocol

---

## 5. How Tyr Fits In

| Tyr Module | SSL Component | Channel |
|---|---|---|
| `Vision` | SSL-Vision multicast → Kalman tracking | → `Hub.Vision` |
| `Referee` | Game Controller multicast → state machine | → `Hub.Referee` |
| `Soccer` | Consumes vision + referee; runs AI | → `Hub.Commands` |
| `Sender` | Publishes commands to NRF radio or grSim | ← `Hub.Commands` |

For a deeper dive into how these modules are wired together internally, see the [CLAUDE.md](../CLAUDE.md) architecture notes.

---

## 6. References & Further Reading

| Resource | Link |
|---|---|
| SSL Official Site | https://ssl.robocup.org |
| Rule Book — 2026 edition (live HTML) | https://robocup-ssl.github.io/ssl-rules/sslrules.html |
| Rule Book GitHub source | https://github.com/RoboCup-SSL/ssl-rules |
| Rule Book — 2025 archive | https://robocup-ssl.github.io/ssl-rules/2025/sslrules.html |
| RoboCup 2025 Rule Update announcement | https://lists.robocup.org/archives/list/robocup-small@lists.robocup.org/message/B3J4IMDI74B43TTW4Q2I4GECVR64TATN/ |
| Future of the SSL (major announcement) | https://ssl.robocup.org/major-updates-regarding-the-future-of-the-small-size-league/ |
| SSL GitHub Organisation | https://github.com/RoboCup-SSL |
| SSL-Vision | https://github.com/RoboCup-SSL/ssl-vision |
| SSL-Vision Wiki (communication) | https://github.com/RoboCup-SSL/ssl-vision/wiki/communication |
| SSL Game Controller | https://github.com/RoboCup-SSL/ssl-game-controller |
| grSim Simulator | https://github.com/RoboCup-SSL/grSim |
| Simulation Protocol | https://github.com/RoboCup-SSL/ssl-simulation-protocol |
