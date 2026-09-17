# Postmortem — ADR-0018 P4a Migration Regression Wave (D1–D8)

**Date:** 2026-09-17
**Author:** technical-director
**Scope:** The UGUI → UI Toolkit combat HUD migration (ADR-0018 P4a, slices 1–7c2,
commits `6a09273` … `ea98e8c` in the Unity repo) and the eight defects it produced.
**Status:** Retrospective. Five defects fixed or hardened, three open (D2 chassis
door, D6 root cause, D7/D8 unverified). No code changed by this document.

**The ask, verbatim:** *"while transitioning into this new system and causing all
this change and bugs to occur, look at this seriously to why it happened so we
don't experience these kinds of backward changes that cost us time."*

**Evidence base (all read, not recalled):** `production/remediation-plan-2026-09-08.md`
§"Playtest debt + defect register" + §0c; `production/td-verdicts/2026-09-16-p4a-defect-fix-slice.md`;
`production/polish-captures/2026-09-13-p4a-slice6-crosshair.md`;
`production/polish-captures/2026-09-16-drag-cast-thresholds.md`; the working-tree
diff of `Assets/UI/CombatHudPanel.uss`, `Assets/Scripts/UI/CombatHudPanelController.cs`
and `Assets/Scripts/CombatView/CombatHud.cs` in the Unity repo.

---

## 1. What happened

Grouped by failure class. Defect numbers are labels, not the unit of analysis —
three of the five classes produced more than one defect, which is the point.

### Class A — Convention-crossing ports that verified value equality, not rendered equality

Two defects, both from the same slice, both invisible to every check that ran.

**D4 (rotation).** UGUI `localRotation` is CCW-positive; USS `rotate` is
CW-positive. The four crosshair brackets' rotations (`0 / −90 / +90 / −180`) were
carried across verbatim. `0` and `−180` are sign-invariant, so exactly the TR/BL
pair rendered 180° wrong — a two-of-four failure, which reads as "the art is a bit
off" rather than "the port is broken." The 2026-09-13 capture *did* identify and
correctly solve a different convention crossing on the same four elements: the
centre-offset ↔ box-margin conversion, including the `centreY = −(marginTop +
height/2)` y-flip, complete with a worked example proving why the naive version
breaks the pinwheel. Position was treated as a convention. Rotation was treated as
a number. Fixed 2026-09-17 in `Assets/UI/CombatHudPanel.uss` (`--tr` → `90deg`,
`--bl` → `−90deg`).

**D1 (original).** `_castEngageLiftPx` / `_castCommitLiftPx` were authored against
one lift-measurement model and consumed under another: lift was measured from
`_basePosition` (the *un-hovered* arc row), while a card under the cursor already
sits +100px up from hover. The constants' own tooltips claimed "pixels of upward
drag"; the code measured pixels-from-a-row-the-card-is-no-longer-in. Engage 120
was ~20px of real drag. Same shape as D4: the number transported intact, the
meaning did not.

**The closure failure.** The 2026-09-15 cross-check compared every crosshair value
against the capture and reported *"the port is not the problem — every one carries
over exactly."* That sentence is TRUE about value transport and FALSE about the
render, and it **closed the investigation**, redirecting D4 entirely onto the
lifecycle hypothesis. The lifecycle bug was real (below), so the investigation
produced a correct finding and a false all-clear in the same breath.

### Class B — One-shot lifecycle events and terminal paths trusted, not validated

**D4 (lifecycle).** `SnapshotCrosshairDefaults` — the only runtime writer of
bracket positions — was triggered by a one-shot `GeometryChangedEvent` registered
on the *parent* reticle, which can fire before its children resolve. It
unregistered unconditionally. Both branches are silent: snapshot-too-early bakes
garbage into inline margins; snapshot-never-runs leaves `_crosshairDefaults` null
and `TickCrosshair` a permanent no-op. Fixed with validate-before-unregister
(`TrySnapshotCrosshairDefaults`, `CombatHudPanelController.cs:930`) plus a
first-cast fallback (`:998`) — two independent entry paths, neither load-bearing
alone.

**D6.** Per-card tweens were fire-and-forget (`HandBeat.cs:112/:184` discarded the
`Coroutine` handles), so `HandSequencer.Stop()` could not stop them, and
`EndTurnCoroutine`'s unbounded `while (_hud.IsHandAnimating)` wait had no
diagnostic. The PlayMode test written during the fix then **proved** the mechanism:
*Unity kills a coroutine on GameObject deactivation without running its `finally`.*
A mid-tween `SetActive` cycle wedged `IsAnimating` forever, silently. Hardened
2026-09-16; test at `Assets/Tests/PlayMode/CombatView/CardHandElement_AnimationLifecycle_Test.cs`.
D6 stays OPEN — the 2026-09-16 lock's trigger is still unnamed.

**D3.** `CancelDrag` cleared `_isDragging`/`_dragStarted` but not `_pointerArmed` —
the same non-exhaustive-terminal-path shape at gesture scale. Latent; died with
tap-to-play.

**D5** was not a defect. It was D6(a) + D2 + D1 presenting as one softlock, and it
consumed a full investigation cycle before the Console repro decomposed it.

### Class C — Case-space reasoning that was complete about the cases it enumerated

**D2.** The 2026-09-13 hit-target ordering reasoned carefully about two adversary
pairs — widget vs. its own zone, and widget vs. structural zone — and got both
right. The pair `widget_i` vs. `zone_j` (i≠j) was never named, so slot 0's badge
shadowed slot 2's wheel zone. Half-fixed by three-tier ordering (all zones → all
widgets → structural). The chassis-door dead spot did **not** fall to static
analysis and remains OPEN.

### Class D — Screen-space numbers estimated from a screenshot

**D1 (retune).** The corrective retune overshot ~2×: the HP-bar landmark was read
off an **828px-tall windowed screenshot** without normalizing to the 1080 reference
panel (~1.6× inflation), and measured **from the panel bottom** instead of from the
cursor's grab point. Result: 400/480 shipped, the crosshair armed at mid-screen,
and a second retune to 220/300 followed on 2026-09-17. A defect fix produced a
defect of the same kind, one round later. The TD recap had independently floated
220/300 and was overridden by the "measured" figure — a measurement with an unstated
frame of reference outranked a correct estimate.

### Class E — Playtest-only-verifiable properties shipped with no first-look gate

Feel thresholds, tween choreography, hit-zone geometry and layering cannot be
proven by EditMode tests, and nothing else gated them. Seven slices landed across
two-plus weeks under the 2026-09-13 continuous-execution approval; the first real
playtest was 2026-09-15 and returned six defects at once, plus D7 (discard burst
bundles cards mid-screen) and D8 (buff-chip tooltip unverified). The captures were
not negligent about this — slice 6's capture ends with an **eight-item playtest
list that names the bracket pinwheel directions explicitly** ("TL down, TR left, BL
right, BR up"). D4's rotation defect would have been caught by item 4 of a list
written on 2026-09-13 and executed on 2026-09-15. The list existed. Running it was
not a gate.

### The process finding underneath all five

Every P4a capture before slice 7 records **"No TD agent spawned"**, each justified
by the same sentence: *"executes the pattern proven in slices 1–N."* That was true
architecturally — and irrelevant. Slice 6 introduced three mechanism classes the
migration had never used: the first **ported rotation**, the first **runtime
snapshot of resolved layout used as a source of truth**, and the first
`RuntimePanelUtils` conversion. A grep for `rotate|rotation|coroutine|schedule|transition`
across the slice 2–4 captures returns **zero hits** — nothing before slice 6 was
transform-animated at all.

Note which of the three broke. The capture explicitly flagged `RuntimePanelUtils`
as a project first, reasoned about its cost profile, and defended keeping the P4b
gate closed — and that mechanism is in §2 *Confirmed good*. The two mechanisms it
did not name as novel are exactly the two that produced D4. **Novelty flagging was
accurate wherever it was applied, and it was applied ad hoc.** Gate-skipping
tracked architectural novelty (genuinely zero) while the risk lived in mechanism
novelty (high).

---

## 2. The common threads

1. **Convention-crossing ports were verified for value equality, not rendered
   equality.** Every defective value was transported perfectly. The check that
   would have caught D4 is not a better diff — it is one screenshot.

2. **A cross-check that found nothing was recorded as a closure.** "Every value
   carries over exactly" narrowed the search space; it was written as though it
   had eliminated a cause. This is [[feedback_verified_requires_evidence]] with
   the polarity flipped: the evidence was real, the *scope claim* attached to it
   was not.

3. **One-shot lifecycle events were trusted rather than validated.** D4-lifecycle,
   D6 and D3 are one shape: a single path is the only writer of state another path
   blocks on, and its failure mode is silence. The project already knows this
   family — `feedback_uidocument_setactive_reclone` and
   `feedback_uidocument_negative_exec_order` describe it almost exactly, and the
   register even *cites them by name* while diagnosing D4. Knowing the pattern did
   not prevent shipping it, because knowledge was not attached to a gate.

4. **Engine runtime facts were assumed, not tested — until one was, and it
   immediately paid.** "Coroutines run their `finally` on teardown" was load-bearing
   for the whole hand pipeline and false. The PlayMode test that disproved it took
   one commit and converted a class of silent wedges into a named, locked behavior.

5. **Feel-properties had no first-look gate, so verification debt compounded
   silently at ~1 slice/day and settled in one wave.** Individually each defect was
   a 20-minute fix; discovered together, they cost a softlock investigation, a
   14-constraint TD slice, a bad retune, and two weeks of accumulated doubt about
   which parts of the HUD were trustworthy. **The cost was not the bugs; it was the
   batching.**

6. **"Same pattern as last time" was used as a risk assessment.** Precedent
   inheritance is not risk inheritance. The exemption is legitimate for repeated
   *architecture* and illegitimate for first-time *mechanisms*.

---

## 3. Prevention protocol

Six rules. Each states where it hooks and what makes it done — a rule with no hook
is a wish.

### P1 — Convention Pair Table + one rendered check (adopts (a), sharpened)

**Rule.** Any port that crosses a coordinate, rotation, unit, origin or scale
convention carries, in its capture, a **Convention Pairs** table with one row per
axis — *position origin, y-axis sign, rotation sign, rotation pivot, reference
resolution / scale, anchor-pivot* — even when the row reads "identity, no
conversion" **with a stated reason**. A missing row is the defect; a row asserting
identity without a reason is the same defect wearing a hat. The port does not close
without a **rendered-output check**: a screenshot of the ported element at rest, or
a geometry assert on the resolved values.

**Sharpening.** The candidate rule said "enumerate each convention pair." Slice 6
*did* document a convention pair — correctly, with worked arithmetic. What was
missing was the enumeration of **which conventions exist at all**. Enumerate the
axis list first, then fill it; do not document the conversions you happened to
think of.

**Why this and not a grep gate:** there is no predicate for "semantically wrong
sign." A CI grep would have to match `rotate:` in USS, which is present and correct
in three of four cases here. See the Rejected section.

**Hook.** Capture template — required section for any migration/port capture. The
TD gate treats a port capture without the table as CONCERNS by default.
**Done-test:** the capture contains the table *and* a `## Rendered check` line with
a screenshot path or an assert name.

### P2 — One-shot registrations validate before unregistering (adopts (b))

**Rule.** A callback that unregisters itself must: (1) validate the precondition it
was waiting for; (2) unregister **only** on success; (3) have a second, independent
entry path at the point of use. Generalized form: **no single-fire path may be the
only writer of state another path reads.**

**Codification.** Ships in `.claude/docs/coding-standards.md` in a new *Lifecycle &
terminal paths* section, alongside the `try/finally` coroutine standard ruled in
`production/td-verdicts/2026-09-16-p4a-defect-fix-slice.md` §5 — **which is still
owed** (grep of `.claude/docs/` returns zero hits for `try/finally` as of today).
The two rules are one family: `finally` covers exception/Dispose terminal paths,
the stoppable handle covers a live wedged coroutine, validate-before-unregister
covers a one-shot that fires at the wrong time. Write all three at once.

**Hook.** `.claude/docs/coding-standards.md` (owner: lead-programmer) + code-review
gate. **Done-test:** the section exists and names all three halves; the reference
implementation is `TrySnapshotCrosshairDefaults` + the first-cast fallback.

### P3 — Engine-runtime assumptions get a pinning PlayMode test, same commit (adopts (c), scoped)

**Rule.** If correctness depends on an engine behavior that is **not** stated in
`docs/engine-reference/unity/`, a PlayMode test pinning that behavior ships in the
same commit, and a one-line entry is added to the engine reference.

**Scoping trigger (so this does not become "test all of Unity"):** the rule fires
when the assumption was *written down as justification* — in a code comment, a
capture, or a TD brief ("Unity guarantees X", "this runs before Y", "teardown will
call Z"). If you had to write the sentence to defend the design, pin it. That test
is cheap and the design already told you where to point it.

**Hook.** `.claude/docs/coding-standards.md` Testing section + TD gate: a brief
whose rationale contains an unpinned engine-behavior claim returns CONCERNS with
the test named in scope. **Done-test:** `CardHandElement_AnimationLifecycle_Test.cs`
is the template; `docs/engine-reference/unity/` gains a `verified-behaviors.md`
whose first row is the coroutine-teardown fact.

### P4 — Five-minute first look, same session, before the commit message (adopts (d), sharpened)

**Rule.** Every UI slice's capture splits its playtest list into **FIRST LOOK
(≤5 items, ≤5 minutes)** and **Deferred**. First-look items must be observable
within 60 seconds of entering a combat with no special setup; anything needing a
rare condition (deck exhaustion, ambush, a defeat, a live buff chip) goes to
Deferred *by construction*. The slice does not close until first look is run and
the result is pasted back into the capture under `## First look — <date>`, one
pass/fail line per item. Only Deferred items may become register rows.

**Sharpening.** The candidate rule was "ship a checklist." The checklists existed
and were good. What was missing is an **execution gate and a size cap** — eight
items at the end of a slice is a document; five items with a stopwatch is a gate.

**On continuous execution.** The 2026-09-13 approval ("keep going until we are
done") is what removed the natural per-slice stop, and it was the right call for
throughput. The correction is narrow and belongs in the protocol, not in a
retraction: **an approval to run continuously is an approval to skip *approval*,
not to skip *verification*.** Continuous mode keeps first look; it drops only the
"may I proceed" round-trip.

**Hook.** Capture template + the pre-commit habit already used for test counts.
**Done-test:** every capture dated after today carries a `## First look` section
with a date and per-item results.

### P5 — Screen-space tuning numbers come from logged runtime values (adopts (e))

**Rule.** Any number destined for a serialized tuning field and expressed in
screen/panel pixels originates from a **runtime log** — a rect dump, a
`resolvedStyle` print, a two-probe panel measure. A screenshot-derived figure is
admissible only if the capture states the screenshot's pixel height *and* shows the
normalization arithmetic to the 1080 reference panel as an explicit line, plus the
measurement origin (grab point vs. panel bottom vs. element centre).

**Standing instrument.** `CombatHud.DescribeHitTargets` (added 2026-09-17; fires
only on the no-target-commit warning, never per frame) is the model: a permanent,
zero-cost-until-needed geometry dump. Keep it, and extend the pattern to panel
landmarks when the next screen-space tune comes up.

**Hook.** Capture `## Why` section must show the arithmetic; TD gate rejects a
screen-space retune whose provenance is an unnormalized screenshot.
**Done-test:** the 2026-09-16 capture's 2026-09-17 addendum is the format — scale
factor and origin both named in the text.

### P6 — Mechanism novelty, not architectural novelty, decides the review gate

**Rule.** The "No TD agent spawned — same pattern as slice N−1" exemption is valid
only when the slice introduces no **first-occurrence mechanism**. First occurrence
in a migration of any of: a ported rotation/scale/skew; a runtime snapshot of
resolved layout used as a source of truth; a coroutine or tween that raises a flag
other code blocks on; a pointer-capture gesture; a screen↔panel or world↔screen
conversion; a self-unregistering callback — spawns Gate 2 (designated specialist +
TD), regardless of how similar the architecture is.

**Corollary on closure wording.** A cross-check that finds no defect is recorded as
**"ruled out: <specific mechanism>"**, never as "X is not the problem." The register
Status column takes the narrow form. D4's investigation was redirected by one
over-broad sentence.

**Hook.** Capture "Technical Director Review" section — the no-TD exemption must
name the mechanism-novelty list and assert zero hits.
**Done-test:** no capture claims the exemption without that assertion.

### Rejected

- **A CI grep gate for convention drift.** Both candidate predicates fail in the
  ADR-0018 manner: matching `rotate:` in USS hits three correct values for every
  wrong one, and matching "ported value" is not expressible. The signal is
  semantic; the gate must be a rendered check (P1), not a pattern match.
- **A blanket re-audit of all seven P4a slices.** Unbounded, and §2 of the register
  already records twelve properties as confirmed-good by play. Replaced by the
  bounded sweep in the debt list below.
- **A timeout/bypass on the End Turn waits.** Already ruled out as TD constraint
  A5 and it stays ruled out — a bypass converts a visible wedge into an invisible
  desync. Diagnostic scream only.
- **Promoting the threshold constants to a ScriptableObject.** No second consumer;
  the serialized-field seam is the 1.0 home. Re-raise only if a second consumer
  appears.

### One-time debt created by this wave

1. ~~**Owed since 2026-09-16:** write the `try/finally` + stoppable-handle +
   validate-before-unregister standard into `.claude/docs/coding-standards.md`
   (P2). It is currently ruled but unwritten.~~ **DONE 2026-09-17** — new
   "Coroutine & One-Shot-Event Lifecycle" section, five rules, both pinned
   runtime facts cited.
2. ~~**Bounded sweep:** every `rotate` / `scale` / `translate` in
   `Assets/UI/CombatHudPanel.uss` whose value was ported from a UGUI
   transform.~~ **DONE 2026-09-17** — grep shows the four crosshair-quarter
   `rotate` values are the file's ONLY transform ports; the ±90° pair was
   fixed, 0°/180° are flip-symmetric. No other exposure.
3. **Run the §3 owed list** under P4's first-look format. (Partially consumed
   by the 2026-09-17 defect closures; remainder rides the next playtest.)
4. ~~**Still open, unchanged by this document:** D2 chassis-door dead spot,
   D6 root cause, D7 discard bundling, D8 buff-chip tooltip.~~ **Register
   §0d–0f (2026-09-17): D2 closed — the door "dead spot" was the y-flip, not
   badge rects (the UX call died with it); D7 closed (gap-frame yank +
   mid-cascade burst split); D8 closed (leaked cast session + ancestor hover
   bubbling). D6's trigger alone remains, hardened + instrumented.**

---

## 4. What we got right — the protocol builds on this

- **Capture-before-destroy.** The slice-6 capture is why D4 was diagnosable at all:
  it preserved the four bracket offsets, both tints, `LockMultiplier 0.65` and
  `LockTransitionSec 0.5` *with their designer rationale and retune history*.
  Without it, a 180° rotation error on two of four sub-sprites would have been
  indistinguishable from "the original art looked like that." The capture also
  caught the prefab-baked threshold trap (`CombatHudPanel.prefab:74-75`) that would
  have made the entire D1 fix a silent no-op.
- **The defect register.** Eight defects tracked by severity, with an evidence
  column separating *code read* from *seen in play* and a "Confirmed good — do not
  re-check" section. That separation is why D3 was correctly downgraded to latent
  and why the second playtest did not re-walk twelve settled properties.
- **The D5 Console protocol** (register §5). Five numbered steps, including the
  line *"a clean Console is also a result"* and the single-word turn-banner
  discriminator that splits D6(a) from D6(b). It turned an unreproducible softlock
  into a three-defect decomposition in one run. This is the template for every
  future "it just locked up."
- **The rect-dump instrument.** `DescribeHitTargets` + the hover-transition
  narrator make the next chassis-door repro self-diagnosing, at zero per-frame cost.
  Diagnostics that ship *with* the open defect are how P5 becomes affordable.
- **Two PlayMode lifecycle tests.** One of them disproved an engine assumption the
  whole hand pipeline rested on. That is the single highest-value artifact of the
  fix slice.

---

## 5. Three-lens self-audit (TD)

**Lens 1 — Codebase health.** ADR-0011: this document proposes no bridge, adapter
or parallel path; P2's "second independent entry path" is a **fallback within one
owner**, not a bimodal path — `TrySnapshotCrosshairDefaults` is the single writer
and both entries call it, which is the distinction that keeps it compliant. Single
responsibility: P1/P4 hook the **capture template**, not any controller, so no
class grows surface. Subscription lifecycle: P2 is the generalization of the
existing Bind↔OnDestroy / OnEnable↔OnDisable rule to *self-unregistering* callbacks,
which that rule never covered — real gap, now named. Duplication: the try/finally
standard currently exists in a verdict and in code but in **no shared doc** — that
is the two-callers-before-the-third case, and debt item 1 is the fix. Teardown: D6's
proven coroutine-teardown behavior is the guard, and it is now test-pinned.

**Lens 2 — Optimization.** The protocol adds no runtime cost — P1/P4/P5/P6 are
document-time gates, P2 is a branch on a one-shot path, P3 is test-only. The
instruments it endorses are already correctly gated: `DescribeHitTargets` runs only
on the warning path, and the hover narrator fires per *transition*, not per frame —
the right cadence, since the underlying state is a slot identity, not a continuous
value. Flagged, not fixed, carried forward from the 2026-09-16 verdict:
`GetComponentInParent<Canvas>()` inside `FindHoverInStack`'s loop
(`CombatHud.cs:961`) — acceptable at drag-only cadence and small N; hoist when next
touching that function. One live cost to schedule: the hover narrator is an
unconditional `Debug.Log` per transition and is marked "remove once the chassis-door
dead spot is closed" — that removal is a real TODO with an owner, not a permanent
cost, and it should die with D2.

**Lens 3 — 1.0-shape survival.** These are process rules, so the survival question
is whether they outlive P4a. P1, P5 and P6 are stated in terms of *any* port and
*any* screen-space tune, which is what P4b (world-anchored conversions, a harder
version of the same problem) and P5-the-migration-phase will need — they are not
P4a-shaped. P2 and P3 land in `coding-standards.md`, the permanent home. P4's
first-look format is the one item with a stopgap risk: a five-item cap that becomes
a ritual signature is worse than no gate, because it manufactures false confidence.
Its survival test is falsifiable and worth writing down — **if three consecutive
slices report first look all-pass and the next playtest still finds a feel defect,
the cap is being gamed and the format gets re-derived, not renewed.**

**Success criteria for this protocol.** We will know it was right if the next
migration phase produces defects that are *found in the session that created them*
rather than in a wave two weeks later — the target is not zero defects, it is zero
**batched** defects. Second signal: no defect in the next phase is of Class A
(value-equal, render-wrong), because that class is the one a single screenshot
retires.
