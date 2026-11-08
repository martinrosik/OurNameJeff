# OUR-46 Budova s prítomným pracovníkom beží naplno

**A building with a Worker serves at full speed.** Builds on OUR-34 (guests arrive and queue).

## What the player sees

Guests sit down on the building's seats (the seat squares from OUR-50 fill up, already wired) and leave
when served. A building with a Worker ("W" badge, OUR-48) gets through its guests at full speed.

## Rules (Build doc §4.4 "miesta pri pulte", §4.8)

- A building has **`capacity` seats** = `BuildingRuntime.Seats` (3 at level 1). A unit that arrives takes a free
  seat; if none is free, it waits in the queue (FIFO).
- **Waiting = arrival → seated.** Time being served does not count as waiting.
- **Service is progress, not a fixed timer:** each seated unit's progress goes from 0 to 1 at
  `utilization ÷ service_time` per second. When the speed changes (a Worker arrives or leaves, OUR-45),
  it changes at once for everyone already seated.
- **`service_time` = 12 s** (TUNING, range 4–24) and **`utilization_staffed` = 1.0**, so one guest takes 12 s per seat.
- Throughput at level 1 with a Worker: `3 × 1.0 ÷ 12 = 0.25 guests/s`, about **22 guests per 90 s**.
- When a unit finishes, its seat frees and the next waiting unit sits down at that exact moment.

## Where it goes

- `WaveSettings`: `serviceTime = 12f` (`[Min(0.1f)]`) and `utilizationStaffed = 1f`. Also add them in
  `PrototypeConfigDefaults` and the asset.
- `WaveSimulation` (from OUR-34): per need, a list of seated units with progress. Per step, advance progress, finish
  units at ≥ 1, then seat units from the queue.
- **`ResortGame.GetSeatedCount(BuildingId)`**: return the number of seated units. OUR-50's seat squares then fill
  by themselves.
- Served count per need, for OUR-37's "Food 2/6".

## Done when

- With a Worker, a lone guest is served 12 s after sitting down.
- 3 guests are served at the same time; the 4th waits until a seat frees.
- Seat squares on the island fill and empty during the wave.

## Tests to write

- One unit arrives at a staffed building: seated immediately (waiting 0), served 12 s later.
- 4 units at once on 3 seats: the 4th sits at 12 s and is served at 24 s.
- `GetSeatedCount` matches the seated units.
- Frame-rate independence (same results with small and big Tick steps).

## Open question

The Jira ticket says "(0.33 s)", but the Build doc says 12 s per guest per seat (the config now uses 12 s) with utilization 1.0 (and 0.33
without a Worker). Keep both as config values (`serviceTime`, `utilization*`), use the Build doc numbers, and see
[design-conflicts.md](../design-conflicts.md) row 10. The designer decides.

## Pitfalls

- Process completions in time order inside a Tick. Compute when the next seat frees
  (`(1 − progress) ÷ speed`) instead of checking per frame.
