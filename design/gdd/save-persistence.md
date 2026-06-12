# Save & Persistence System

> **Status**: Designed (second revision — 2026-04-21)
> **Author**: Bertan Berkol + Claude Code agents
> **Last Updated**: 2026-04-21
> **Implements Pillar**: Pillar 1 (Vehicle as Character), Pillar 4 (Scarcity with Agency)
> **Review history**:
> - 2026-04-21 (1st): MAJOR REVISION NEEDED (10 blocking + 15 recommended). Revised same-day with scope reduction.
> - 2026-04-21 (2nd re-review): NEEDS REVISION → APPROVED WITH CARRY-OVERS per creative-director synthesis. 6 GDD-body must-fixes applied; 15 items graduate to Save ADR and implementation-gate ACs. See `reviews/save-persistence-review-log.md`.

## Overview

The Save & Persistence System is the serialization infrastructure for Wasteland Run. It owns two categories of durable state: **RunState** — the complete in-progress run snapshot (vehicle loadout, deck, Scrap balance, node map position, per-node runtime flags) — and **MasteryState** — the between-run persistent record (chassis mastery XP and level, unlocked content flags per chassis). Save operates as a **passive serializer**: it owns no schema knowledge of individual systems; each system defines its own Data Transfer Objects (DTOs) and `Serialize()/Deserialize()` pair, and Save orchestrates when and where to write. All writes are atomic (write-to-temp then rename) and a single rolling backup (N=1) is maintained alongside the live save. Writes execute on a background task so the render loop is never stalled by disk I/O. RunState is written at three moments: after every node transition, on periodic idle flush (bounded data loss on hard-kill), and on clean app quit. MasteryState is written once per combat after rewards are committed, with a blocking user dialog on catastrophic recovery failure. Schema migration is deferred to the first content patch that requires it; EA launches with version-tagged DTOs but no migration chain. Save & Persistence is pure infrastructure — players experience it as mastery progress that outlives any individual run and a vehicle build that is faithfully waiting for them the next session.

## Player Fantasy

The car remembers. Every dent is still there. The flamethrower still hasn't been installed. The cracked axle is waiting. When a player returns to an in-progress run — after a break, a crash, or a power-out — the vehicle is exactly where they left it, in exactly the condition they left it in. The wasteland takes fuel, parts, and routes through the player's own choices, not through technical accidents. Save & Persistence is the contract that makes Pillar 4 possible: scarcity only feels meaningful when every loss is authored by the player, not inflicted by the system.

The mastery carries forward even when the run doesn't. A run that dies at the Haven gate isn't erased — the chassis remembers the work. XP ticks up. A card unlocks. The next Scout builds a little sharper than the last. The wasteland took the run; it didn't take the lesson. This is the emotional purpose of separating RunState from MasteryState: losing a run should sting precisely because the run is finite — but the player is not. The specific vehicle is mortal; that mortality is what gives Pillar 1 its emotional weight.

**Mid-combat quit is an intentional soft-undo.** If a player Alt+F4s during a combat, the combat resumes from its starting state on next launch — deck restored, enemy at full HP, no cards played. This is a deliberate concession to the player: combat is the only decision layer where we allow a take-back, because forcing mid-combat persistence adds complexity disproportionate to the game's scope. Quitting between encounters is committed; quitting mid-encounter is replayable. Scarcity applies at the run level, not the turn level.

> **Design tension: Quit-to-Resume vs. Pillar 1 (acknowledged, not resolved mechanically).** Pillar 1 says every loss is authored by the player, not inflicted by the system. The soft-undo creates a narrow surface where a player could force-quit mid-combat to escape a losing fight and replay it — which is exactly the kind of "un-authored" outcome Pillar 1 argues against. We accept this tension. Precedent: *Hades* resumes in-room on quit; *Slay the Spire* resumes mid-combat on quit. Neither is considered fantasy-breaking because the scarcity that matters lives at the run level (parts lost, runs ended), not the turn level (cards played). A player who discovers and exploits the soft-undo will either stop on their own (the fantasy of "my car, my run" reasserts itself) or become a rare save-scumming edge case that is acceptable collateral. The soft-undo's primary justification is protection against *technical* run loss (crashes, power, driver faults) — the Pillar 1 tension is the price of that protection. This paragraph is the authored acknowledgment; no mechanical change is made.

## Detailed Design

### Core Rules

**R1 — Save Scope Separation**

Two distinct categories of durable state. A system may not write to both.

**RunState** — facts that are true within a single run and meaningless after it ends:
- Vehicle part loadout (installed parts per slot, damage state per part)
- Deck state (`CardId` lists for: in-deck, in-discard, exhausted)
- Scrap balance
- Node map position (current node index, node-index traversal list, branching path taken)
- Per-node runtime flags (e.g., `IsFreeValveApplied: bool` keyed by node index)
- Run-scoped counters (e.g., the Rare pity counter from Card System)

**MasteryState** — facts that persist across runs:
- Chassis mastery XP per chassis
- Chassis mastery level per chassis
- Unlocked content flags per chassis

**Enforcement:** `IRunStateSerializable` and `IMasteryStateSerializable` are distinct C# interfaces. A class may not implement both. Enforcement mechanism: a **reflection-based unit test** (`tests/unit/save/InterfaceExclusion_test.cs`) scans all types in the gameplay assembly at CI time and fails the build if any type implements both interfaces. C# has no language-level compile enforcement for this — the unit test is the gate.

---

**R2 — Write Triggers**

**RunState is written at three points:**

1. **After node resolution** — when all node content has fully resolved (combat completed, event resolved, merchant visit concluded) and all outcomes have been applied (rewards taken, deck modified, Scrap adjusted). The write fires when the map screen is displayed. This is the only automatic single-event RunState write during a run. **Gate (retrofit 2026-04-23):** write deferred if `NodeEncounter.HandlerActive == true` OR `NodeMap.IsCommitInProgress == true`. Write fires on the transition to both false.
2. **On periodic idle flush** — while the player is on the map screen (not resolving a node), RunState is written every 30 seconds if the in-memory state is dirty. This bounds data loss on hard-kill events (Task Manager, power loss, driver crash) to at most 30 seconds of map-screen dwell. The periodic flush does not fire during node resolution. **Flush cap:** at most **30 periodic flushes per continuous map-screen dwell session**. After 30 flushes within a single dwell (~15 minutes of idle), the timer stops firing until the player leaves the map screen (enters a node, opens a menu that changes state, or re-enters from a different state). This bounds F1's upper output for pathological idle sessions. The cap resets on every map-screen re-entry. **Gate (retrofit 2026-04-23):** periodic flush skipped entirely if `NodeEncounter.HandlerActive == true` OR `NodeMap.IsCommitInProgress == true`; the 30s timer continues to run but the write is a no-op until both clear.
3. **On clean app quit** — `Application.quitting` fires a synchronous RunState flush using current in-memory state. **This is a best-effort safety net, not a primary durability mechanism** — Unity does not call `Application.quitting` on Task Manager kill, Steam force-stop, OOM, SIGKILL, or driver crash. The periodic flush (2) is what guarantees bounded data loss across hard-kill failure modes. **Gate (retrofit 2026-04-23):** if `HandlerActive == true` at quit, the quit-flush writes the **pre-encounter snapshot** (last committed save) rather than the in-memory mid-encounter state. This preserves the "mid-encounter crash = restart encounter" contract uniformly across crash types.

**Auto-save on handler completion (retrofit 2026-04-23):** When `NodeEncounter.HandlerActive` transitions from `true` → `false` (handler emits its outcome callback, post-encounter state is committed to Vehicle / Deck / Scrap / Loot state), Save observes the transition and issues an immediate write on the next main-thread tick. This is the single authoritative save point for encounter outcomes; trigger (1) above does not fire separately because `HandlerActive` gates node-resolution writes.

**Mid-combat state is in-memory only.** If the player crashes or force-quits mid-combat, they resume at the start of that combat node with clean combat state (no damage dealt, full opening draw). The vehicle loadout, deck, and Scrap balance are intact — only the in-progress combat is reset. See Player Fantasy for the design rationale (intentional soft-undo).

**MasteryState is written at one point:**

1. **After combat node resolution, AFTER the post-combat reward screen resolves** — mastery XP and rewards are committed together as one logical unit. The write fires when the reward screen closes and the map screen is about to display. This prevents orphan mastery state where XP is recorded for a combat whose rewards never landed. Non-combat nodes do not write MasteryState.

**What is NOT a write trigger:** Individual card plays during combat. Opening menus, tooltips, or deck inspection. Hovering nodes. Node entry. Reward screen display (write fires on close, not open).

---

**R3 — Atomic Write Pattern**

**All writes execute on a background `Task`. The render loop is never stalled by disk I/O.** Main thread signals write intent via a thread-safe queue; the background writer serializes, writes, and reports completion on a main-thread-pumped callback. Two exceptions to background execution:
1. The `Application.quitting` write is synchronous on the main thread (Unity's quit callback cannot await a background task).
2. The launch-time recovery chain (R6) is synchronous before the main menu displays.

The atomic write sequence:

1. Assemble all DTOs under a single `try`/`catch`. If any DTO throws during serialization, abort the write, preserve the existing live file, log `SaveAssemblyFailedException`. No partial write is ever committed.
2. Serialize the complete envelope to JSON bytes using **Newtonsoft.Json** (see below).
3. Write bytes to `Application.temporaryCachePath/[filename].tmp` — `FileStream` with `FileMode.Create`, `FileShare.None`. Call `Flush(flushToDisk: true)` before closing (maps to `FlushFileBuffers` on Windows; Mono behavior verified by code review — see OQ4).
4. Rotate the single backup (see R4) on files in `saves/`.
5. `File.Move(tmpPath, targetPath, overwrite: true)` — the 3-argument overload (available in Unity 6.3 via Mono 6.4+ / .NET Standard 2.1). Code review must verify the overload resolves at runtime before first code freeze.

Temp files live in `temporaryCachePath` — not the `saves/` directory — so Steam Cloud Sync never uploads an incomplete write.

**Serialization library:** **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json` package) with a `link.xml` preservation entry covering the `WastelandRun.Save.Dtos` namespace. IL2CPP stripping would otherwise break reflection-based deserialization at runtime. `System.Text.Json` was rejected because it requires `[JsonSerializable]` source generators on every DTO under IL2CPP; Newtonsoft's link.xml approach centralizes preservation in one file. `UnityEngine.JsonUtility` was rejected because it does not support dictionaries (required for `NodeFlagsDTO`).

**Checksum scope:** SHA-256 is computed over the **canonical serialization of the envelope excluding the `checksum` field itself**. Canonical serialization: Newtonsoft.Json with `DefaultContractResolver`, sorted property order, UTF-8 encoding, no indentation, no trailing whitespace. The `SaveSystem.ComputeEnvelopeChecksum(envelopeDto)` helper owns this — no system may compute its own checksum.

**Timestamp format:** `written_at` is ISO 8601 UTC with millisecond precision (`yyyy-MM-ddTHH:mm:ss.fffZ`). The helper `SaveSystem.NowTimestamp()` owns this.

**SHA-256 instance caching:** A single `IncrementalHash` instance is thread-local and reused across writes to avoid per-write GC allocation.

**Windows sharing violations** (antivirus, backup clients — Windows Defender can hold a file handle 500–4000ms during a real-time scan): retry up to **5 times with exponential backoff — 250ms, 500ms, 1000ms, 2000ms, 4000ms (total budget ~7.75s)** — all on the background thread. On persistent failure: log `SaveWriteFailedException`, show a non-blocking HUD notification ("Progress may not have saved — check disk access"), preserve the existing live file. Because retry is off-main-thread, no frame stall occurs.

**Disk full:** catch `IOException` with `ERROR_DISK_FULL` on the background thread, surface a non-blocking HUD warning, abort the write without touching the live file.

---

**R4 — N=1 Rolling Backup**

> **Scope-reduction risk acknowledgment:** This GDD ships with N=1 (one rolling backup) rather than the originally designed N=3. Risk accepted: the recovery chain is shortened to three candidates (live `.sav`, orphaned `.tmp`, `.bak`). In the narrow failure window where both `.sav` and `.bak` are corrupted without a valid `.tmp` present, RunState is lost and the player starts a new run. For RunState this is documented as non-blocking (`RunStateFullLoss` — see EC4). Rationale: solo-dev EA scope for a card roguelike where a corrupted run is equivalent to "start a new run"; the cost of N=3 (more rotation, more disk I/O, deeper recovery chain) outweighs the benefit at EA scope. If post-EA telemetry shows meaningful RunStateFullLoss incidence, N can be raised to 2 via the Tuning Knob without breaking save compatibility.

```
saves/runstate.sav          — live
saves/runstate.sav.bak      — single rolling backup

saves/masterystate.sav
saves/masterystate.sav.bak
```

Rotation (executed after the temp file write succeeds and passes checksum validation; the old live file must still exist at step 1). Step 3 is the rename that atomically promotes the temp file into the live slot:
1. Delete `*.sav.bak` if present.
2. Rename `*.sav` → `*.sav.bak` (prior live data is now the backup).
3. Move `[tempPath].tmp` → `*.sav` (new data is now the live file).

Every DTO is wrapped in an envelope:

```
{
  "envelope_version": 1,
  "system": string,
  "schema_version": int,
  "written_at": string,    // ISO 8601 UTC, ms precision
  "checksum": string,      // SHA-256 hex of canonical serialization of envelope minus this field
  "payload": object
}
```

`envelope_version` allows future envelope-level changes to be detected even if payload schema is unchanged. On load: verify `envelope_version == 1`; higher version = incompatible (treated same as higher payload schema version — see R5).

---

**R5 — Schema Versioning (EA: per-system tag, no migration chain)**

Each system's DTO exposes two constants:
- `const string SystemId` — stable identifier, never changes post-ship.
- `const int SchemaVersion` — increments by 1 on each breaking DTO change.

**Two-tier policy — EA mode vs. permanent semantic.** These are distinct rules; conflating them caused a prior-review trap:

**Permanent semantic (the design intent, documented now for traceability):**
- **Lower** `schema_version` than current code → save is older. Migration candidate; the forward migration chain runs and upgrades the payload.
- **Higher** `schema_version` OR `envelope_version` than current code → save is newer (player downgraded). Reject with blocking dialog.

**EA-mode policy (this release only — no migration runtime exists yet):**
- **Any** `schema_version` mismatch (higher OR lower) → treated as incompatible; skip to next candidate. The "lower = incompatible" branch is an EA-specific simplification, not the permanent semantic.
- **Higher** `envelope_version` → reject (same as permanent semantic).
- If all candidates are incompatible: show a user-facing blocking dialog (see EC5).

**Post-EA migration introduction:** The first content patch that changes a DTO introduces the migration chain runtime. When that happens: (a) a new section R5a will be authored in this GDD; (b) the EA-mode "lower = incompatible" branch is REMOVED and replaced with the permanent semantic (lower → migrate forward); (c) the corresponding EA-only AC (see Schema Versioning AC section) is inverted and marked as superseded. **This is a scheduled AC inversion on the first content patch** — the EA AC explicitly flags this so the inversion is not silently missed.

**Pre-ship DTO churn:** during development, if a DTO changes, nuke local saves. This is acceptable for solo-dev EA.

If the file's `schema_version` or `envelope_version` is **higher** than the current code (player downgraded): treat the file as incompatible, skip to next candidate. If all candidates are from a newer version: surface a user-facing blocking dialog — *"Save file was created with a newer version of Wasteland Run. Please update the game."*

---

**R6 — Crash Recovery**

On every launch, before the main menu, Save validates all candidates per category synchronously. Recovery chain (tried in order, for each category independently):

1. `*.sav` — validate envelope + checksum → use if valid
2. `*.tmp` in `temporaryCachePath` — validate → if valid, move to `*.sav` and use (orphaned temp from interrupted write)
3. `*.sav.bak` → use if valid (log `SaveBackupRecovery`)
4. No valid file:
   - **RunState**: no run in progress — present main menu with "New Run" only. Correct behavior. Log `RunStateFullLoss`.
   - **MasteryState**: show a **blocking user dialog**: *"Mastery progress could not be loaded — this indicates save file corruption across all backups. Please report this to support. Continue with a fresh mastery state, or quit to attempt manual recovery?"*. Do NOT silently initialize. Log `MasteryStateFullLoss` regardless of player choice.

The two categories recover independently. Corrupted RunState does not block MasteryState.

**Chain ordering preference:** If `.sav` validates, the `.bak` is NEVER promoted. The chain exits on the first valid candidate. `SaveBackupRecovery` is logged only when a backup is used.

---

**R7 — Save File Location**

| Path | Contents |
|---|---|
| `Application.persistentDataPath/saves/` | Live saves + single backup (`*.sav`, `*.sav.bak`) |
| `Application.temporaryCachePath/` | Temp files only (`*.tmp`) — lives in a separate OS tree (`%LocalAppData%/Temp/...`) from `persistentDataPath`; Steam Cloud never sees it because Auto-Cloud include-paths do not reach this tree |

On Windows/Steam: `C:\Users\[username]\AppData\LocalLow\[CompanyName]\[ProductName]\saves\`

`CompanyName` and `ProductName` in Unity Project Settings must be finalized before EA and never changed — changing them silently relocates `Application.persistentDataPath` and orphans all saves. The EA baseline is locked in `.claude/docs/technical-preferences.md` under "Project Identity" (to be added before EA ship). A CI check (see EC10) compares `ProjectSettings/ProjectSettings.asset` against this baseline.

**Steam Cloud:** Steam Cloud Auto-Cloud uses an **include-path allowlist**, not an exclude-list. Configure Steamworks partner portal Auto-Cloud with a single root path: `%LocalAppDataLow%/[CompanyName]/[ProductName]/saves/` (matched on Windows; equivalent resolution on other platforms if added later). Only files under this path are uploaded; all other trees (including `temporaryCachePath` which resides under a different root, typically `%LocalAppData%/Temp/[CompanyName]/[ProductName]/`) are never considered by Steam Cloud. **No exclusion configuration exists or is needed** — anything outside the include-path is implicitly ignored. A developer setting up Steam Cloud should verify the Auto-Cloud dashboard shows exactly one root path pointing at `saves/`. Cloud conflict resolution is **last-write-wins** — accepted as a known limitation (see OQ3) and documented on the store page that multi-machine play may cause progress inversions. A merge strategy is deferred to post-EA.

---

**R8 — DTO Schemas (retrofit 2026-04-23 — Scrap Economy, Loot & Reward, Node Encounter)**

Each DTO is owned by the system listed in Interactions; Save only composes them into the RunState envelope.

```csharp
// Scrap Economy — new in retrofit; replaces trivial Scrap-int-only shape
public sealed class ScrapStateDTO
{
    public const string SystemId       = "scrap-economy";
    public const int    SchemaVersion  = 1;
    public int  CurrentScrap;
    public bool FreeValveConsumedThisVisit;  // cleared on Chopshop exit; persisted so mid-visit save/load preserves the flag
}

// Loot & Reward — new in retrofit
public sealed class LootStateDTO
{
    public const string SystemId       = "loot-reward";
    public const int    SchemaVersion  = 1;
    public Dictionary<SlotType, int> PartDropCooldown;  // nodes-until-eligible per slot
    public int RarePityCounter;                         // consecutive card offers without a Rare
    public int RewardTableSeedOffset;                   // optional — for Loot RNG stream isolation
}

// Node Encounter — new in retrofit
public sealed class NodeEncounterStateDTO
{
    public const string SystemId       = "node-encounter";
    public const int    SchemaVersion  = 1;
    public bool   HandlerActive;       // true iff a handler has begun and not yet emitted its outcome callback
    public string? CurrentHandlerId;   // "Combat" | "EliteCombat" | "Merchant" | "Chopshop" | "Event" | "Rest" | "Haven" | null
    // Per-handler transient state is NOT persisted — mid-encounter crash resumes pre-encounter (see "Handler save-block" above)
}
```

**Default DTOs on run start:** Save initializes all three DTOs to defaults on new-run creation (empty dictionaries, counters = 0, `FreeValveConsumedThisVisit = false`, `HandlerActive = false`, `CurrentHandlerId = null`). This closes EC-LR4 (null LootStateDTO boundary) and ensures the handler save-block has a concrete initial state to read.

**Schema migration:** If any of these DTOs adds or renames a field in a future release, `SchemaVersion` must increment per R5. The EA-mode policy applies uniformly.

### States and Transitions

**RunState lifecycle:**

| State | Description | Entry | Exit |
|---|---|---|---|
| **No Run** | No `runstate.sav` exists. Main menu shows "New Run" only. | Run complete (win/loss) — `runstate.sav` + `.bak` deleted | Player starts a new run |
| **Run Active** | `runstate.sav` exists. Main menu shows "Continue Run". | New run started (first node resolution write) | Run complete (win/loss) |
| **Node Resolution Snapshot** | RunState written after node fully resolves. | Node fully resolved; map screen displayed | Next node resolution write or periodic flush |
| **Periodic Idle Flush** | RunState written every 30s on map screen if dirty. | Map screen dwell timer ≥ 30s AND in-memory state dirty | Timer resets after write; dirty flag clears |

**Run end:** On run completion (Haven reached or vehicle destroyed), RunState and its backup are deleted from disk. MasteryState is updated with the run's final XP award **before** RunState deletion. A run ends only through deliberate game logic — not through any file operation triggered by the player closing the application.

---

**MasteryState lifecycle:**

| State | Description | Entry | Exit |
|---|---|---|---|
| **Initialized** | All XP = 0, levels = 1, no unlocks. Created on first launch if no file exists. | First launch ever, or user-consented full-loss recovery | Updates in place |
| **Updated** | XP incremented, level recalculated, unlocks flagged. Written after every combat resolution (post-rewards). | After every combat node, AFTER reward screen closes | N/A |

### Interactions with Other Systems

| System | What They Send to Save | What Save Sends Back | DTO Owner |
|---|---|---|---|
| **Card System** | `List<string>` of `CardId` for deck, discard, exhausted states | Same lists on load | Card System owns `DeckStateDTO` |
| **Vehicle & Part System** | Part loadout per slot (part ID, damage state, installed flag, `CombatsSurvived` counter per slot, fuel container {CurrentFuel, FuelCap}) | Same on load | Vehicle & Part System owns `VehicleStateDTO` (includes per-slot `CombatsSurvived` field and `FuelStateDTO` sub-record — V&P R10/R11 retrofit) |
| **Node Map System** | Current node index, traversal list, branching path, per-node runtime flags, `IsCommitInProgress` bool (see below) via `INodeMapSerializable` facade | Same on load; Save consults `IsCommitInProgress` before every write | Node Map System owns `NodeMapStateDTO` + implements `INodeMapSerializable` (retrofit 2026-04-23) |
| **Scrap Economy System** | Current Scrap balance + `FreeValveConsumedThisVisit` visit-scoped bool | Same on load | Scrap Economy System owns `ScrapStateDTO` (retrofit 2026-04-23 — includes `FreeValveConsumedThisVisit` field cleared on Chopshop exit) |
| **Loot & Reward System** | `LootStateDTO` — `PartDropCooldown` per slot (int map), `RarePityCounter` (int), any cached reward-table seeds | Same on load; default DTO (empty cooldowns, counter=0) initialized on run start per EC-LR4 | Loot & Reward System owns `LootStateDTO` (retrofit 2026-04-23 — new serializer added) |
| **Node Encounter System** | `HandlerActive: bool` + `CurrentHandlerId: string?` (for save-block gate); on successful encounter completion, clears both | Save consults `HandlerActive` before every write (see "Handler save-block" in Write Triggers) | Node Encounter System owns `NodeEncounterStateDTO` (retrofit 2026-04-23 — only `HandlerActive` + `CurrentHandlerId` persisted; per-handler state stays in-memory) |
| **Meta Progression System** | Chassis mastery XP + level + unlock flags per chassis | Same on load | Meta Progression System owns `MasteryStateDTO` |
| **Addressables (asset system)** | N/A — see Edge Case EC11 | N/A | Missing-key handling owned by loading systems (Card, Vehicle), not Save |
| **Card Combat System** | Nothing — mid-combat state is in-memory only. Combat reads from loaded RunState at node entry. Save-blocked during combat via NE's `HandlerActive` gate (no separate CC flag needed). | Nothing during combat | No DTO |

### INodeMapSerializable Facade (retrofit 2026-04-23)

```csharp
public interface INodeMapSerializable
{
    NodeMapStateDTO ToDto();
    void            FromDto(NodeMapStateDTO dto);
    bool            IsCommitInProgress { get; }  // true during the commit pipeline (Node Map C3.5 steps 1-9)
}
```

Save consults `IsCommitInProgress` before **every** write operation (including the 30s periodic flush and explicit menu-triggered saves). If `true`, the write is deferred until `IsCommitInProgress` returns `false` (fires the dirty-flag timer again on transition to false). This prevents mid-commit save corruption where `CurrentNodeIndex` has advanced but `CurrentFuel`/`StormFrontX` have not yet.

### Node Encounter Handler Save-Block (retrofit 2026-04-23)

When an NE handler is active (`HandlerActive == true`), Save behavior changes:

1. **All writes are blocked** — periodic flush is deferred, explicit menu-saves show a "Cannot save during encounter" inline message, node-resolution writes don't fire (they fire on `Idle` transition instead).
2. **Auto-save fires on `HandlerActive == false` transition** — when an encounter completes (handler emits callback), the save system observes the transition and issues a write with the full post-encounter state. This is the single authoritative save point for encounter outcomes.
3. **Mid-encounter crash recovery** — if the game crashes while `HandlerActive == true`, on load the save shows the pre-encounter state (because no save was written during the encounter). The player loses the encounter's progress but retains deck/vehicle/Scrap state from before the encounter. This is the explicit EA tradeoff vs. full mid-encounter resumption (see Save OQ — mid-encounter save point policy, and NE GDD OQ-NE equivalent).

**Handler save-block applies to all 7 NE handlers uniformly** (Combat, EliteCombat, Merchant, Chopshop, Event, Rest, Haven); the only exception is Haven, which is run-terminal — its completion triggers run-end delete, not a save.

**Save system owns:** file I/O, atomic write, background task orchestration, backup rotation, validation, recovery chain, checksum helper, timestamp helper. **Save system does NOT own:** any DTO schema, any gameplay value, any serialization logic for individual systems, any Addressable key resolution.

**Traversal history bound:** `NodeMapStateDTO.traversal_list` is specified as a `List<int>` of node indices only — no per-node metadata, no embedded outcomes. Per-node runtime state that must persist belongs in `NodeFlagsDTO`. This keeps RunState file size bounded at F3's estimate across content additions.

## Formulas

### F1 — RunState Write Frequency per Run

```
RunStateWrites = NodeCount + PeriodicFlushes + QuitWrites
```

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| `NodeCount` | N | int | 16–22 (design target) | Total nodes traversed — confirmed by Node Map System GDD |
| `PeriodicFlushes` | P | int | 0–(30 × MapScreenDwellSessions) — typical 0–10 | Number of 30s idle flushes that fire during map-screen dwell. Capped at 30 per continuous dwell session (see R2.2). Typical player: ~0.5 per node transition (map inspection). |
| `QuitWrites` | Q | int | 0–1 | 1 if the player quits mid-run cleanly (`Application.quitting` fires); 0 otherwise |

**Output range under typical play:** 16–33 writes per run (16 nodes, 0 flushes, 0 quits = 16; 22 nodes, 10 flushes, 1 quit = 33).

**Pathological worst case (single idle dwell capped at 30 flushes):** 22 + 30 + 1 = 53 writes. The per-dwell cap (R2.2) prevents unbounded flushing on long idle sessions.

**Example:** 20-node run, 5 periodic flushes (player reads map carefully), no mid-run quit: `RunStateWrites = 20 + 5 + 0 = 25 writes`.

---

### F2 — MasteryState Write Frequency per Run

```
MasteryStateWrites = CombatNodeCount
```

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| `CombatNodeCount` | C | int | ~14 (placeholder — confirmed by Node Map System GDD) | Normal + Elite combat nodes per run |

**Output range:** ~14 writes per run. Non-combat nodes (Merchant, Chopshop, Event, Treasure) do not write MasteryState.

**Edge case:** `CombatNodeCount = 0` (hypothetical all-event run) → 0 MasteryState writes during the run. Final run-end MasteryState write still fires as part of the run-end sequence.

---

### F3 — On-Disk Storage Budget

```
TotalSaveBytes ≈ (RunStateFileSize × 2) + (MasteryStateFileSize × 2) + TmpOverhead
```

**Estimated file sizes:**

| File | Estimated Size | Basis |
|---|---|---|
| `runstate.sav` | 3–6 KB | ~25 CardIds × ~25 chars + vehicle loadout + Scrap balance + map position + node flags |
| `masterystate.sav` | 0.5–1 KB | 2 chassis × (XP int + level int + ~30 unlock flag booleans) |
| `.tmp` transient | Up to 6 KB peak | Exists only during a write; bounded by largest payload size |

**Output range:** ~7–14 KB persistent on disk (2 files × 2 categories, N=1 backup) + up to 7 KB transient `.tmp`. Well within any per-user storage budget.

> **⚠️ Sequencing constraint:** F1 and F2 use `NodeCount` and `CombatNodeCount` as placeholders. Both must be confirmed by the Node Map System GDD before implementation. The storage budget (F3) is negligible at all plausible node counts.

## Edge Cases

**EC1 — Run Abandon: Quit Mid-Encounter vs. Quit on Map Screen**

Two distinct quit contexts:

- **Quit mid-encounter (mid-combat, mid-event, mid-merchant):** The live `runstate.sav` reflects the post-resolution snapshot of the last *completed* node. The quit write (if `Application.quitting` fires) or periodic flush captures the same state. Resume returns the player to the start of the current (uncompleted) node — they replay the encounter from scratch. The vehicle loadout, deck, and Scrap balance from all prior completed nodes are intact. **Mid-combat quit is an intentional soft-undo** — see Player Fantasy.
- **Quit on the map screen (between nodes):** The live `runstate.sav` is the post-resolution snapshot (rewards fully applied). Resume returns the player to the map screen with full current state intact.

In neither case is the run treated as a loss.

---

**EC2 — Sharing Violation: Antivirus or Backup Client Has the Save File Open**

`FileStream` with `FileShare.None` prevents a second writer. If the `File.Move` rename step fails due to a sharing violation: retry up to 5 times with exponential backoff (250ms, 500ms, 1000ms, 2000ms, 4000ms — ~7.75s total) on the background thread. The budget is sized to clear typical Windows Defender real-time scan hold windows (500–4000ms). On persistent failure: log `SaveWriteFailedException`, show a non-blocking HUD notification. The existing live file is not modified.

---

**EC3 — Disk Full During Write**

The temp file write (`FileStream`) throws `IOException` with `ERROR_DISK_FULL`. Catch specifically on the background thread: abort the write, surface a non-blocking HUD warning, preserve the existing live `*.sav`. Backup rotation has NOT run at this point (rotation fires only after the temp file passes checksum validation) — the live `.sav` and `.sav.bak` are both intact.

**Run-end variant:** At run completion, if the MasteryState write throws `ERROR_DISK_FULL`: do NOT delete `runstate.sav`. Surface a blocking user dialog: *"Could not save run completion — disk is full. Free some space and click OK to retry, or Cancel to resume the run."* This prevents the orphan state where RunState is deleted but MasteryState never committed.

---

**EC4 — All Recovery Candidates Corrupted**

If `*.sav`, `*.tmp`, and `*.sav.bak` all fail validation:

- **RunState:** Present main menu as if no run exists. Log `RunStateFullLoss`. Non-blocking. The player starts fresh.
- **MasteryState:** Show a **blocking user dialog**: *"Mastery progress could not be loaded — this indicates save file corruption across all backups. Please report this to support. Continue with a fresh mastery state, or quit to attempt manual recovery?"*. Log `MasteryStateFullLoss` on either choice. Do NOT silently reset.

---

**EC5 — Game Downgraded: File from Newer Schema or Envelope Version**

If every recovery candidate has `schema_version` OR `envelope_version` higher than current code: show a blocking error — *"Save file was created with a newer version of Wasteland Run. Please update the game."* No files are deleted — the player can update the game to recover.

---

**EC6 — Save Directory Missing on First Launch**

`Application.persistentDataPath/saves/` may not exist on a fresh install. `Directory.CreateDirectory(savePath)` is idempotent — called unconditionally on every write.

---

**EC7 — MasteryState Double-Award on Node Re-entry**

For EA, the Node Map System is unidirectional — nodes cannot be re-entered — so this edge case cannot occur. **Traceability note:** If any future Node Map revision adds replay-encounter or backtrack mechanics (e.g., a "fight a copy" event), this GDD's AC suite is incomplete until the idempotency guard (`mastery_xp_awarded_for_node_[index]: bool` tracked in `RunState`) is added. The `/propagate-design-change` skill must be run when Node Map is revised to surface this dependency.

---

**EC8 — Periodic Flush During Node Entry Transition**

If the 30s periodic flush timer fires while the player is mid-transition into a new node (animation playing, node content loading), the flush is deferred until the player is observably idle on the map screen OR fully engaged in node content. Concrete rule: periodic flush only fires when the game state is `MapScreen.Idle`. It does not fire during `MapScreen.Transitioning`, `Node.Loading`, or any node-active state.

If the player quits during a transition, the quit write (if it fires) captures the last committed state — i.e., the state from BEFORE node entry.

---

**EC9 — Temp File Orphaned on Previous Launch**

If `Application.temporaryCachePath/[filename].tmp` exists on launch: this indicates the rename step did not complete. The recovery chain (R6 step 2) validates the temp file's checksum and promotes it to `*.sav` if valid. If invalid: delete the temp file and proceed to `.bak`.

---

**EC10 — `CompanyName` or `ProductName` Changed After EA Ship**

Changes to these Unity Project Settings values silently relocate `Application.persistentDataPath`. CI check (see AC) compares `ProjectSettings/ProjectSettings.asset` against the baseline committed in `.claude/docs/technical-preferences.md`. Mismatch = build fails.

---

**EC11 — Broken Addressables Key on Load**

A loaded save may contain a `CardId` (or part ID, or other Addressables key) that no longer resolves in the current build (the asset was removed or renamed in a patch). Save & Persistence is NOT responsible for asset-key validation — it faithfully returns the stored keys. The loading system (Card System, Vehicle & Part System) is responsible for detecting unresolvable keys and applying a fallback policy (e.g., drop the card with an in-game notification, substitute a default part). This GDD flags this as a cross-system concern; specific handling belongs in the loading system's GDD.

---

**EC12 — Hard-Kill: Task Manager, OOM, Driver Crash**

These failure modes do NOT trigger `Application.quitting`. The design explicitly accepts this — the periodic idle flush (R2.2) bounds data loss to at most the flush interval (30s of map-screen dwell). If hard-kill occurs during a node resolution (before the post-resolution write), the player resumes at the start of that node (see EC1). If hard-kill occurs during map-screen dwell within 30s of the last flush, at most 30s of state is lost — which in the map-screen context means no state is lost (map dwell has no meaningful state delta between flushes).

---

**EC13 — Run-End Crash Window Between MasteryState Write and RunState Deletion**

Run completion sequence:
1. Assemble and write final MasteryState (includes run's XP award).
2. Delete `runstate.sav` and `.sav.bak`.

If crash occurs between step 1 (success) and step 2: next launch sees both a valid `masterystate.sav` with the run's XP applied AND a valid `runstate.sav` showing an active run. Resolution: on launch, if a run's final state in `runstate.sav` indicates "Haven reached" or "vehicle destroyed" (run-end markers), the launch-time recovery skips "Continue Run" and deletes `runstate.sav` instead. Run-end markers must be written to RunState at the moment of win/loss, before step 1.

If crash occurs during step 1 (disk full): see EC3 run-end variant.

## Dependencies

### Systems This GDD Depends On

Save & Persistence is a **Foundation-layer system** with no upstream dependencies on other game-logic systems. It depends on Unity Engine APIs (`Application.persistentDataPath`, `Application.temporaryCachePath`, `Application.quitting`, `FileStream`, `File.Move`) and the Newtonsoft.Json Unity package.

---

### Systems That Depend On This GDD

| System | What They Need From Save & Persistence | Dependency Nature |
|---|---|---|
| **Card System** | Storage for `IsFreeValveApplied: bool` (per Chopshop node); deck/discard/exhausted serialization contract (`DeckStateDTO` using `CardId` strings); `CardId` key resolution fallback policy (EC11) | Hard |
| **Card Combat System** | `CombatSnapshotDTO` round-trip — snapshot written on PlayerTurn entry (autosave boundary), discarded on `CombatEnded`; used only for crash-recovery mid-combat | Hard |
| **Enemy System** | No direct DTO (enemy combat runtime state is regenerated from `EnemyDefinitionSO` + seed on combat entry); `CombatEndedPayload` shape is contract-visible to Save via Card Combat's `CombatSnapshotDTO` | Soft (contract-only) |
| **Status Effects** | `StatusInstance[]` array schema embedded in `VehicleStateDTO` and `CombatSnapshotDTO` (4-field struct: `Type`, `Stacks`, `RemainingDuration`, `SourceId`) | Hard |
| **Vehicle & Part System** | `VehicleStateDTO` structure; `InstalledPartDTO` (includes `CombatsSurvived` counter per BLOCKER-4 retrofit); part-key resolution fallback (EC11); `INodeMapSerializable` fuel-cap persistence | Hard |
| **Scrap Economy System** | `ScrapStateDTO` — currently trivial (single `int Balance`); flagged as schema-sensitive if per-chassis currency is added | Currently soft, schema-sensitive |
| **Node Map System** | `NodeMapStateDTO` structure for map position and bounded traversal list; `NodeFlagsDTO` for all per-node runtime flags; run-end marker field; unidirectional constraint (EC7); commit-time Frame-HP sampling tuple persisted so mid-map reload restores the same fuel-cost preview | Hard |
| **Node Encounter System** | **No direct DTO.** Per 2026-04-24 C1 regeneration: NE's handler-state model is **subscribe-not-own** — handlers are stateless functions that subscribe to `OnEncounterEntered` / `OnEncounterResolved` events emitted by Node Map. Mid-encounter state (e.g., dialog choice highlighted but unconfirmed) lives transiently in UI and is **never persisted** — Save's autosave boundary is node-commit, not mid-handler. If the player quits mid-encounter, the handler re-fires clean on reload using Node Map's committed state. The `HandlerActive` flag in `NodeMapStateDTO` is a **save-block** signal (Save refuses autosave while `HandlerActive == true`) rather than a handler-state field. | Soft (behavioral contract only) |
| **Loot & Reward System** | `LootStateDTO` — `RarePityCounter: int`, `PartDropCooldown: Dictionary<SlotType, int>`; passive-serializer model (L&R mutates DTO in-place, Save's dirty-flag detects and flushes on its own cadence) | Hard |
| **Meta Progression System** | `MasteryStateDTO`; `IMasteryStateSerializable`; write trigger timing (post-reward-screen); `MasteryStateFullLoss` dialog specification for UX team | Hard |

**Note on DTO ownership:** Each dependent system owns its own DTO. Save & Persistence defines the envelope contract, the interfaces, the `SaveSystem.ComputeEnvelopeChecksum` helper, and `SaveSystem.NowTimestamp` helper. The payload schema is owned entirely by the implementing system.

---

### Dev Tooling Dependencies

Save & Persistence requires the following dev tools for QA execution (see Acceptance Criteria). These must be authored alongside implementation:

- `SaveSystem.DebugWriteCount` — instrumented counter exposed in dev console.
- `SaveSystem.InjectFault(FaultType)` — fault-injection hook for `CorruptTemp`, `CorruptSav`, `CorruptBak`, `SharingViolation`, `DiskFull`, `KillBetweenTmpAndMove`.
- `SaveSystem.SimulateSlowWrite(delayMs)` — forces write to take ≥ delayMs for EC8-style ordering tests.
- `/dev/corrupt-save [runstate|mastery] [sav|bak|tmp]` — byte-flip a named file.
- `/dev/fresh-install-sim` — launches with `persistentDataPath/saves/` deleted.
- Sequence-number log events: every save write logs `SaveSystem.Write #N complete [timestamp] [file] [bytes]`. Every load logs `SaveSystem.Load #N [file] [validates|fails] [reason]`.

These dev tools are engineering-owned but QA-facing. Block Save implementation gate-pass on these being available.

---

### ADRs Referenced

None. Save & Persistence is pure infrastructure. Systems that own DTOs may reference ADR-0001 (or future ADRs) in their own DTOs.

---

### Bidirectional Consistency Check

Regenerated 2026-04-24 against current 10-GDD MVP set and NE handler-state subscribe model (C1 concern closure):

| This GDD lists as dependent | Their GDD must list this system | Status |
|---|---|---|
| Card System | "Depends on: Save & Persistence System" | ✅ Confirmed |
| Card Combat System | `CombatSnapshotDTO` + autosave boundary referenced in R2/R13 | ✅ Confirmed |
| Enemy System | Contract-only; no DTO; `CombatEndedPayload` schema agreement | ✅ Confirmed (soft) |
| Status Effects | `StatusInstance[]` schema embedded in VehicleStateDTO / CombatSnapshotDTO | ✅ Confirmed |
| Vehicle & Part System | `VehicleStateDTO`, `InstalledPartDTO` (with `CombatsSurvived` per BLOCKER-4) | ✅ Confirmed |
| Scrap Economy System | `ScrapStateDTO` | ✅ Confirmed |
| Node Map System | `NodeMapStateDTO`, `NodeFlagsDTO`, unidirectional constraint | ✅ Confirmed |
| Node Encounter System | Behavioral contract only (subscribe-not-own; `HandlerActive` save-block flag) | ✅ Confirmed (soft) |
| Loot & Reward System | `LootStateDTO` (RarePityCounter + PartDropCooldown) | ✅ Confirmed |
| Meta Progression System | "Depends on: Save & Persistence System" | ⏳ Not yet authored (deferred post-MVP) |

## Tuning Knobs

| Knob | Location | Current Value | Safe Range | Gameplay Effect | What Breaks at Extremes |
|---|---|---|---|---|---|
| Backup count (N) | `SaveSystem` constant | 1 | 0–2 | Number of rolling backups beyond live. Higher = more recovery options at cost of disk space + rotation cost. | N=0: corruption on a single write = run loss. N>2: negligible benefit, more rotation cost. |
| Periodic flush interval | `SaveSystem` constant | 30s | 15s–120s | How often map-screen state is flushed. Bounds hard-kill data loss. | Too short: excess writes on long map dwell. Too long: more data loss on hard-kill. |
| Sharing violation retries | `SaveSystem` constant | 5 | 3–7 | How many times to retry before failing a write. Sized to clear Windows Defender real-time scan hold windows. | Too low (≤3 at 100ms delay): legit Defender scans cause false failures — the prior default (3×100ms=300ms) was insufficient. Too high: extends failure latency (background only). |
| Sharing violation retry spacing | `SaveSystem` constant | Exponential: 250, 500, 1000, 2000, 4000ms (~7.75s total) | Total budget 5–15s | Exponential backoff between retries (background thread). | Total <5s: insufficient for real-world Defender holds. Total >15s: extends failure detection latency unacceptably. |
| MasteryState write timing | Design constraint | After reward screen close | Not adjustable | When mastery XP is written. | **Not a tuning knob** — moving it pre-reward creates orphan-state crash window (see EC13 rationale). |
| `CompanyName` / `ProductName` | Unity Project Settings | Baseline in `.claude/docs/technical-preferences.md` | **Immutable post-EA** | Determines `persistentDataPath`. | Changing post-EA = silent data loss for existing players. |

**Scope-reduction note:** This GDD was revised 2026-04-21 (first revision) to cut backups from N=3 to N=1, defer schema migrations to first content patch, and document Steam Cloud last-write-wins as an accepted limitation (see OQ3). A second revision the same day addressed re-review findings: F1 flush cap added, R3 sharing-violation backoff raised to 5×exponential (7.75s total), R5 split into EA-mode vs. permanent semantic, R7 Steam Cloud rewritten for include-path semantics, R4 N=1 risk acknowledgment added, and a Design Tension paragraph added to Player Fantasy. Rationale in review log.

## Visual/Audio Requirements

Minimal — Save & Persistence is infrastructure. Two user-facing surfaces require art/audio spec:

1. **Save indicator** — brief, non-intrusive visual feedback on successful writes (e.g., small disk icon in HUD corner, 500ms fade). Informs the player "your progress is saved." Deferred to HUD UX spec.
2. **Save error notification** — non-blocking HUD toast for `SaveWriteFailedException` and disk-full warnings. Spec shared with general HUD error notification system.

Blocking dialogs (MasteryStateFullLoss, schema-newer, run-end disk-full) use the general blocking-dialog UX pattern — no save-specific art required.

## UI Requirements

Minimal — handled by general UX components:

1. **"Continue Run" vs "New Run" main menu gating** — driven by `runstate.sav` existence + validity. Main menu UX owns the button logic; Save system provides `SaveSystem.HasValidRunState: bool`.
2. **Save indicator** — see Visual/Audio.
3. **Blocking dialog patterns** — reuse general dialog component.

Detailed UI spec deferred to Main Menu UX spec and HUD UX spec.

## Acceptance Criteria

### Save Scope

- [ ] **[Unit test]** **GIVEN** the gameplay assembly is compiled, **WHEN** `InterfaceExclusion_test` runs, **THEN** no type is found that implements both `IRunStateSerializable` AND `IMasteryStateSerializable`. Test fails with the offending type name on any violation.

---

### Write Triggers

- [ ] **[Integration test — requires DebugWriteCount]** **GIVEN** a player has completed a node (all content resolved, rewards applied), **WHEN** the map screen is displayed, **THEN** a `runstate.sav` write is logged with a sequence number and the payload reflects the post-reward state (updated Scrap, deck, traversal list).
- [ ] **[Integration test — requires log sequencing]** **GIVEN** a player selects a node, **WHEN** node content begins, **THEN** no write event appears in the log between the player's click and the node-active state. The most recent write event's sequence number matches the previous node's resolution.
- [ ] **[Integration test — requires log sequencing]** **GIVEN** a combat node has just resolved AND the reward screen is closing, **WHEN** inspecting the event log, **THEN** the `MasteryState write complete` event appears AFTER the `RewardScreen.Closed` event and BEFORE the `MapScreen.Displayed` event.
- [ ] **[Integration test]** **GIVEN** a player completes a non-combat node, **WHEN** the node resolves, **THEN** no `masterystate.sav` write event is logged for that transition.
- [ ] **GIVEN** the player is mid-combat, **WHEN** the player force-quits, **THEN** on next launch the combat node is at its initial state (no damage dealt, no cards played, full opening draw). Vehicle loadout, deck, and Scrap from prior completed nodes are intact. (Documented as intentional soft-undo.)
- [ ] **GIVEN** a player is on the map screen for 30+ seconds with dirty state, **WHEN** the player hard-kills the process (Task Manager), **THEN** on next launch the game resumes at the map screen with state no older than the most recent periodic flush. Log `PeriodicFlushRecovery`.
- [ ] **[Integration test]** **GIVEN** a player is mid-node-transition, **WHEN** the 30s periodic flush timer elapses, **THEN** no write event is logged until the game returns to `MapScreen.Idle`.

---

### Run Lifecycle

- [ ] **[Integration test — requires log sequencing]** **GIVEN** a player reaches Haven (win) or has their vehicle destroyed (loss), **WHEN** the run-end sequence executes, **THEN** the log shows: `MasteryState write complete` → `RunState files deleted`. RunState deletion does NOT precede MasteryState write.
- [ ] **GIVEN** `runstate.sav` and `.bak` have been deleted after run completion, **WHEN** main menu displays, **THEN** only "New Run" is shown.
- [ ] **GIVEN** `runstate.sav` exists and validates, **WHEN** main menu displays, **THEN** "Continue Run" is available.
- [ ] **GIVEN** a run completes AND disk is full when writing final MasteryState, **WHEN** the write throws `ERROR_DISK_FULL`, **THEN** a blocking dialog appears AND `runstate.sav` is not deleted AND the player can free space and retry.
- [ ] **GIVEN** a crash occurs between MasteryState write success and RunState deletion, **WHEN** the game relaunches, **THEN** `runstate.sav` is detected as run-ended (Haven-reached or vehicle-destroyed marker present), `runstate.sav` is deleted at launch, and "Continue Run" is not offered.

---

### Atomic Write

- [ ] **[Code review]** **GIVEN** a save write executes, **WHEN** the write sequence is inspected, **THEN** the order is: assemble DTOs (abort on any exception) → serialize envelope → write `.tmp` to `temporaryCachePath` → validate `.tmp` checksum → rotate `.bak` → `File.Move(tmp, target, overwrite: true)`.
- [ ] **[Integration test — requires InjectFault(KillBetweenTmpAndMove)]** **GIVEN** the fault is active, **WHEN** a save write runs, **THEN** the process dies between `.tmp` write and `File.Move`. On relaunch: `runstate.sav` (the prior live file) is intact, the orphaned `.tmp` is present in `temporaryCachePath`, and recovery promotes the `.tmp` to `.sav`.
- [ ] **[Integration test]** **GIVEN** a save write succeeds, **WHEN** `temporaryCachePath` is inspected after completion, **THEN** no `.tmp` file remains.
- [ ] **[Code review]** **GIVEN** writes are issued, **WHEN** the calling thread is inspected, **THEN** node-resolution writes execute on a background thread and do NOT block the main thread. Only the `Application.quitting` write and launch-time recovery run synchronously.
- [ ] **[Integration test]** **GIVEN** any DTO serializer throws an exception mid-assembly, **WHEN** the write runs, **THEN** no `.tmp` file is created (or the `.tmp` is deleted before rotation), the live `.sav` is unchanged, and `SaveAssemblyFailedException` is logged.

---

### Rolling Backup

- [ ] **[Integration test]** **GIVEN** `runstate.sav` and `runstate.sav.bak` both exist, **WHEN** a save write completes, **THEN** the prior `.bak` is deleted, the prior `.sav` is renamed to `.bak`, and the new data becomes `.sav`.
- [ ] **[Integration test]** **GIVEN** only `runstate.sav` exists (no `.bak`), **WHEN** a save write completes, **THEN** `runstate.sav` is renamed to `.bak` and the new data becomes `.sav`.
- [ ] **[Integration test — requires corruption tool]** **GIVEN** a `.sav` or `.bak` file has its payload corrupted, **WHEN** the save system loads that file, **THEN** checksum validation fails and the recovery chain proceeds to the next candidate. `CorruptionDetected` is logged with the file path and expected-vs-actual checksum.
- [ ] **[Unit test]** **GIVEN** a known envelope (fixture), **WHEN** `SaveSystem.ComputeEnvelopeChecksum` is called, **THEN** the returned SHA-256 matches the fixture's expected hex string exactly. Verifies canonical serialization is deterministic.

---

### Schema Versioning (EA)

- [ ] **[Integration test — EA ONLY; SCHEDULED FOR INVERSION]** **GIVEN** a save file with `schema_version` lower than current code, **WHEN** the file is loaded, **THEN** it is treated as incompatible and the recovery chain proceeds. (EA has no migration chain — any version mismatch is incompatible.) **Inversion trigger:** when the first post-EA content patch adds a migration runtime (R5a), this AC must be inverted to "lower schema_version is a migration candidate and deserializes forward." Add a CI comment referencing this file + AC number at the migration PR.
- [ ] **[Integration test]** **GIVEN** a save file with `envelope_version` higher than current code, **WHEN** the file is loaded, **THEN** it is skipped as incompatible.
- [ ] **[Integration test]** **GIVEN** all recovery candidates have `schema_version` or `envelope_version` higher than current code, **WHEN** the chain is exhausted, **THEN** a blocking dialog displays: *"Save file was created with a newer version..."*. No files are deleted.

---

### Crash Recovery

- [ ] **[Integration test]** **GIVEN** `runstate.sav` passes validation, **WHEN** the recovery chain runs, **THEN** the live `.sav` is used AND the `.bak` is NOT promoted AND `SaveBackupRecovery` is NOT logged. Chain exits on first valid candidate.
- [ ] **[Integration test — requires corruption tool]** **GIVEN** `.sav` fails but `.bak` passes, **WHEN** chain runs, **THEN** `.bak` is used and `SaveBackupRecovery` is logged.
- [ ] **[Integration test — requires corruption tool]** **GIVEN** `.sav` and `.bak` both fail but a valid orphaned `.tmp` exists, **WHEN** chain runs, **THEN** `.tmp` is promoted to `.sav` and used.
- [ ] **[Integration test — requires corruption tool]** **GIVEN** all three RunState candidates fail, **WHEN** chain exhausts, **THEN** main menu shows "New Run" only AND `RunStateFullLoss` is logged AND NO blocking dialog appears.
- [ ] **[Integration test — requires corruption tool]** **GIVEN** all three MasteryState candidates fail, **WHEN** chain exhausts, **THEN** a blocking dialog appears asking the player to choose "Continue with fresh mastery" or "Quit". `MasteryStateFullLoss` is logged regardless of choice. MasteryState is only reset if the player chooses "Continue."
- [ ] **[Integration test]** **GIVEN** MasteryState fails but RunState passes, **WHEN** both chains run, **THEN** each recovers independently.

---

### Save File Location

- [ ] **[Integration test]** **GIVEN** the game runs on Windows via Steam, **WHEN** a save write occurs, **THEN** `.sav` and `.bak` are in `Application.persistentDataPath/saves/` and `.tmp` is in `Application.temporaryCachePath/`.

---

### Periodic Flush (New)

- [ ] **[Integration test — requires DebugWriteCount]** **GIVEN** a player is on the map screen with dirty state for exactly 30s, **WHEN** the timer elapses, **THEN** exactly one periodic flush write is logged.
- [ ] **[Integration test]** **GIVEN** a periodic flush has just fired, **WHEN** state remains unchanged for another 30s, **THEN** no additional flush fires (dirty flag cleared by the write).
- [ ] **[Integration test]** **GIVEN** a player is mid-node-transition, **WHEN** 30s elapses, **THEN** no flush fires until `MapScreen.Idle` is reached.
- [ ] **[Integration test — requires DebugWriteCount + clock injection]** **GIVEN** a player remains on `MapScreen.Idle` with dirty state for a simulated 16+ minutes (30 flushes fired), **WHEN** the timer would next fire, **THEN** no additional flush is logged for this dwell session. The cap counter resets to 0 on the next `MapScreen.Idle` entry (after leaving the map screen — e.g., entering a node).

---

### Edge Cases

**EC2 — Sharing violation:**
- [ ] **[Integration test — requires InjectFault(SharingViolation)]** **GIVEN** a sharing violation is injected for `File.Move`, **WHEN** the write runs, **THEN** exactly 5 retries are logged with exponential spacing of 250ms, 500ms, 1000ms, 2000ms, 4000ms (±50ms tolerance for timer scheduling). On final failure, `SaveWriteFailedException` is logged AND a non-blocking HUD notification displays AND the live `.sav` is unchanged AND the main thread reports no frame time spike (retries on background thread).

**EC3 — Disk full:**
- [ ] **[Integration test — requires InjectFault(DiskFull)]** **GIVEN** disk-full is injected on `.tmp` write, **WHEN** caught, **THEN** the write is aborted, backup rotation has NOT run (`.bak` unchanged), the live `.sav` is intact, and a non-blocking HUD warning displays.
- [ ] **[Integration test]** **GIVEN** disk-full occurs during the run-end MasteryState write, **WHEN** caught, **THEN** a blocking dialog appears AND `runstate.sav` is not deleted.

**EC6 — Save directory missing:**
- [ ] **[Integration test — requires fresh-install-sim]** **GIVEN** `saves/` does not exist, **WHEN** the first write fires, **THEN** the directory is created and the write completes.
- [ ] **[Code review]** **GIVEN** save write runs, **WHEN** inspected, **THEN** `Directory.CreateDirectory(savePath)` is called unconditionally (idempotent).

**EC9 — Orphaned temp:** (covered in Crash Recovery ACs)

**EC10 — CompanyName / ProductName:**
- [ ] **[CI check]** **GIVEN** the CI pipeline reads `ProjectSettings/ProjectSettings.asset`, **WHEN** `CompanyName` or `ProductName` differs from the baseline in `.claude/docs/technical-preferences.md` under "Project Identity", **THEN** the build fails. The baseline file commits a canonical value for each field; the CI script parses both and compares.

**EC11 — Broken Addressables key:**
- [ ] **[Cross-system]** **GIVEN** a saved `CardId` no longer resolves in the current build, **WHEN** the deck is loaded, **THEN** Save System returns the stored keys faithfully AND the Card System applies its fallback policy (spec owned by Card System GDD). Save System reports no error; the load is considered successful.

**EC12 — Hard-kill:**
- [ ] **[Manual test]** **GIVEN** a player is on the map screen for >30s, **WHEN** Task Manager is used to end the process, **THEN** on relaunch, the game resumes at the map screen with state from the most recent periodic flush (≤30s old).

**EC13 — Run-end crash window:**
- [ ] **[Integration test — requires InjectFault(KillAfterMasteryWrite)]** **GIVEN** the fault is active, **WHEN** a run completes, **THEN** MasteryState write succeeds, RunState deletion is interrupted, and on relaunch the run-end marker in `runstate.sav` is detected AND `runstate.sav` is deleted AND "Continue Run" is not offered.

---

### IsFreeValveApplied Round-Trip (new)

- [ ] **[Integration test]** **GIVEN** a Chopshop node sets `IsFreeValveApplied = true` in `NodeFlagsDTO`, **WHEN** the player quits and relaunches, **THEN** the loaded RunState contains `IsFreeValveApplied = true` for the correct node index.

---

### Concurrent Writes (new)

- [ ] **[Integration test]** **GIVEN** a RunState write and a MasteryState write are both queued at the same moment, **WHEN** both execute on the background thread, **THEN** neither fails due to file contention AND both complete AND their completion log entries appear in the event log.

---

### Formula Verification

- [ ] **[Integration test — requires DebugWriteCount]** **GIVEN** a simulated 20-node run with 0 periodic flushes, no mid-run quit, **WHEN** all transitions complete, **THEN** `runstate.sav` has been written exactly 20 times.
- [ ] **[Integration test — requires DebugWriteCount]** **GIVEN** a simulated 20-node run, player quits cleanly after entering node 10, with 2 periodic flushes during map dwell, **WHEN** writes are counted, **THEN** total is 13 writes: 10 + 2 + 1.
- [ ] **[Integration test — requires DebugWriteCount]** **GIVEN** a simulated run with exactly 14 combat nodes and 6 non-combat nodes, **WHEN** all resolve, **THEN** `masterystate.sav` has been written exactly 14 times.
- [ ] **[Smoke check]** **GIVEN** a full run completes, **WHEN** on-disk size is measured, **THEN** combined size is ≤ 14 KB (2 × 6KB RunState + 2 × 1KB MasteryState).

---

### Regression Suite (Minimum Core)

When Save changes in a patch, re-run at minimum:
- Write Triggers — all 7
- Crash Recovery — all 6
- Atomic Write — the KillBetweenTmpAndMove and thread-ownership ACs
- Run Lifecycle — the crash-window AC (EC13)
- Schema Versioning — all 3
- Formula Verification — all write-count ACs

Advisory: full suite on major version patches.

## Open Questions

| # | Question | Owner | Needed By |
|---|---|---|---|
| OQ1 | F1 and F2 use `NodeCount` (~20) and `CombatNodeCount` (~14) as placeholders. Both must be confirmed by the Node Map System GDD. F3 storage budget is not sensitive. | Node Map System GDD | Before Save implementation |
| OQ2 | **EC7 traceability**: If Node Map introduces replay-encounter or backtrack mechanics post-EA, this GDD's AC suite must add the idempotency guard (`mastery_xp_awarded_for_node_[index]`) before that feature ships. Owned via the `/propagate-design-change` skill. | Node Map System GDD (if revised) | Before any node-re-entry feature ships |
| OQ3 | **Steam Cloud last-write-wins is an accepted limitation.** Multi-machine play may produce progress inversions in rare sync-ordering cases. Document on store page. A merge strategy is deferred to post-EA if playtesting shows this as a real problem. | Steam integration phase | Before EA ship (store page copy) |
| OQ4 | **`FlushFileBuffers` via `FileStream.Flush(flushToDisk: true)` on Mono**: Historically inconsistent. Verify in code review at Save implementation gate. If unreliable, add explicit P/Invoke to `FlushFileBuffers` or equivalent on target platforms. | Engineering (Save implementation) | Save implementation gate |
| OQ5 | **`File.Move(tmp, target, overwrite: true)` 3-arg overload in Unity 6.3 Mono runtime**: .NET Standard 2.1 exposes this; code review must verify it resolves at runtime on the target Mono version. If not: fallback requires P/Invoke `MoveFileEx` with `MOVEFILE_REPLACE_EXISTING`. | Engineering (Save implementation) | Save implementation gate |
