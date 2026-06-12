# Epics Index

Last Updated: 2026-05-21
Engine: Unity 6.3 LTS

| Epic | Layer | System | GDD | Stories | Status |
|------|-------|--------|-----|---------|--------|
| [Card System](card-system/EPIC.md) | Foundation | Cards | design/gdd/card-system.md | Not yet created | Ready |
| [Vehicle POCO + Part Catalog](vehicle-poco-part-catalog/EPIC.md) | Foundation | Vehicle (data) | design/gdd/vehicle-and-part-architecture.md + vehicle-and-part-mechanics.md | Not yet created | Ready |
| [Vehicle Visual Layer](vehicle-visual-layer/EPIC.md) | Foundation | Vehicle (view) | design/gdd/vehicle-and-part-architecture.md + vehicle-and-part-mechanics.md | Not yet created | Ready ⚠️ ADR-0008 Proposed |
| [Save & Persistence](save-persistence/EPIC.md) | Foundation | Persistence | design/gdd/save-persistence.md | Not yet created | Ready |

## TR Coverage Summary

| Epic | TRs | Covered | Untraced |
|------|-----|---------|----------|
| Card System | 25 | 25 | 0 |
| Vehicle POCO + Part Catalog | 25 | 25 | 0 |
| Vehicle Visual Layer | 0 (ADR-driven ACs) | — | 0 |
| Save & Persistence | 25 | 24 | 1 (TR-save-025 — partial, ADR-0008 pending) |

## Foundation Layer Gate

Foundation epics are a prerequisite for the Pre-Production → Production gate.
Once all Foundation stories are Done, run `/gate-check production`.
