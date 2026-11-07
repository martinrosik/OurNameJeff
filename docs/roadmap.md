# Roadmap

How the project gets from an empty Unity project to the MVP. Each bullet is roughly one Jira ticket.
References like §4.4 point to the [Build doc](https://docs.google.com/document/d/10X2j8p_AwRmr4103MdLnX0faVNob0-8Sy5otXSyd6QA/edit).

The prototype answers one question: **is the wave loop fun?** Everything that doesn't test the wave loop waits
for the MVP.

## Phase 0: Foundation ✅

- Unity 6000.6.5f1, URP 2D, Input System, landscape, bundle ID `com.ournamejeff.resortempire`
- `.gitignore`, `.gitattributes` (LFS + UnityYAMLMerge), `.editorconfig`
- `Assets/_Project` layout, assemblies (Core / Game / UI / Editor / Tests), Game scene, smoke tests
- README, CONTRIBUTING, CLAUDE.md, design conflict log

## Phase 1: Wave simulation core (`ResortEmpire.Core`, no visuals) ✅

Built from the Build doc. It reproduces the doc's "no intervention" table exactly. How it works, how to tune it,
and the open assumptions: [simulation.md](simulation.md). Still worth getting the companion files
(`wave_sim.py`, Energy System Spec, Decision Log, User Story Walkthrough, Project rules) to confirm those assumptions.

- **Config data**: ScriptableObjects for the §4.8 parameter table (`wave_timer`, `service_time`,
  `utilization_empty`, `patience_grace`, floors, rating thresholds, `coins_per_unit`, multipliers, …), needs,
  buildings (need, 3 levels of seats, unlock gate + cost), the 8-wave table and the Sudden rush event.
- **Needs model**: steady arrivals (`demand_total ÷ wave_timer`), seats per building, service as progress whose
  speed changes immediately when a Worker moves or a boost starts/ends (§4.4).
- **Satisfaction**: per-unit wait (arrival → seated), linear falloff, `unit_floor` and `wave_floor`, weighted
  average with the Ocean View weight, rating tiers (§4.4).
- **Need states**: OK / BOTTLENECK / RECOVERING / SATISFIED / LOCKED, using queue length (not seated guests),
  threshold and hysteresis.
- **Workers**: always one fewer than buildings, 1 slot per building, a free instant move = "pick which building to
  leave empty". Placement is remembered between waves (§4.5).
- **Game state machine**: `AWAITING_WAVE → WAVE_INCOMING → WAVE_ACTIVE ⇄ WAVE_PAUSED → WAVE_RESOLVING →
  WAVE_COMPLETE → UPGRADE`. The timer closes arrivals, never the wave (§4.3).
- **Economy**: Coins (`units × coins_per_unit × rating multiplier`), Energy (drop by rating, Speed Up ⚡3 halves
  service time for 15 s, once per building per wave), Resort Value (only grows).
- **Upgrades and unlocks**: seats 3 → 5 → 7 between waves only. Pool after Wave 3, Bungalows after Wave 6:
  gate + Coins, each adds its need and a Worker. An unbought unlock's need never spawns (WR9).
- **Tests**: EditMode tests for WR1–WR9 and AC1–AC8, plus a headless C# port of `wave_sim.py` that prints the
  "no intervention" table and flags WR7/WR8 violations.

## Phase 2: Greybox playable prototype (placeholder art)

- Island scene with placeholder building sprites showing seats, a queue, and whether a Worker is there
- Guests as simple sprites that walk to buildings, queue and sit, with the wave arrival boat (US3)
- S1 Home: resort status, currencies, Resort Value, next-wave forecast, Start Wave, tap a building / construction site
- S2 Wave Needs Panel (side panel, island stays visible), bottleneck warning + guest toast, RECOVERING state
- S3 Worker Allocation (bottom sheet), entered from S2 or from S1 before the wave
- S4 Wave Complete: rating, satisfaction %, rewards, What went well / Needs improvement, unhappy guests leaving
- S5 Upgrade Suggestion tied to the longest bottleneck
- S9 Area Unlock (one template, two triggers)
- Sudden rush mini-event in wave 2 (Move Worker here / Use Energy / Ignore)
- Pause on background + save/load of the full wave state (JSON in `persistentDataPath`), resume exactly (AC5)
- Debug panel: jump to wave N, add Coins/Energy, time scale, reset save

## Phase 3: Figma UI and art pass

Only for screens whose rows in [design-conflicts.md](design-conflicts.md) are decided.

- UI kit: fonts (TextMeshPro assets), colors, outlined pill buttons, panels, icon set
- Final island and building sprites: 5 buildings × 3 levels, seat count readable from a distance
- Guest sprites and animations
- Juice: arrival, Worker move feedback, rating reveal, unlock celebration (respect reduced-motion settings)
- Sound: calm music, UI and feedback SFX

## Phase 4: Playtest build

- Android build profile, signing, an install path for testers
- Local metrics log for §4.12: `wave_complete_rate`, `worker_moves_pre_wave` / `worker_moves_in_wave`,
  `worker_move_before_prompt_rate`, `drain_duration_s`, `rating_distribution`, `upgrade_take_rate`, `session_length`
- Playtest script based on the exit criteria (§4.12)

## Phase 5: MVP (after the prototype passes playtests)

VIP layer (S6 Preview, S7 Request Board, S8 VIP Complete), Reputation as the unlock gate, Materials and Gems,
Managers and Specialists hired for Coins, more Worker slots, the Spa as a VIP building, building levels 4–5,
Energy Mode B, monetization hooks (rewarded video, first IAP places). See Build doc §5.
