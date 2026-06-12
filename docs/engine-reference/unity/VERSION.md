# Unity Engine — Version Reference

| Field | Value |
|-------|-------|
| **Engine Version** | Unity 6.3 LTS |
| **Release Date** | December 2025 |
| **Project Pinned** | 2026-04-18 |
| **Last Docs Verified** | 2026-04-18 |
| **LLM Knowledge Cutoff** | May 2025 |

## Knowledge Gap Warning

The LLM's training data likely covers Unity up to ~2022 LTS (2022.3). The entire
Unity 6 release series (formerly Unity 2023 Tech Stream) introduced significant
changes that the model does NOT know about. Always cross-reference this directory
before suggesting Unity API calls.

## Post-Cutoff Version Timeline

| Version | Release | Risk Level | Key Theme |
|---------|---------|------------|-----------|
| 6.0 | Oct 2024 | HIGH | Unity 6 rebrand, new rendering features, Entities 1.3, DOTS improvements |
| 6.1 | Nov 2024 | MEDIUM | Bug fixes, stability improvements |
| 6.2 | Dec 2024 | MEDIUM | Performance optimizations, new input system improvements |
| 6.3 LTS | Dec 2025 | HIGH | First LTS since 6.0, production-ready DOTS, enhanced graphics features |

## Major Changes from 2022 LTS to Unity 6.3 LTS

### Breaking Changes
- **Entities/DOTS**: Major API overhaul in Entities 1.0+, complete redesign of ECS patterns
- **Input System**: Legacy Input Manager deprecated, new Input System is default
- **Rendering**: URP/HDRP significant upgrades, SRP Batcher improvements
- **Addressables**: Asset management workflow changes
- **Scripting**: C# 9 support, new API patterns

### New Features (Post-Cutoff)
- **DOTS**: Production-ready Entity Component System (Entities 1.3+)
- **Graphics**: Enhanced URP/HDRP pipelines, GPU Resident Drawer
- **Multiplayer**: Netcode for GameObjects improvements
- **UI Toolkit**: Production-ready for runtime UI (replaces UGUI for new projects)
- **Async Asset Loading**: Improved Addressables performance
- **Web**: WebGPU support

### Deprecated Systems
- **Legacy Input Manager**: Use new Input System package
- **Legacy Particle System**: Use Visual Effect Graph
- **UGUI**: Still supported, but UI Toolkit recommended for new projects
- **Old ECS (GameObjectEntity)**: Replaced by modern DOTS/Entities

## Verified Sources

- Official docs: https://docs.unity3d.com/6000.0/Documentation/Manual/index.html
- Unity 6 release: https://unity.com/releases/unity-6
- Unity 6.3 LTS announcement: https://unity.com/blog/unity-6-3-lts-is-now-available
- Migration guide: https://docs.unity3d.com/6000.0/Documentation/Manual/upgrade-guides.html
- Unity 6 support: https://unity.com/releases/unity-6/support
- C# API reference: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/index.html
- Unity 6.3 upgrade guide: https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html
- What's new in Unity 6.3: https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html

## Critical Flags for Wasteland Run (2D Card Roguelike, URP, C#)

Verified 2026-04-18 against official Unity 6.3 upgrade guide:

1. **URP Compatibility Mode REMOVED in 6.3** — Must use Render Graph API (`RecordRenderGraph`). Old `ScriptableRenderPass.Execute` patterns will NOT compile. See `breaking-changes.md`.
2. **`[SerializeField]` fields-only in 6.3** — Cannot apply to properties/methods. Use `[field: SerializeField]` for auto-implemented properties.
3. **`Object.FindObjectsOfType` → `Object.FindObjectsByType<T>(FindObjectsSortMode.None)`** — Old API deprecated since 6.0; `None` sort is significantly faster.
4. **UI Toolkit USS parser stricter in 6.3** — Invalid USS now produces errors. Audit all `.uss` card and HUD stylesheets.
5. **Box2D v3 in 6.3** — New `UnityEngine.LowLevelPhysics2D` namespace. 2D physics behavior may differ subtly.
6. **New Input System is default** — Legacy `Input` class deprecated. All input code must use `UnityEngine.InputSystem`.
