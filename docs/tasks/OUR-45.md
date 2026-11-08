# OUR-45 Možnosť presúvania pracovníkov pred aj počas vlny

**Move Workers between buildings, before and during a wave.** Works on its own; it matters most with OUR-47
(unstaffed buildings are slow).

## What the player sees (UI doc S3 Worker Allocation)

The player **taps a building** (or later a need row on the side panel, OUR-37) and a **bottom sheet** opens:
"WORKER ALLOCATION · RESTAURANT".

- If the building has **no Worker**: "Take from Reception" / "Take from Beach Bar" buttons. Each says what that
  building is doing and that **it will be left without a Worker**. The cost must be visible, not just the gain
  (AC2b).
- If the building **has its Worker**: offer "Move to <the empty building>". If its queue still grows, say it's
  **too small** and should be upgraded after the wave. Telling "no Worker" apart from "too small" is the main job
  of this screen (§4.1, AC2d).

After a move, the island updates at once: the "W" badge moves, "No Worker" moves to the other building (OUR-48).

## Rules (Build doc §4.5, WR5, AC2, AC2b, AC4d)

- Workers are **anonymous and interchangeable**: no names, levels or star ratings. (The Figma shows named chefs;
  that's an open conflict, see design-conflicts.md row 6. Follow the doc.)
- **1 Worker slot per building**, and there's always **one fewer Worker than buildings**. So a move is always
  "pick which building to leave empty".
- **Moving is free and instant** (WR5): no Coins, no Energy, no confirm dialog.
- Allowed in AWAITING_WAVE (prepare for the forecast), WAVE_INCOMING and WAVE_ACTIVE, including the drain after
  90 s. Not on the result screen.
- **Placement is remembered** between waves.
- The speed change applies **immediately**, also to guests already seated (OUR-46's progress model).

## Where it goes

- Core: `ResortGame.MoveWorker(BuildingId from, BuildingId to)` returning a result enum (`Moved`, `NotAllowedNow`,
  `SameBuilding`, `NoWorkerAtSource`, `TargetFull`, `BuildingNotOnIsland`), plus an event `WorkerMoved(from, to)`.
  `BuildingRuntime.HasWorker` already has an internal setter. After a move during a wave, re-check need states
  (OUR-39).
- Island taps:
  - `BuildingView` implements `IPointerClickHandler` and needs a `BoxCollider2D` (about 2.4 × 2.9 units).
  - The Main Camera needs a `Physics2DRaycaster`. The scene builder currently *removes* it, so add it back in
    `GreyboxSceneBuilder`, then rebuild the scene.
  - `IslandView` raises `BuildingTapped(BuildingId)`.
  - The Game assembly then needs a reference to `UnityEngine.UI` (EventSystems).
- UI: new `Scripts/UI/Greybox/WorkerAllocationView.cs`, a bottom sheet with a dimmed backdrop, built with `UIFactory`.

## Done when

- Tapping the Restaurant before the wave offers "Take from Reception" and "Take from Beach Bar".
- Choosing one gives the Restaurant the "W" badge, and the source building shows "No Worker".
- The same works during the wave, and the placement stays for the next wave.

## Tests to write

- EditMode:
  - every refusal case (target full, no Worker at source, same building)
  - WorkerCount stays the same after a move
  - AC2b: the source building has no Worker right after
- PlayMode: tap a building, sheet open, option clicked, badge moved.
