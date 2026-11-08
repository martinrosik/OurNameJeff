# Sprint task guides

One guide per sprint ticket that isn't built yet. Each says what the player should see, the exact rules and
numbers from the [Build doc](https://docs.google.com/document/d/10X2j8p_AwRmr4103MdLnX0faVNob0-8Sy5otXSyd6QA/edit)
(§ references), where the code goes, what "done" means, and which tests to write. Give the guide to your Claude
model as context (see the prompt template below).

## What already exists

| Ticket | What's in the game |
|---|---|
| OUR-31 | Island with Reception, Restaurant, Beach Bar (Kenney placeholder art), palms, dock, guest boat |
| OUR-27 | **Welcome Guests** button: `AWAITING_WAVE → WAVE_INCOMING` (boat sails in) `→ WAVE_ACTIVE`. The wave then stays in progress. |
| OUR-33 | Forecast before the wave: "Next: Wave 1 · 12 guests · Rooms 8 · Drinks 8 · Food 6", with which building has a Worker |
| OUR-48 | Worker badge next to each building: green **W**, or grey **-** plus "No Worker" |
| OUR-50 | Capacity: one seat square per seat (3 at level 1). Seats fill from `ResortGame.GetSeatedCount()`, which returns 0 until guests exist |

Read [docs/development.md](../development.md) and [docs/simulation.md](../simulation.md) first: the code layout, the
config asset, and how to drive Unity from the command line.

## Tickets and order

```
OUR-34 guests arrive over 90 s
   └─► OUR-46 staffed building serves at full speed  ──► OUR-47 unstaffed building is slower
            └─► OUR-41 serve the last guests after 90 s ──► OUR-35 wave ends when everyone is served ──► OUR-40 tell the player
            └─► OUR-38 overloaded building warning ──► OUR-39 "catching up" state
   OUR-37 needs side panel (needs OUR-34; shows OUR-38/39 states once they exist)
   OUR-45 move Workers before and during the wave (matters once OUR-47 exists)
```

| Ticket | Guide | Needs first |
|---|---|---|
| OUR-34 Generovanie návštevníkov počas 90 s od prijatia vlny | [OUR-34.md](OUR-34.md) | nothing |
| OUR-46 Budova s prítomným pracovníkom beží naplno | [OUR-46.md](OUR-46.md) | OUR-34 |
| OUR-47 Budova bez pracovníka beží v spomalenom režime | [OUR-47.md](OUR-47.md) | OUR-46 |
| OUR-41 Doobsluhovanie posledných návštevníkov po ukončení vlny | [OUR-41.md](OUR-41.md) | OUR-34, OUR-46 |
| OUR-35 Ukončenie vlny do 2 minút | [OUR-35.md](OUR-35.md) | OUR-41 |
| OUR-40 Informovanie hráča o ukončení vlny | [OUR-40.md](OUR-40.md) | OUR-35 |
| OUR-38 Zobrazenie preťaženia budov pri veľkom dopyte | [OUR-38.md](OUR-38.md) | OUR-34, OUR-46 |
| OUR-39 Zmena stavu z preťaženia na pozitívny stav | [OUR-39.md](OUR-39.md) | OUR-38 |
| OUR-37 Zobrazovanie potrieb hostí na bočnom paneli | [OUR-37.md](OUR-37.md) | OUR-34 |
| OUR-45 Možnosť presúvania pracovníkov pred aj počas vlny | [OUR-45.md](OUR-45.md) | OUR-27 (works alone; meaningful with OUR-47) |

Features outside this sprint (satisfaction and stars, Coins/Energy, Speed Up, Sudden rush, upgrades, unlocks,
pause/save, balance check) are listed in [later.md](later.md).

## How to work

1. Pull `main`, then create a branch from it named `<you>-feature-<short-description>` (or `<JIRA-KEY>-...`).
2. Open the project in Unity **6000.6.5f1**. Scene: menu **Resort Empire → Open Game Scene**.
3. Code goes where the guide says. The hard rules, in short:
   - **Core** is plain C#: no MonoBehaviours, no scene access.
   - **Numbers** go into the config asset (`Assets/_Project/Data/Config_Prototype.asset`), never into code.
   - **The word "idle" is banned** (a test fails on it).
4. Add the tests the guide lists, then run all tests (**Window → General → Test Runner**, EditMode and PlayMode).
5. Commit messages start with the Jira key: `OUR-34 Spawn guests evenly over the arrival phase`.
6. Open a pull request into `main`.

Several tickets touch `ResortGame.Tick()` and the side panel, so merge often and talk before two people edit the
same file. Each ticket has its own `// OUR-xx:` comment in `ResortGame.Tick()` marking where it plugs in.

## Prompt template for Claude Code

Paste this, with your ticket number, into Claude Code started in the repo folder:

```
Read docs/development.md, docs/simulation.md and docs/tasks/OUR-XX.md.
Implement OUR-XX exactly as the guide describes. Follow its rules and numbers from the Build doc, put all
tunable numbers into GameConfig (Config_Prototype.asset + PrototypeConfigDefaults), keep Core free of
MonoBehaviours, and add the tests the guide lists.
Ask me before deciding anything the guide leaves open, or anything that conflicts with docs/design-conflicts.md.
When done: no Console errors, all EditMode and PlayMode tests pass, then show me a screenshot of the Game view.
```

## Things that bit us (read before you start)

- **Frame-rate independence:** simulate in steps driven by events (next arrival, next service end) rather than
  per frame, or results change with the frame rate. A test that plays the same wave with `Tick(1/60)` and
  `Tick(0.7)` should give the same numbers.
- **Float config vs double math:** a threshold stored as `0.55f` is `0.5500000119` as a double. Compare with a
  small tolerance (1e-6).
- **TextMeshPro font:** LiberationSans SDF has no ★, ⚠, ⚡ or "…" glyphs. Use plain text.
- **Unity from the CLI:** Play mode freezes at frame 1 when the Editor window isn't focused. Run
  `unity command editor set_auto_tick` and `unity command editor.playmode set_run_in_background` first (see docs/development.md).
- **Editor scripts:** load assets after `EditorSceneManager.OpenScene`, or the references get saved as missing.
