# Save & Persistence GDD — Review Log

## Review — 2026-04-21 — Verdict: MAJOR REVISION NEEDED

Scope signal: L (drops to M after scope-reduction revision)
Specialists: game-designer, systems-designer, unity-specialist, performance-analyst, qa-lead, creative-director
Blocking items: 10 | Recommended: 15 | Prior verdict resolved: First review

**Summary:** Five specialists converged on F1/R2 contradiction (write-frequency formula contradicted the rule that only one write fires per node). Unity-specialist uniquely surfaced three platform-reality gaps: `File.Move(tmp, target, overwrite)` availability on Mono, `Application.quitting` unreliability on hard-kill, and `System.Text.Json` silent breakage under IL2CPP stripping. Systems-designer demonstrated that R1's claim of compile-time interface mutual-exclusion is impossible in C#. Game-designer surfaced three pillar violations: mid-combat Alt+F4 as de facto revert, MasteryState write-before-reward-screen creating orphan state, and `MasteryStateFullLoss` as silent toast violating "the car remembers" fantasy. Creative-director added a strategic scope-reduction recommendation: save system was over-engineered for solo-dev card roguelike scope (AAA live-service architecture for a game where a corrupted run means "start a new run").

**Revision applied same day (2026-04-21):**

Scope reductions:
- Backups cut from N=3 (5 recovery candidates) to N=1 (3 recovery candidates)
- Per-system migration chain deferred to first content patch (EA ships with version tags but no migration runtime)

Blocking fixes (10):
1. F1 rewritten: `NodeCount + PeriodicFlushes + QuitWrites`. Formula-verification ACs rewritten to match.
2. `File.Move` 3-arg overload: OQ5 tracks runtime verification; P/Invoke `MoveFileEx` documented as fallback.
3. `Application.quitting` demoted to best-effort safety net; new periodic idle flush (30s) is primary hard-kill guarantee. EC12 added.
4. Serializer switched: Newtonsoft.Json + link.xml preservation (replaces System.Text.Json). Rationale documented.
5. Interface enforcement: reflection-based unit test (`InterfaceExclusion_test`) is the gate. False compile-time claim removed.
6. Mid-combat quit explicitly accepted as intentional soft-undo; "no revert window" language removed.
7. MasteryState write moved to AFTER reward screen close. Ordering AC rewritten.
8. `MasteryStateFullLoss` escalated to blocking Continue/Quit user dialog.
9. Threading: writes on background `Task`; main thread fires & forgets. Only quit + launch recovery are synchronous.
10. `FileStream.Flush(true)` Mono verification tracked in OQ4; P/Invoke fallback documented.

Recommended revisions applied (14):
- Envelope: `envelope_version: 1`, `written_at` ISO 8601 UTC ms, checksum canonical serialization scope specified.
- `SaveSystem.ComputeEnvelopeChecksum()` + `SaveSystem.NowTimestamp()` helpers added.
- Multi-DTO transactional guarantee via single try/catch; `SaveAssemblyFailedException`.
- Steam Cloud R7 body amended; OQ3 resolved to "accepted limitation, documented on store page."
- EC11 added (Addressables broken-key fallback owned by loading systems, not Save).
- EC10 baseline location specified (`.claude/docs/technical-preferences.md` "Project Identity").
- EC13 added (run-end crash window between MasteryState write and RunState deletion).
- Dev Tooling Dependencies section enumerates 6 required hooks.
- All integration ACs labeled with their tool requirement.
- Missing ACs added: IsFreeValveApplied round-trip, chain-ordering preference, concurrent writes.
- AC labels standardized: [Unit test] / [Integration test] / [Code review] / [CI check] / [Smoke check] / [Manual test] / [Cross-system].
- Regression Suite (Minimum Core) section added.
- `NodeMapStateDTO.traversal_list` bound to node indices only.
- SHA-256 thread-local `IncrementalHash` reuse specified.

Deferred:
- Visual/Audio + UI Requirements sections remain light (infrastructure; detailed specs handed to HUD UX).
- Abandon-run mechanic scoped as Node Map concern.
- EC7 idempotency guard remains deferred per OQ2, with explicit `/propagate-design-change` traceability note.

**Next step:** Re-review in a fresh session. Prior blocking items tracked above for the re-review to verify.

---

## Review — 2026-04-21 (second, re-review) — Verdict: APPROVED WITH CARRY-OVERS

Scope signal: M (reduced from L by prior revision)
Specialists: game-designer, systems-designer, unity-specialist, performance-analyst, qa-lead, creative-director (senior synthesis)
Blocking items surfaced: 21 | Recommended surfaced: 19 | Prior verdict resolved: **Yes — all 10 prior blocking items verified applied.**

**Summary:** Re-review surfaced 21 new blocking items across 5 specialists. Creative-director synthesized them into three categories: (1) genuine GDD-body gaps requiring document edits, (2) implementation-specification gaps belonging in an ADR rather than a GDD, (3) adversarial nits inappropriate to block on given solo-dev EA scope. CD's judgment: "The GDD has crossed the 'good enough to build from' threshold with 6 targeted fixes." CD explicitly rejected the game-designer pillar-tension blocker on soft-undo (precedent: Hades, Slay the Spire), converting it into an authored-tension documentation fix instead of a mechanical change.

**6 GDD-body must-fixes applied (second revision, same session):**

1. **F1 flush cap** — R2.2 now caps periodic flushes at 30 per continuous map-screen dwell (resets on re-entry). F1 output range updated (typical 16–33; pathological worst case 53). New AC added for cap enforcement.
2. **R4 rotation prose** — rewritten to clarify step 3 IS the final rename; removed self-contradictory "before the final rename" phrasing.
3. **R3 sharing-violation backoff** — raised from 3×100ms (300ms) to 5 retries with exponential spacing (250, 500, 1000, 2000, 4000 ms — 7.75s total). Sized to clear Windows Defender real-time scan hold windows (500–4000ms). Tuning Knobs safe ranges updated. EC2 integration AC updated.
4. **R5 schema versioning** — split into two-tier policy: permanent semantic (lower = migration candidate, higher = reject) vs. EA-mode policy (any mismatch = incompatible, no migration runtime). EA AC tagged "SCHEDULED FOR INVERSION" with CI-comment requirement at first content patch.
5. **R7 Steam Cloud** — rewritten from (factually wrong) "configure Steamworks to exclude temporaryCachePath" to (correct) Auto-Cloud include-path allowlist description. `temporaryCachePath` tree-separation from `persistentDataPath` documented; no exclusion needed or possible.
6. **N=1 backup risk acknowledgment** — prose added at top of R4 explicitly naming the scope reduction, the RunStateFullLoss risk, and the rationale. Post-EA escalation path to N=2 via Tuning Knob noted.

**Bonus fix (CD-authored recommendation):** Player Fantasy now contains a "Design tension: Quit-to-Resume vs. Pillar 1" acknowledgment paragraph citing Hades/Slay the Spire precedent. Soft-undo stays as-is; the tension is now authored, not buried.

**15 items graduate to carry-over (not blocking this GDD — tracked as Save ADR + implementation-gate ACs):**

- **Save ADR requirements (`docs/architecture/save-system-adr.md` — to author before first save-code commit):** IL2CPP stripping config with `Newtonsoft.Json.Serialization.*` preservation + stripping level Low for Save assembly; SynchronizationContext / main-thread dispatcher mechanism; periodic-flush timer mechanism (PlayerLoop / UniTask / Task.Delay+CancellationToken); dirty-flag ownership and clear-on-confirmed-complete semantics; concurrent-write queue topology.
- **Implementation-gate ACs (add at Save implementation story):** Launch-recovery latency budget AC (HDD + Defender scenario); `Application.quitting` timeout bound AC; Newtonsoft allocation profiler capture requirement; qa-lead AC rewrites (mid-combat quit labeling, hard-kill automation with new `InjectFault(HardKill)`, concurrent-writes testable predicate, clock injection for flush AC); missing-coverage ACs (envelope_version lower, EC7 backstop, EC8 Node.Loading, `Application.quitting` code-review AC); Dev Tooling hook interface specification.
- **OQ4/OQ5 clarifications:** verification must happen on IL2CPP release build, not Editor (which uses Mono).
- **EC11 note:** Addressables handle-release required on exception paths (cross-system note to loading systems).
- **Narrative pass:** MasteryStateFullLoss dialog copy queued for narrative-director tone sweep at UI copy phase.
- **Nice-to-have:** F2 C variable range specification (awaits Node Map GDD OQ1 resolution).

**Key decisions applied in second revision (user-approved, same session):**

- F1 cap strategy: **hard cap at 30 flushes per dwell** (alternative was exponential backoff or unbounded-with-estimate).
- R3 retry profile: **5 retries at 250/500/1000/2000/4000ms = 7.75s** (alternatives: 3.85s or 12s budgets).

**Rationale for APPROVED WITH CARRY-OVERS instead of another MAJOR REVISION cycle (creative-director verdict):** Diminishing returns argument — Save is a support system, not a pillar feature; each revision cycle diverts author time from Combat/Map/Card pillars. Category-appropriateness argument — IL2CPP, dispatcher, Steam Cloud wiring belong in an ADR, not a GDD. Solo-dev signal argument — 21 blocking items is adversarial-review noise; the healthy response is triage, not compliance.

**Next step:** Author Save ADR before first save-code commit. `/design-review design/gdd/status-effects.md` is the next pending review.

