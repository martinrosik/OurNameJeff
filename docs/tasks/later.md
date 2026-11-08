# Later sprints (not in OUR Sprint 2)

Features the Build doc and UI doc define for the prototype that aren't in this sprint. The key rules are listed
so whoever picks them up starts from the right numbers. All numbers are TUNING and go in the config asset.

## Wave Satisfaction and stars (Build doc §4.4)

- Satisfaction is collected quietly during the wave and **only shown after it** (never live).
- Per need unit: waiting ≤ `patience_grace` 10 s → 100 %. It then falls linearly to `unit_floor` 25 % at 30 s, and
  stays at 25 % after that.
- Wave Satisfaction = max(`wave_floor` 40 %, weighted average). Ocean View weighs 1.5 (others 1.0).
- Stars: 1★ 40 % · 2★ 55 % · 3★ 70 % · 4★ 80 % · 5★ 90 % · Perfect 100 %. The worst result is 1★, never zero.
- Hard rule WR1: `unit_floor < wave_floor < 3★ threshold`.

## Coins, Energy, Resort Value (Build doc §4.6, §4.8)

- Coins = units served × `coins_per_unit` 16 × multiplier (1★ 0.4 · 2★ 0.6 · 3★ 0.8 · 4★ 1.0 · 5★ 1.2 · Perfect 1.3).
  Paid on "Claim Rewards".
- Energy is an **optional boost, never a gate** (HR1/HR2):
  - **Speed Up** costs ⚡3 and halves a building's service time for 15 s, once per building per wave
  - earned by rating drop [0, 1, 2, 3, 5] and from a voluntary rewarded video
  - no regeneration over time
- Resort Value only ever grows (status/prestige). Its amounts aren't defined in the doc yet.
- They go in the top bar of S1 (room is left on the right).

## Sudden rush (Build doc §4.6)

Wave 2 only, at 45 s: +6 units over 10 s at the building that has **no Worker** at that moment, so moving a Worker
always solves it. A prompt inside S2 offers "Move Worker here / Use Energy / Ignore". The wave doesn't pause, and
the forecast doesn't show it.

## Upgrades (S5) and unlocks (S9) (Build doc §4.8)

- **Upgrades**: seats 3 → 5 → 7 (the level data already exists in the config), costing 500 / 1,200 Coins.
  **Only between waves** (WR6). S5 suggests the building that was overloaded longest (AC6).
- **Unlocks**: Pool (Relax) after wave 3 for 1,200 Coins; Bungalows (Ocean View) after wave 6 for 2,500 Coins.
  - Each comes **with one Worker**, placed on the new building.
  - The new need debuts at about half volume (the wave table already does this).
  - A need whose building isn't bought never spawns (WR9).
  - Reserved island spots exist in `GreyboxSceneBuilder.Offset()`.

## Pause and save (Build doc §4.3, AC5)

`WAVE_PAUSED` when the app goes to the background: every timer frozen, nothing progresses or decays while away.
Save the full wave state, so the player resumes exactly where they left off, even after the app is killed.

## Balance check (Build doc §4.8, WR7/WR8)

The economist's `wave_sim.py` simulates a passive player: one Worker placement, no moves, no upgrades, no Energy.
- **WR7:** from wave 3 (except debut waves 4 and 7), the passive player must **not** reach 5★.
- **WR8:** a fully upgraded resort must reach 5★ in every wave without Energy.
- Expected passive results with the doc numbers: Perfect, 4★, 4★, Perfect, 4★, 4★, 5★, 3★. Passive Coins come to
  about 1,900 for waves 1–3 and about 3,900 for waves 4–6.
- Worth a C# port with an Editor menu once satisfaction exists.

## Full result screen (S4)

OUR-40 shows the basic "wave complete" moment. The full S4 adds stars, Wave Satisfaction %, rewards, "What went
well / Needs improvement", and a few unhappy guests leaving (the number depends on the rating; it has no effect on
the next wave).
