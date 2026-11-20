# Developer guide

Practical notes for working in this repo: the rules that shape the code, the code layout, tools and pitfalls.
Read [README.md](../README.md) and [CONTRIBUTING.md](../CONTRIBUTING.md) too. This is also the file to give an AI
coding assistant as context.

## What this is

**Resort Empire: Paradise Tycoon**, a calm, landscape mobile resort tycoon in Unity 6000.6.5f1 (URP 2D, 2D sprites).
Guests arrive in waves. The player manages the *system*, not individual guests: reads the needs panel, moves
anonymous Workers to bottlenecked buildings, then upgrades between waves. Current phase: **Prototype**
(5 buildings, 5 needs, 8 scripted waves, screens S1–S5 + S9). VIP, Reputation, Materials, Gems and Managers are
**MVP**: don't build them early.

## Current state and sprint tickets

The team splits the work by Jira ticket (OUR Sprint 2). Built so far:
- **OUR-31**: island + Reception, Restaurant, Beach Bar
- **OUR-27**: Welcome Guests starts the wave
- **OUR-33**: forecast
- **OUR-48**: Worker indicator
- **OUR-50**: capacity seats
- **OUR-34, OUR-41, OUR-35**: guests arrive over 90 s, the last ones are served, the wave ends
- **OUR-46, OUR-47**: service with and without a Worker
- **OUR-37, OUR-38, OUR-39**: Wave Needs Panel with "queue growing" and "catching up"
- **OUR-45**: moving Workers
- **OUR-40** and the wave evaluation: Wave Complete with rating stars, Wave Satisfaction % and Guests served
- **Wave difficulty**: every wave is harder than the one before it (`WaveProgression`)
- **Tutorial**: a practice wave with speech bubbles in `Tutorial.unity`, then the real game (see [Tutorial](#tutorial))
- **Settings**: a gear in the top-right corner with Sound ON/OFF and PLAY TUTORIAL AGAIN (see [Greybox](#greybox))

**When working on a ticket, read its guide in [tasks/](tasks/README.md) first.** It holds the Build-doc rules,
numbers, where the code goes and the tests to write. Each continuing ticket has a `// OUR-xx` hook in
`ResortGame.Tick()`. Don't build other people's tickets as a side effect; ask if a task seems to need one.

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
Check [design-conflicts.md](design-conflicts.md). If your task hits an Open row, ask. If you find a new conflict, add
a row and ask.

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

The core (`ResortGame`, `GameConfig`, `BuildingRuntime`, `WaveForecast`) and the planned wave model are described
in [simulation.md](simulation.md). Tunable numbers live in `Assets/_Project/Data/Config_Prototype.asset`.
`PrototypeConfigDefaults` (Editor) holds the Build doc baseline used to create that asset and in tests. A new
setting goes into the settings class, `PrototypeConfigDefaults` and the asset.

## Greybox

- `Scripts/Game/GameController` owns the `ResortGame` and ticks it.
- `Scripts/Game/Greybox/*` are the island views (`IslandView`, `BuildingView` with seats and Worker badge, `BoatView`).
- `Scripts/UI/Greybox/*` is uGUI built from code at runtime: the `GreyboxUI` root plus plain view classes (`HudView`
  today). New screens follow the same pattern; `GreyboxUI.SideArea` is reserved for the needs panel (OUR-37).
  All of it is meant to be replaced by Figma-based prefabs in the art pass.
- Settings: `SettingsView` puts a gear in the top bar's right corner (sprite from **Resort Empire → Greybox → Generate
  Settings Icon**). Its menu has Sound ON/OFF and PLAY TUTORIAL AGAIN, and the wave waits (`Time.timeScale = 0`) while
  it's open. `SoundSettings` (Game) keeps the sound choice in PlayerPrefs and applies it to `AudioListener.volume` at
  startup, so sounds added later follow it with no extra code. The game has no sounds yet.
- `Scripts/Editor/GreyboxSceneBuilder` (menu **Resort Empire → Greybox → Rebuild Greybox Scene**) regenerates the
  Game/Island/UI roots in `Game.unity`, then rebuilds `Tutorial.unity` from it. `GreyboxArtGenerator` makes the white
  placeholder sprites.
- Text uses only characters LiberationSans SDF has. No ★ ⚠ ⚡ or ellipsis glyphs.
- Placeholder art: Kenney CC0 renders in `Art/Kenney/{City,Pirate}`, made with `Scripts/Editor/Art/ModelSpriteRenderer`
  (isometric 30°/45° orthographic bake, `ModelSpriteBake` shader, 3× supersampling, trimmed). To add more, import the
  model temporarily, call `ModelSpriteRenderer.Render(model, colormap, "Assets/_Project/Art/...png")`, set the PNG to
  Sprite (PPU 256), and delete the model again. Only PNGs go into the repo.

## Tutorial

Not in the design docs; the team added it to the sprint. It plays a short practice wave on the same island, explains
each step in a speech bubble with an arrow, and then opens the real game at wave 1.

- **Scenes.** `Game.unity` stays the first scene. `Tutorial.unity` is a copy of it plus a `Tutorial` root with
  `TutorialController`; **Resort Empire → Greybox → Rebuild Tutorial Scene** makes the copy again (Rebuild Greybox
  Scene does it too). Don't edit `Tutorial.unity` by hand; change `Game.unity` or the builder instead.
- **When it runs.** A build opens the tutorial on its first launch (`FirstLaunchTutorial`) until the player finishes
  or skips it; `TutorialProgress` remembers that in PlayerPrefs. **PLAY TUTORIAL AGAIN** in the settings replays it
  at any time, also during a wave. The Tutorial scene hides the settings gear (`GreyboxUI.SettingsAvailable`): SKIP
  TUTORIAL sits in that corner.
- **Coming back.** Before the Tutorial scene opens, the player's game is parked in memory (`ParkedGame`), frozen like
  the Build doc's WAVE_PAUSED: nothing progresses, nothing gets worse. The Game scene's `GameController` picks it up
  again instead of starting a new game (`IsResumed`), and `GuestCrowdView.CatchUp` walks the guests who had arrived
  straight back into their buildings. The tutorial's last button then says **BACK TO MY RESORT**. Nothing is saved to
  disk yet (save/load is on the roadmap), so quitting the app still starts over. In the Editor, Play starts whichever scene is open, so open
  `Tutorial.unity` (**Resort Empire → Open Tutorial Scene**) to play it. **Resort Empire → Reset Tutorial Progress**
  forgets that it was played.
- **Steps.** `TutorialScript` (Core) lists the steps and what each waits for: NEXT, a Worker move, Welcome Guests, a
  building falling behind, the wave end. It follows the game, so starting the wave early skips ahead. The wave pauses
  (`Time.timeScale = 0`) while a bubble explains something during it, and while it waits for the overload fix.
- **One thing at a time.** While a step asks for a Worker move, only the building the bubble points at opens its
  Worker sheet (`TutorialScript.AllowsWorkerSheet`, applied through `GreyboxUI.CanOpenWorkerSheet`); a tap on another
  building shakes the bubble. Welcome Guests stays greyed until the first Worker move is done (`GreyboxUI.CanStartWave`).
- **Practice wave.** `Assets/_Project/Data/Config_Tutorial.asset` (`TutorialConfig`, defaults in
  `TutorialConfigDefaults`): 9 Rooms, 9 Drinks, 9 Food over 40 s, played with the prototype config's rules. Whichever
  building has no Worker falls behind at about 36 s, and a Worker turns it to "catching up" at once.
  `TutorialConfigTests` checks that for both config assets, so retuning either one can't quietly break the tutorial.
- **Look.** `TutorialView` draws the bubble, the curved arrow, the pulsing frame and SKIP TUTORIAL on its own canvas
  above the greybox UI. `TutorialLayout` places the bubble beside its target, clear of the other buildings and panels.
  Bubble texts live in `GameText`. The arrow and frame sprites come from **Resort Empire → Greybox → Generate Tutorial
  Sprites** (shapes in `TutorialArt`); the host is a baked guest character.

## Driving Unity from the command line

The project includes `com.unity.pipeline`, so Unity's `unity` command-line tool can control a running Editor:

```bash
unity status --until-ready --project-path <project>       # is the Editor ready?
unity command script eval --file <file.cs>                # run C# in the Editor
unity command console status                              # compile errors / console counts
unity command test run --mode all --filter ResortEmpire --filter-type assembly
unity command test status                                 # PlayMode results arrive async
```

- Edit scenes, prefabs and ScriptableObjects through the live Editor (`script eval`), not by hand-editing YAML.
- On Windows PowerShell, pass C# with `--file` rather than `--code`: PowerShell 5.1 mangles embedded quotes.
- After adding or changing scripts, the Editor recompiles and reloads. A command sent during the reload can drop
  ("closed the connection"). Wait with `unity status --until-ready`, then check whether it ran before repeating it.
- Pass `--project-path` (or `UNITY_PROJECT_PATH`) when more than one Editor might be open.
- **Play mode from the command line freezes at frame 1 while the Editor window is unfocused.** Before
  `editor.playmode start`, run `unity command editor set_auto_tick` and
  `unity command editor.playmode set_run_in_background`. Prove frames advance with
  `unity command job wait -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}'`.
  Afterwards turn both off again (`--enabled false`).
- For screenshots, set the Game view to the Figma frame first:
  `UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1704, 786, "Figma 852x393 @2x")` via `script eval`,
  then `unity command capture game_view --output-path <png>` (source=screen; camera captures mis-scale overlay UI).
- In editor scripts, load assets *after* `EditorSceneManager.OpenScene`: opening a scene unloads unused assets,
  and references loaded before it get saved as missing.

## Before you finish a task

- The Console has no errors (`console status` → `compilationFailed: false`, `consoleErrors: 0`).
- EditMode and PlayMode tests pass.
- `git diff` only contains what the task needs. Revert incidental `ProjectSettings/` churn you didn't intend.
