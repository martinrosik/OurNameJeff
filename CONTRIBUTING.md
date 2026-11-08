# Contributing

## One-time setup

1. Install **Unity 6000.6.5f1** with Android Build Support, and Git (Git for Windows includes Git LFS).
2. Enable Git LFS:
   ```bash
   git lfs install
   ```
3. Register Unity's **Smart Merge** driver, so git can merge scenes and prefabs. `.gitattributes` already
   points Unity YAML files at it, but each machine has to say where the tool is. On Windows:
   ```bash
   git config --global merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
   git config --global merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -h -p --force %O %B %A %A"
   git config --global merge.unityyamlmerge.recursive binary
   ```
   On macOS the tool is at `/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/Tools/UnityYAMLMerge`.

## Branches, commits, pull requests

- `main` is always openable and playable. Nobody pushes to it directly. Everything goes through a pull request.
- One branch per Jira ticket, named `<JIRA-KEY>-short-description`, for example `RE-12-wave-needs-panel`.
- Commit messages are plain English in the imperative, with no Jira keys:
  `Add Welcome Guests and the forecast panel, build the island scene`.
- A commit that fixes something starts with `Fix:`, for example `Fix: Warn in the Editor when the art is missing Git LFS files`.
- Keep PRs small: one ticket, one PR. Another teammate reviews before merge.
- Before opening a PR:
  - the project opens with no Console errors,
  - EditMode and PlayMode tests pass (Window → General → Test Runner → Run All),
  - you didn't commit changes you didn't mean to (Unity loves touching `ProjectSettings/`; check the diff).

## Working with Unity files

- **Scenes conflict badly.** Only one person edits `Game.unity` at a time. Say so in the team chat or on the
  Jira ticket. Build things as **prefabs** and put them in the scene. Two people can edit two prefabs at once.
- **Always commit the `.meta` file** together with its asset. Moving or renaming assets: do it inside Unity,
  never in Explorer/Finder, so the `.meta` file follows.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, IDE files or builds. `.gitignore` handles it,
  so don't force-add.
- Don't upgrade Unity or packages on your branch. Version bumps are a separate, agreed PR.
- Empty folders are kept with a `.gitkeep` file. Delete it once the folder has real content.

## Large files (Git LFS)

Images (`png`, `psd`, `aseprite`, …), audio, fonts, video and `fbx` are stored in Git LFS automatically
(see `.gitattributes`). Nothing to do by hand. Check with `git lfs ls-files`. Don't commit huge source files
nobody needs in the game (raw renders, video captures). Keep those in the shared drive.

Without Git LFS, git checks out small text "pointer" files instead of the images, and Unity shows no sprites
or icons. The Editor warns about it on start (**Resort Empire → Check Git LFS Files** runs the check again).
Fix it in the project folder with `git lfs install` and `git lfs pull`.

## Code conventions

- Everything in namespace `ResortEmpire.*` matching the assembly (`ResortEmpire.Core`, `ResortEmpire.Game`, …).
- Where code goes:
  - **Core:** game rules and numbers. Plain C#, no `MonoBehaviour`, no scene access. Unit-test it in EditMode.
  - **Game:** MonoBehaviours that connect Core to the scene.
  - **UI:** screens. UI reads state and sends player intents. It never changes game rules itself.
- Tuning numbers live in ScriptableObject configs under `Assets/_Project/Data`, never hard-coded.
  The Build doc marks them `TUNING` or `e.g.`, and the economist will change them.
- Use the Build doc's names in code: `Wave`, `Need`, `Worker`, `Bottleneck`, `WaveSatisfaction`,
  `ResortValue`, `AWAITING_WAVE`…
- **Never use the word "idle"**, anywhere (project rules §6). The game is not an idle game, and the resting
  state is `AwaitingWave`. A test fails the build if "idle" appears in `Scripts/`.
- Style: `.editorconfig` (4 spaces, LF, `_camelCase` private fields, PascalCase everything else).
- One `MonoBehaviour` per file, file named like the class.

## Asset naming

`<Category>_<Name>_<Variant>`, PascalCase, no spaces. For example:

| Asset | Example |
|---|---|
| Building sprite per level | `Bld_Restaurant_Lv1.png` |
| Guest sprite / animation | `Guest_Tourist_Walk.anim` |
| UI sprite | `UI_Button_Primary.png` |
| Icon | `Icon_Need_Food.png` |
| Sound | `SFX_Wave_Complete.wav`, `Music_Island_Calm.ogg` |
| Prefab | `Building_Restaurant.prefab`, `Screen_WaveComplete.prefab` |
| Config | `Config_WaveSystem.asset`, `Wave_03.asset` |

## Design questions

The Google Docs (GDD, Build doc, UI doc) are the source of truth for **mechanics**. The Figma is the source of
truth for **look**. If they disagree, don't pick one silently: add a row to
[docs/design-conflicts.md](docs/design-conflicts.md) and ask the designer.
