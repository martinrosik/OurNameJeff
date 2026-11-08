# Resort Empire: Paradise Tycoon

A calm mobile resort tycoon, played in landscape. You turn an abandoned tropical island into a luxury resort.
Guests arrive in **waves**. You watch what they need (Rooms, Drinks, Food, Relax, Ocean View), spot where the
resort can't keep up, and move Workers to fix it. After each wave you get a rating with an explanation and
invest the reward in upgrades and new areas. Income comes from handling waves well, never from waiting.
3–7 minute sessions, no punishment for being away.

**Current phase:** Prototype. Goal: prove the wave loop feels good: *I see the problem → I decide → it gets better.*

## Design documents (source of truth)

| Document | What it is |
|---|---|
| [Kickoff deck](https://docs.google.com/presentation/d/1HTw437iiV31J2T-4Cooc49EmVjhuKMZfXkHWuuNkRYA/edit) | The game in 9 minutes: player, feel, one wave, prototype vs. MVP |
| [GDD](https://docs.google.com/document/d/1GA5ZrfYrmtqqby1wVPyE2eNXl7jFHgJTcp2hweQUoc0/edit) | Compact overview: pillars, non-goals, core loop, currencies |
| [Build doc: Prototype → MVP](https://docs.google.com/document/d/10X2j8p_AwRmr4103MdLnX0faVNob0-8Sy5otXSyd6QA/edit) | Scope, state machines, wave/needs model, numbers, acceptance criteria |
| [UI screens S1–S9](https://docs.google.com/document/d/15Fl0n6qk64ITsSY-o66Ppj-vO_J84USLn_hisbE7i3U/edit) | Each screen's goal, main CTA, states |
| [Figma](https://www.figma.com/design/whlebVoyEKoW4emX7kdR2B/Resort-Empire) | Visual designs (frame size 852×393, landscape) |

Where the Figma and the docs disagree, check [docs/design-conflicts.md](docs/design-conflicts.md). The plan of
work is in [docs/roadmap.md](docs/roadmap.md).

## Requirements

- **Unity 6000.6.5f1** exactly (install it through Unity Hub with **Android Build Support**). Opening the
  project in any other version changes dozens of files. Don't commit that.
- **Git LFS**: it ships with Git for Windows. Run `git lfs install` once per machine.
- An IDE: Rider or Visual Studio (both supported by the installed packages).

## Getting started

```bash
git lfs install
git clone https://github.com/martinrosik/OurNameJeff.git
```

1. Unity Hub → **Add** → pick the cloned folder → open with **6000.6.5f1**. The first import takes a few minutes.
2. Open the game scene: menu **Resort Empire → Open Game Scene** (or `Assets/_Project/Scenes/Game.unity`).
3. In the Game view, add a fixed resolution **852×393** (the Figma frame) and pick it, so you see what the designs show.
4. Run the tests: **Window → General → Test Runner** → EditMode and PlayMode → Run All.

Before your first commit, do the one-time git setup in [CONTRIBUTING.md](CONTRIBUTING.md#one-time-setup).

**No sprites or icons in the scene?** The project was cloned without Git LFS, so the images are only text
pointers. Unity says so in a dialog when it opens. In the project folder run:

```bash
git lfs install
git lfs pull
```

Then switch back to Unity; it reimports the art by itself.

## What's in the game now

Sprint "OUR Sprint 2" is split across the team. This part is built:

| Ticket | In the game |
|---|---|
| OUR-31 | The island with Reception, Restaurant and Beach Bar (placeholder art), palms, dock, guest boat |
| OUR-48 | Worker indicator: a green **W** badge, or a grey badge and "No Worker" (one building always starts without a Worker) |
| OUR-50 | Capacity indicator: one seat square per seat (3 at level 1) |
| OUR-33 | The forecast before the wave: how many guests need Rooms, Drinks and Food |
| OUR-27 | **Welcome Guests** starts the wave: the boat sails in and the status says "WAVE 1 IN PROGRESS" |
| OUR-34 | Guests arrive evenly over 90 s (Rooms 8, Drinks 8, Food 6 in wave 1). Animated guests walk from the boat to their buildings, a guest can visit several buildings, seats and queue squares fill when they get there |
| OUR-41 / OUR-35 | After 90 s nobody new comes but nobody leaves. When everyone is served the guests walk back to the boat ("GUESTS LEAVING") and the wave completes. Needs service (OUR-46) to happen in play |

Open `Game.unity` and press **Play** (Game view at **852×393** or **1704×786**). The wave stays in progress for
now: guests, service, the needs panel, moving Workers and the wave end are other teammates' tickets. Each has a
guide in [docs/tasks/](docs/tasks/README.md).

Placeholder art comes from [Kenney](https://kenney.nl) (CC0, public domain): *City Kit (Suburban)* for the
houses, *Pirate Kit* for palms, rocks, the dock and the ship, and *Mini Characters* for the guests who walk
from the dock to their building (13 ordinary people, walk cycle baked into `Art/Guests` with
**Resort Empire → Art → Bake Guest Characters**, `GuestSpriteBaker`). The 3D models were rendered to PNGs with
**Resort Empire → Art → Render Selected Models To PNG** (`ModelSpriteRenderer`); only the PNGs and the license are
in `Assets/_Project/Art/Kenney`. All of it gets replaced in the art pass.
If you change the island layout code, **Resort Empire → Greybox → Rebuild Greybox Scene** regenerates the
Game, Island and UI objects (manual edits to those three are lost).

## Project layout

```
Assets/
  _Project/                     everything we make lives here
    Art/        Island, Buildings, Guests, UI, Icons, Fonts
    Audio/      Music, SFX
    Data/       ScriptableObject configs (needs, buildings, wave table, tuning)
    Prefabs/
    Scenes/     Game.unity
    Scripts/
      Core/     ResortEmpire.Core: pure C# simulation (waves, needs, workers, economy), no MonoBehaviours
      Game/     ResortEmpire.Game: MonoBehaviours that run Core in the scene, state machine, save/load
      UI/       ResortEmpire.UI: screens S1–S9, toasts, HUD (uGUI + TextMeshPro)
      Editor/   ResortEmpire.Editor: editor tools and menus
    Tests/
      EditMode/ fast tests, mostly for Core
      PlayMode/ tests that need a running scene
  Settings/                     URP render pipeline assets (from the template, keep here)
  InputSystem_Actions.inputactions
```

Assembly references only point "down": `UI → Game → Core`. Core never references Game or UI.

## Tech

- Unity 6.6, **URP 2D renderer**, **2D sprites** (pre-rendered isometric look like the Figma)
- **Input System** only (old Input Manager is off)
- **uGUI + TextMeshPro** for UI
- Landscape only, Android first (IL2CPP, ARM64). Bundle ID `com.ournamejeff.resortempire`
- Unity Test Framework for EditMode/PlayMode tests
- `com.unity.pipeline`: lets the `unity` command-line tool drive the Editor (scenes, assets, tests). Editor-only.
