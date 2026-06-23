# Polish Capture: Generator Layout Authoring (ceiling + range + rename + icon halve)

**Date:** 2026-06-22
**System:** Run Loop — `BiomeDistributionSO` tuning surface + `BiomeGenerationInputs` POCO record + `BiomeWebGenerator` placement & edge passes + `MapView.uss` beacon size token
**Slice:** Intermediate — paused MapView authoring Commits 2+3 (sprite rebuild, USS custom properties) until layout settles. Single atomic commit, scoped ~30 LOC of production code + asset + tests.

## Trigger

Playtest pass on Block 3 closeout (Unity `006152d`) flagged the generated layouts as unusable. User's annotated screenshot: certain visually-close beacons not graph-adjacent; certain long edges should not exist; tuning the only knob (`MaxHopDistance: 280`) had no observable effect.

**Root cause split into three:**

1. **Naming mislead.** `MaxHopDistance` is a FLOOR (re-add close-pair Delaunay edges that pruning dropped), not a CEILING (cap edge length). Bumping it does not shorten edges. The name promised the wrong contract.
2. **Missing ceiling.** No upper-bound length filter exists in the pipeline. Long Delaunay survivors of forward-bias pruning go uncapped.
3. **Density + visual mass.** Beacon count is hardwired as a generator-internal const (`MinBeaconCount = 18`, `MaxBeaconCount = 22`), not a per-biome SO knob. Beacon icons render at `var(--wr-space-xl)` ≈ doubled visual mass; user asked for half.

Two of three are per-biome content (ADR-0015 narrowing): edge-length ceiling and beacon density. The third is a rename — ADR-0011 clarifying-rename during active dev (the system's not at done state yet; ADR-0015 amendment shipped 2026-06-21 already reframes the algorithm).

## What's being changed (single commit)

### 1. Field rename: `MaxHopDistance` → `ReconnectRadius`

The retained semantics: "any two beacons whose Delaunay edge length is ≤ this value MUST be graph-adjacent — re-add forward-oriented edges that pruning dropped, restricted to the Delaunay candidate set." That is a re-connection radius, not a hop cap. The rename aligns name with behavior. ADR-0011 clarifying-rename per `feedback_cross_check_adr_contract.md` — the system has no published API; tests live in-repo; no external consumers exist.

**Call sites (renames in lockstep):**

| File | Symbol |
|------|--------|
| `Assets/Scripts/Run/Authoring/BiomeDistributionSO.cs` | `_maxHopDistance` field, `MaxHopDistance` property accessor, xmldoc |
| `Assets/Scripts/Run/BiomeGenerationInputs.cs` | record positional param, get-only property override, xmldoc |
| `Assets/Scripts/Run/Authoring/BiomeGenerationInputsFactory.cs` | named-arg pass-through |
| `Assets/Scripts/Run/BiomeWebGenerator.cs` | `EnforceMaxHopReAdd` → `EnforceReconnectRadius` (helper method + parameter `maxHopDistancePx` → `reconnectRadiusPx`) + xmldoc + comment at the Generate pipeline call site |
| `Assets/Tests/EditMode/Run/BiomeWebGenerator_Test.cs` | ~15 references at lines 358, 360, 752, 770, 777, 819, 834, 861, 876, 913, 919, 937, 944, 961, 965, 972, 986, 1000, 1002, 1003, 1004, 1005 (named-arg `MaxHopDistance:` → `ReconnectRadius:`; test method names `MaxHopDistance_*` → `ReconnectRadius_*`; xmldoc and assertion messages) |
| `Assets/Resources/Run/Biomes/Biome1Distribution.asset` | YAML key `_maxHopDistance: 280` → `_reconnectRadius: 280` |

**Authored value preserved:** `280` survives intact under the new key.

### 2. Add `_maxEdgeLength` ceiling field (post-Delaunay cull)

New per-biome tuning knob — drop any Bowyer-Watson candidate edge whose canvas-pixel length exceeds this value BEFORE forward-bias pruning operates on the candidate set. This gives forward-bias a length-bounded graph to roll against; long survivors are no longer possible.

**Authoring surface:**

```csharp
// BiomeDistributionSO.cs (new field)
[SerializeField,
 Tooltip("Maximum permitted edge length in canvas pixels (1920×1080 reference) " +
         "before forward-bias pruning runs. Any Bowyer-Watson candidate edge " +
         "longer than this is dropped from the candidate set entirely, so neither " +
         "PruneEdges nor the ReconnectRadius re-add pass can re-introduce it. " +
         "Set to 0 to disable the ceiling (legacy / unconstrained behaviour).")]
private float _maxEdgeLength = 0f;

public float MaxEdgeLength => _maxEdgeLength;
```

**POCO record extension:**

```csharp
// BiomeGenerationInputs.cs — new positional param + get-only override (IsExternalInit workaround)
BiomeGenerationInputs(
    ...,
    float ReconnectRadius = 0f,
    float MaxEdgeLength = 0f)
{
    ...
    public float MaxEdgeLength { get; } = MaxEdgeLength;
}
```

**Factory pass-through:** `BiomeGenerationInputsFactory.From` adds `distribution.MaxEdgeLength` to the constructor call.

**Generator pipeline integration** — new helper `EnforceMaxEdgeLengthCull` inserted between Bowyer-Watson and PruneEdges:

```csharp
// BiomeWebGenerator.Generate — pipeline order
// 4. Bowyer-Watson Delaunay triangulation
var triEdges = BowyerWatsonEdges(positions);

// 4b. Max-edge-length cull (new, before pruning operates)
EnforceMaxEdgeLengthCull(positions, triEdges, inputs.MaxEdgeLength);

// 5. Forward-bias pruning + edge orientation
var edges = PruneEdges(rngTopology, positions, triEdges);

// 5b. Reconnect-radius re-add pass (was: max-hop)
EnforceReconnectRadius(positions, triEdges, edges, inputs.ReconnectRadius);
```

`EnforceMaxEdgeLengthCull` mutates `triEdges` in place, removing any unoriented `(a, b)` whose canvas-pixel distance exceeds `maxEdgeLengthPx`. `0f` → no-op.

**Authored value (Biome 1):** Initial `_maxEdgeLength: 320` baseline — sized to admit the typical Δx hop across the 1920-px canvas at 5–6 columns of clustering while cutting the longest forward-bias survivors. Tune by eye next session.

### 3. Add `_beaconCountRange` Vector2Int (replaces internal consts)

Moves `MinBeaconCount = 18` and `MaxBeaconCount = 22` off the generator class as public consts and onto `BiomeDistributionSO` as a single authored range. Per-biome density tuning — Biome 1 stays at (18, 22); future Biome 2+ pick their own range without touching code.

**Authoring surface:**

```csharp
// BiomeDistributionSO.cs (new field)
[SerializeField,
 Tooltip("Per-biome beacon count range, including the Start beacon at index 0. " +
         "Generator samples uniformly from [x, y] inclusive. Lower x for sparser " +
         "biomes, raise y for denser. Range must satisfy 1 ≤ x ≤ y; OnValidate " +
         "clamps and warns if violated.")]
private Vector2Int _beaconCountRange = new Vector2Int(18, 22);

public Vector2Int BeaconCountRange => _beaconCountRange;
```

**POCO record extension:**

```csharp
// BiomeGenerationInputs.cs — UnityEngine.Vector2Int cannot cross into POCO (ADR-0002).
// Use a named tuple at the POCO boundary.
BiomeGenerationInputs(
    ...,
    (int Min, int Max) BeaconCountRange,
    ...);
```

**Generator consumption:**

```csharp
// BiomeWebGenerator.Generate — line 173 equivalent
int targetCount = rngTopology.Next(
    inputs.BeaconCountRange.Min,
    inputs.BeaconCountRange.Max + 1);
```

**Consts removed:** `public const int MinBeaconCount = 18;` and `public const int MaxBeaconCount = 22;` deleted from `BiomeWebGenerator`. Tests at lines 358-360 switch from `BiomeWebGenerator.MinBeaconCount`/`MaxBeaconCount` to `inputs.BeaconCountRange.Min`/`.Max`.

**Factory pass-through:** `BiomeGenerationInputsFactory.From` adds `(distribution.BeaconCountRange.x, distribution.BeaconCountRange.y)` to the constructor call.

**OnValidate guard** (BiomeDistributionSO):

```csharp
if (_beaconCountRange.x < 1) _beaconCountRange.x = 1;
if (_beaconCountRange.y < _beaconCountRange.x)
{
    Debug.LogWarning(
        $"[{name}] BeaconCountRange.y ({_beaconCountRange.y}) < .x ({_beaconCountRange.x}); " +
        "clamping y up to x.", this);
    _beaconCountRange.y = _beaconCountRange.x;
}
```

### 4. Halve `.wr-beacon` USS size

```css
/* Assets/UI/MapView.uss — before */
.wr-beacon {
    width: var(--wr-space-xl);
    height: var(--wr-space-xl);
    ...
}

/* after */
.wr-beacon {
    width: var(--wr-space-lg);
    height: var(--wr-space-lg);
    ...
}
```

ADR-0014 design-token discipline preserved — both tokens are declared in the Slice 6 Phase 1 token sheet; this is a token swap, not a magic number. Connection-line endpoint anchoring already reads from `resolvedStyle` so the visual half automatically follows the layout half.

### 5. Biome1Distribution.asset YAML update

```yaml
# Authored values after this commit
_displayName: Biome 1 — Open Wasteland
_nonTerminalBeaconTypes:
- Type: 1
  Weight: 1
_terminalBeaconType: 7
_combatArchetypes:
- Id: 0
  Weight: 1
- Id: 1
  Weight: 1
- Id: 2
  Weight: 1
_allowBidirectional: 1
_reconnectRadius: 280          # was _maxHopDistance
_maxEdgeLength: 320            # NEW
_beaconCountRange: {x: 18, y: 22}   # NEW
_mapTheme: {fileID: 11400000, guid: c5d4e3f2a1b0c9d8e7f6a5b4c3d2e1f0, type: 2}
```

## What's destroyed (capture-before-destroy receipts)

### `BiomeWebGenerator.MinBeaconCount` / `MaxBeaconCount` public consts

Deleted from production. Both were public consts (one test reads them directly at lines 358-360). Replaced by per-instance `BeaconCountRange.Min/.Max` on the SO + POCO record. No compat overload retained (ADR-0011 #5).

### `MaxHopDistance` name

Old name does not survive anywhere — SO field, SO property, POCO positional param, POCO property override, factory arg, generator helper name (`EnforceMaxHopReAdd`), generator parameter name (`maxHopDistancePx`), Biome1Distribution.asset YAML key, all xmldoc, all test method names (`MaxHopDistance_DroppedEdgeReAddedWhenWithinD`, etc.), all `Assert.AreEqual` assertion messages. ADR-0011 clarifying-rename — no `// renamed from MaxHopDistance` comments anywhere; the diff IS the explanation.

### `EnforceMaxHopReAdd` helper

Renamed to `EnforceReconnectRadius`. Mechanically identical; parameter `maxHopDistancePx` → `reconnectRadiusPx`. Pipeline comment block updated to "5b. Reconnect-radius re-add pass."

### Authored value not destroyed

`_maxHopDistance: 280f` carries forward intact as `_reconnectRadius: 280f`. The 2026-06-22 Block 3 tuning baseline survives the rename. New fields `_maxEdgeLength: 320` and `_beaconCountRange: (18, 22)` are additive — neither replaces any prior authored value.

## Test surface

**Tests touched (rename only — no logic change):**

| Test method (old name → new name) | Reason |
|-----------------------------------|--------|
| `MaxHopDistance_DroppedEdgeReAddedWhenWithinD` → `ReconnectRadius_DroppedEdgeReAddedWhenWithin` | rename |
| `MaxHopDistance_BackwardEdgeNotReAdded` → `ReconnectRadius_BackwardEdgeNotReAdded` | rename |
| `MaxHopDistance_NonDelaunayPairsNotAdded` → `ReconnectRadius_NonDelaunayPairsNotAdded` | rename |
| `MaxHopDistance_ZeroDisablesThePass` → `ReconnectRadius_ZeroDisablesThePass` | rename |
| `MaxHopDistance_BiomeDistributionSOFlagThreadsThroughInputs` → `ReconnectRadius_BiomeDistributionSOFlagThreadsThroughInputs` | rename |
| Beacon-count-bounds asserts (lines 358-360) | Switch `BiomeWebGenerator.MinBeaconCount` → `inputs.BeaconCountRange.Min` (and Max) so the test still pins the bound but reads it from the per-call input rather than a deleted const |

**New tests:**

- `MaxEdgeLength_LongEdgeDroppedBeforePruning` — fixture biome with a hand-crafted high-aspect edge (>500 px); assert no edge longer than the ceiling appears in `graph.Edges` after Generate.
- `MaxEdgeLength_ZeroDisablesTheCull` — fixture biome with `MaxEdgeLength = 0f`; assert long edges may survive (smoke check against the same fixture).
- `BeaconCountRange_RoundTripsThroughInputs` — pins `(18, 22)` and `(28, 32)` round-trip through SO → factory → record → graph.Beacons.Count bound.

**Net test delta after slice:** +3 tests, 0 dropped. EditMode target: **504 / 503 passed / 0 failed / 1 pre-existing skip** (501 baseline + 3 new).

## ADR alignment

### ADR-0015 (Biome Distribution as Configuration Narrowing)

- `_maxEdgeLength` and `_beaconCountRange` are per-biome **content** — same shape as the existing `_allowBidirectional`, `_reconnectRadius`, beacon types, archetypes. Narrowing-by-data-table.
- The boundary holds: algorithm-internal tuning (Poisson-disc min-sep schedule, super-triangle scale, forward-bias floor, terminal-X tolerance) stays as code constants. Per-biome semantically-meaningful tuning (density, edge-length ceiling, reconnect radius) goes on the SO.
- No new branches in `BiomeWebGenerator.Generate`. New ceiling pass is a flat add to the pipeline, gated by `<= 0 → no-op` — same shape as the existing reconnect-radius pass.

### ADR-0011 (No bridges at done)

- Clarifying rename during active development: system has no shipped consumers outside this repo, no published API, ADR-0015 amendment 2026-06-21 already reframed the algorithm. No `// formerly MaxHopDistance` comments; no compat property; no compat overload.
- No transitional adapter for the deleted consts — tests rewire to the record. `BiomeWebGenerator.MinBeaconCount` was public; the deletion is a hard cut, justified by zero external consumers (`grep -rE "BiomeWebGenerator\.(Min|Max)BeaconCount"` returns only the two test sites at lines 358-360).

### ADR-0002 (Run assembly noEngineReferences)

- `UnityEngine.Vector2Int` stays on the SO side only. POCO record uses `(int Min, int Max) BeaconCountRange` named tuple. Factory unpacks `Vector2Int.x/.y` into the tuple at the SO → POCO boundary. No engine type crosses.

### ADR-0014 (UI Toolkit primary)

- USS size halve is a token swap (`--wr-space-xl` → `--wr-space-lg`), not a hardcoded value. Design-token discipline preserved.

## Technical Director Review

**Verdict (2026-06-22, in-conversation TD consult):**

> **Ship this:** New slice "Generator layout authoring (ceiling + range + rename + icon)." One atomic commit, ~30 LOC of production code plus the asset YAML and test renames. Add `_maxEdgeLength` (float, post-Delaunay cull), replace `MinBeaconCount`/`MaxBeaconCount` with `_beaconCountRange : Vector2Int`, rename `MaxHopDistance` → `ReconnectRadius` everywhere (SO + record + factory + generator + asset + tests), halve `.wr-beacon` size token. Then resume MapView Commits 2+3 on the validated layout. Tests must stay ≥ 501/500/0/1 (3 new tests bump the floor to 504/503/0/1).
>
> **Rationale.** The user's playtest finding is structural: the rename + missing ceiling are real bugs in the authoring surface, not visual polish. Shipping BeaconNodeElement sprite rebuild on top of a layout that doesn't match the user's mental model wastes the next slice — they'd come back with the same "lines are wrong" complaint at the sprite-rebuild closeout. Fix the generator, validate by eye, then polish.
>
> **Boundary discipline (ADR-0015):** beacon count and edge-length ceiling are per-biome SO knobs. The Poisson min-sep schedule, super-triangle scale, forward-bias floor, and terminal-X tolerance stay on the generator class as constants — those are algorithm-internal tuning, not biome content. If a future biome demands a different forward-bias floor, that's a separate slice that touches the algorithm boundary; today it doesn't.
>
> **Order of operations inside the commit:** SO edits → POCO record extension → factory + generator updates (including the new helper inserted between Bowyer-Watson and PruneEdges so forward-bias operates on a length-bounded graph) → test renames + new coverage → Biome1Distribution.asset YAML edits → MapView.uss `.wr-beacon` halve → run EditMode → revert any TextMeshPro drive-by → single commit. Grep-gates the same as Block 3 (no `MaxHopDistance`, `MinBeaconCount`, `MaxBeaconCount` survivors anywhere; no `_maxHopDistance` in any asset).
>
> **Risk acknowledged:** the `_maxEdgeLength: 320` baseline is a guess. It WILL need eyeball tuning next session against Biome 1's spread. That's fine — same iteration loop as Block 3's 280 baseline. Surface the value in the post-commit summary so the user knows to playtest.

## Execution order

1. Write this capture file (hook unblocks).
2. SO edits (`BiomeDistributionSO`): rename field, add `_maxEdgeLength`, add `_beaconCountRange`, OnValidate guard, accessor properties.
3. POCO record extension (`BiomeGenerationInputs`): rename param, add `MaxEdgeLength`, add `BeaconCountRange` tuple, get-only property overrides, xmldoc.
4. Factory pass-through (`BiomeGenerationInputsFactory`): pass all three new/renamed fields.
5. Generator (`BiomeWebGenerator`): delete consts, rename helper + parameter, add `EnforceMaxEdgeLengthCull`, swap `Next(MinBeaconCount, MaxBeaconCount + 1)` → `Next(inputs.BeaconCountRange.Min, inputs.BeaconCountRange.Max + 1)`, xmldoc.
6. Test rename (`BiomeWebGenerator_Test.cs`): mechanical sed-style pass; switch beacon-count bound asserts to read from `inputs`.
7. New tests: `MaxEdgeLength_LongEdgeDroppedBeforePruning`, `MaxEdgeLength_ZeroDisablesTheCull`, `BeaconCountRange_RoundTripsThroughInputs`.
8. Biome1Distribution.asset YAML: rename key, add two new keys.
9. MapView.uss: `.wr-beacon` size token swap.
10. Run EditMode in batchmode (no `-quit` per `project_unity_batchmode_no_quit.md`).
11. Revert any TextMeshPro drive-by (`git checkout --` on RussoOne SDF if touched).
12. `git status` + grep-gates clean → single commit.

## Post-commit posture

- Resume task #22 (Commit 2: BeaconNodeElement sprite rebuild) on the validated layout.
- Resume task #23 (Commit 3: ConnectionLineElement USS custom properties).
- Framework origin push still deferred (24+ framework commits stacked locally).
- `_maxEdgeLength: 320` is a baseline guess — user playtests next session and tunes the SO directly.
