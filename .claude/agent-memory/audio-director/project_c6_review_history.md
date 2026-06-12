---
name: C6 Review History
description: V&P Mechanics C.6 audio/visual feedback section review history and open issues
type: project
---

Section: `design/gdd/vehicle-and-part-mechanics.md` §C.6 "Audio and Visual Feedback"

R1 verdict (2026-05-20): NOT production-actionable. Blocking issues: no tier parameters, Critical state loop contract unspecified, bus priority/ducking rules missing, OnArmorExposed break stinger row absent.

W1+W2 applied (2026-05-20): W1 = editorial (formula authority transfers, vocabulary). W2 = creative (hybrid damage arc, Offline zone, E.9 forced-pass). C.6 table was NOT substantively changed in W1 or W2 — the same table text exists post-W2 as pre-W1. W3 (spec hardening: AC rework, audio tier parameters, Critical loop contract, UX dialog) explicitly deferred.

Re-review post-W2: C.6 is still not production-actionable. All four R1 blocking issues remain open. Additional issues surfaced: simultaneous feedback audio/visual sequencing ambiguity, Offline zone zone change invalidates "cards grey simultaneously" note, schema-drift has no audio spec, E.9 forced-pass has no audio spec, Degraded entry is intentionally silent but undocumented as such, "relief audio cue" and "distinct SFX" are intent statements not specs, Critical loop exit event is unspecified.

W3 applied (2026-05-20): W3 added the "Audio spec note (W3g)" callout and the "simultaneous feedback rule." It did NOT resolve any of the four R1 blocking issues. The table rows themselves are unchanged from pre-W3.

Re-review post-W3 (2026-05-20): Five blocking issues confirmed. Full findings:
- BLOCKER: Critical loop — exit event, polyphony rule, and Destroyed-while-looping behavior all unspecified.
- BLOCKER: Heavy-tier cues (Slot Destroyed, Slot repaired from Offline) — "positive sonic direction required" is a placeholder, not a brief. Missing: palette callout, emotional target, duration class, pitched vs. atonal.
- BLOCKER: Bus priority and ducking rules entirely absent. Simultaneous Heavy + loop events have no audio resolution path.
- BLOCKER: OnArmorExposed break stinger absent from the table entirely. Not marked Silent — just missing. Must be confirmed Silent or added as a row.
- BLOCKER: Three "spec TBD W3g" rows (forced-pass, schema-drift, non-Offline repair) cannot be briefed from tier label alone.
- RECOMMENDED: "Simultaneous layering" rule needs bus-level clarification for same-frame Heavy+Heavy collisions.
- MINOR: Repair-from-Offline Heavy cue and "subtle chime" row on same event — relationship between them undefined.
- MINOR: Non-Offline repair Light row indistinguishable from the chime row — same-asset vs. separate-asset not specified.

**Why:** W3g audio pass explicitly deferred; the W3g note in the doc is an acknowledgment of the gap, not a resolution.
**How to apply:** Do not treat C.6 as a valid brief for sound-designer delegation. All five blockers must be resolved in the W3g pass before any audio production can begin from this section.
