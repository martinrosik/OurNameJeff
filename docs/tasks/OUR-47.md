# OUR-47 Budova bez pracovníka beží v spomalenom režime

**A building without a Worker still serves, but slower.** Builds on OUR-46.

## What the player sees

The building marked "No Worker" (OUR-48) serves at about a third of the speed. Its seats stay full longer and its
queue grows sooner. It must read as "slow", never as "broken" (UI doc S3).

## Rules (Build doc §4.5, §4.8, AC2c, WR2)

- **`utilization_empty` = 0.33** (TUNING, range 0.1–0.6). Service time becomes `12 ÷ 0.33 ≈ 36 s` per guest.
- Throughput at level 1 without a Worker: `3 × 0.33 ÷ 12 ≈ 0.08 guests/s`, about **7 guests per 90 s**.
  Demand below ~7 can be handled by an empty building, ~7–22 needs a Worker, and above ~22 needs an upgrade.
- **WR2: `utilization_empty` must stay above 0.** An empty building never stops, otherwise the wave might never end.
  Add this check to `GameConfigValidator`.
- **AC2c:** an empty building with up to ~15 units in a wave still serves all of them.
- The doc calls `utilization_empty` and `service_time` the two most sensitive numbers of the prototype: tune them
  together.

## Where it goes

- `WaveSettings`: `[Min(0.01f)] public float utilizationEmpty = 0.33f;` plus defaults and the asset.
- `WaveSimulation`: speed = `(HasWorker ? utilizationStaffed : utilizationEmpty) ÷ serviceTime`.
- `GameConfigValidator`: error "WR2: utilizationEmpty must be above 0".

## Done when

- At the Restaurant (no Worker at the start), a guest takes about 36 s to serve.
- 14 Food units on the empty Restaurant all get served (the wave can end once OUR-41/35 exist).
- The validator rejects `utilizationEmpty = 0`.

## Tests to write

- An unstaffed building serves one unit in `serviceTime ÷ utilizationEmpty` seconds.
- The same building with a Worker serves it 3× faster.
- Validator: `utilizationEmpty = 0` → WR2 error.

## Open question

The Jira ticket says "(1 s)", but the doc's model gives about 36 s per guest without a Worker. Keep it in config,
use the doc numbers, and see [design-conflicts.md](../design-conflicts.md) row 10.
