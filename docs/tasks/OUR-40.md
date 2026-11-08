# OUR-40 Informovanie hráča o ukončení vlny

**Tell the player the wave is over, then bring them back to the island for the next one.** Builds on OUR-35.

## What the player sees (UI doc S4 Wave Complete)

When the last guest is served, a **Wave Complete** moment appears: a full-screen or large overlay, because
closing the wave deserves full attention. It shows:
- "WAVE 1 COMPLETE"
- **Guests served 12/12** (everyone is always served)
- how long the wave took (optional)
- one main button, **Continue** (later "Claim Rewards"), which returns to the island. The status goes back to
  "READY FOR GUESTS" and the forecast shows wave 2.

Tone: calm and positive. Never "you lost". Stars, Wave Satisfaction %, rewards and "What went well / Needs
improvement" are **later-sprint** features ([later.md](later.md)), so leave room for them. The UI doc says S4 is a
*template* that the VIP result screen (S8) reuses later.

## Rules

- Appears on `GamePhase.WaveComplete` (OUR-35). Not before, and never as a pop-up during the wave.
- Continue: `WaveComplete → AwaitingWave`. The Build doc puts `UPGRADE` in between; until upgrades exist
  (later sprint) go straight to `AwaitingWave`.
- One main CTA per screen (UI doc §2).

## Where it goes

- Core: `ResortGame.ContinueAfterWave()` (only valid in WaveComplete), plus the result data from OUR-35.
- UI: a new view class in `Scripts/UI/Greybox/`, e.g. `WaveCompleteView.cs`, built with `UIFactory`
  (`Panel`, `Text`, `Button`), the way `HudView` is built. Create it in `GreyboxUI.Build()` and refresh it in
  `GreyboxUI.LateUpdate()`. Use only plain characters (no ★, the font lacks it).

## Done when

- After wave 1 the overlay appears with "Guests served 12/12".
- Continue returns to the island with the forecast for wave 2 and Welcome Guests visible.

## Tests to write (PlayMode, `Tests/PlayMode`)

- Load the Game scene, press Welcome Guests, `Tick` until WaveComplete, overlay visible.
- Press Continue: phase is AwaitingWave and the forecast says wave 2.
