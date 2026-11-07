# CLAUDE.md

Context for AI coding assistants (Claude Code and others) working in this repository.
Read [README.md](README.md) and [CONTRIBUTING.md](CONTRIBUTING.md) too. Everything there applies to you.

## What this is

**Resort Empire: Paradise Tycoon**, a calm, landscape mobile resort tycoon in Unity 6000.6.5f1 (URP 2D, 2D sprites).
Guests arrive in waves. The player manages the *system*, not individual guests: reads the needs panel, moves
anonymous Workers to bottlenecked buildings, then upgrades between waves. Current phase: **Prototype**
(5 buildings, 5 needs, 8 scripted waves, screens S1–S5 + S9). VIP, Reputation, Materials, Gems and Managers are **MVP**:
don't build them early.

## Source of truth

Mechanics, numbers and screen behavior come from the Google Docs (version IV). They are written in Slovak.
In-game strings and identifiers in them are English and go into code as-is.

- GDD: https://docs.google.com/document/d/1GA5ZrfYrmtqqby1wVPyE2eNXl7jFHgJTcp2hweQUoc0/edit
- Build doc (Prototype → MVP: state machines, wave/needs model, §4.8 parameter table, WR1–WR9, AC1–AC8):
  https://docs.google.com/document/d/10X2j8p_AwRmr4103MdLnX0faVNob0-8Sy5otXSyd6QA/edit
- UI screens S1–S9: https://docs.google.com/document/d/15Fl0n6qk64ITsSY-o66Ppj-vO_J84USLn_hisbE7i3U/edit
- Kickoff deck: https://docs.google.com/presentation/d/1HTw437iiV31J2T-4Cooc49EmVjhuKMZfXkHWuuNkRYA/edit
- Figma (look only): https://www.figma.com/design/whlebVoyEKoW4emX7kdR2B/Resort-Empire

A public doc can be read as plain text with `.../export?format=txt` (Docs) or `.../export/txt` (Slides).

**Figma vs. docs conflicts are decided by the designer, case by case.** Never resolve one silently.
Check [docs/design-conflicts.md](docs/design-conflicts.md). If your task hits an Open row, ask the user. If you find
a new conflict, add a row and ask.

## Design rules that shape code

- Pillars: calm decisions (no reflexes), never punish absence, cause → effect → investment, manage the system
  not individuals, a short session is a complete experience, growth is visible on the island.
- Non-goals: not idle (no passive income ticking, no coin collecting), not match-3/merge, not time-pressure
  stress, no 15-stat spreadsheets, never handle 30 guests by hand.
- **The word "idle" is banned** in code, assets and docs (project rules §6). The resting state is `AWAITING_WAVE`.
  `TerminologyTests` enforces this for `Assets/_Project/Scripts`.
- The wave pauses when the app is backgrounded (`WAVE_PAUSED`). Nothing progresses offline, nothing decays.
- Hard rules: Energy is an optional boost, never a gate. 5★/Perfect must be reachable without Energy (HR2).
  Moving a Worker is always free and instant (WR5). No upgrades during `WAVE_ACTIVE` (WR6).
- Every tunable number from the Build doc (`TUNING`, `e.g.`) goes into a ScriptableObject config, never into code.

## Code layout

| Assembly | Folder | Rules |
|---|---|---|
| `ResortEmpire.Core` | `Assets/_Project/Scripts/Core` | Pure C# simulation. No MonoBehaviours, no scene access. Deterministic `Tick(dt)`. |
| `ResortEmpire.Game` | `Assets/_Project/Scripts/Game` | MonoBehaviours, state machine driver, save/load. References Core. |
| `ResortEmpire.UI` | `Assets/_Project/Scripts/UI` | uGUI + TextMeshPro screens. References Core and Game. Reads state, sends intents. |
| `ResortEmpire.Editor` | `Assets/_Project/Scripts/Editor` | Editor-only tools. |
| `ResortEmpire.Tests.EditMode` / `.PlayMode` | `Assets/_Project/Tests/…` | NUnit. Core logic is tested in EditMode. |

Core/Game/UI expose internals to the test assemblies (`AssemblyInfo.cs`).

## Driving Unity from the CLI

The project includes `com.unity.pipeline`, so the `unity` CLI can control a running Editor:

```bash
unity status --until-ready --project-path <project>           # is the Editor ready?
unity command script eval --file <file.cs> --caller plugin --skill unity-cli
unity command console status --caller plugin --skill unity-cli  # compile errors / console counts
unity command test run --mode all --filter ResortEmpire --filter-type assembly --caller plugin --skill unity-cli
unity command test status --caller plugin --skill unity-cli     # PlayMode results arrive async
```

- Edit scenes, prefabs and ScriptableObjects through the live Editor (`script eval`), not by hand-editing YAML.
- On Windows PowerShell, pass C# with `--file` rather than `--code`: PowerShell 5.1 mangles embedded quotes.
- After adding or changing scripts, the Editor recompiles and reloads. A command sent during the reload can drop
  ("closed the connection"). Wait with `unity status --until-ready`, then check whether it ran before repeating it.
- Pass `--project-path` (or `UNITY_PROJECT_PATH`) when more than one Editor might be open, for example in worktrees.

## Before you finish a task

- The Console has no errors (`console status` → `compilationFailed: false`, `consoleErrors: 0`).
- EditMode and PlayMode tests pass.
- `git diff` only contains what the task needs. Revert incidental `ProjectSettings/` churn you didn't intend.
