# Claude Code Game Studios -- Game Studio Agent Architecture

Indie game development managed through 48 coordinated Claude Code subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## Technology Stack

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Version Control**: Git with trunk-based development
- **Build System**: Unity Build Pipeline
- **Asset Pipeline**: Unity Asset Import Pipeline + Addressables

> **Note**: Engine-specialist agents exist for Godot, Unity, and Unreal with
> dedicated sub-specialists. Use the set matching your engine.

## Project Structure

@.claude/docs/directory-structure.md

## Engine Version Reference

@docs/engine-reference/unity/VERSION.md

## Technical Preferences

@.claude/docs/technical-preferences.md

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

## Capture-Before-Destroy + Technical Director Review (ENFORCED BY HOOK)

Before any destructive edit to authored content or any system refactor /
new system ≥50 lines, Claude MUST:

1. Spawn `technical-director` agent with current state, proposed change,
   files at risk, and the final-game picture the change serves.
2. Write a capture file at `production/polish-captures/<YYYY-MM-DD>-<system>.md`
   enumerating every authored value being destroyed and pasting the TD verdict
   under a `## Technical Director Review` heading.
3. Get user approval on the capture before editing.

This is enforced by `.claude/hooks/capture-before-destroy.sh`. The hook blocks
Edit/Write/MultiEdit on protected paths (prefabs, scenes, author scripts,
designer-tuned SOs, GDDs, ADRs, system-shape carriers) when destructive
thresholds are crossed and no matching capture file exists for today.

Full protocol: `production/polish-captures/README.md`.

> **First session?** If the project has no engine configured and no game concept,
> run `/start` to begin the guided onboarding flow.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

@.claude/docs/context-management.md
