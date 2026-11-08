# OUR-41 Doobsluhovanie posledných návštevníkov po ukončení vlny

**After the 90 s arrival phase, buildings keep serving the guests who are still waiting.** Builds on OUR-34 and OUR-46.

## What the player sees

After 90 s no new guests come. The panel (OUR-37) says **"All guests arrived"**. Queues keep shrinking until
everyone is served. The player can still move Workers during this time (OUR-45).

## Rules (Build doc §4.3, §4.4, WR3, WR4, AC3, AC4)

- **WR3:** `wave_timer` closes the **arrivals**, never the wave. The wave continues until every queue and seat is
  empty ("dobeh", the drain).
- **WR4:** **nobody leaves during the wave.** Every unit that arrived is served, however long it waits. Being
  unhappy shows only after the wave (later sprint).
- **AC4:** when the timer hits 90 s with guests still queuing, no new demand arrives, the wave continues, and the
  player can still move Workers (and later use Energy).
- **AC3:** while at least one unit is waiting or seated, the wave runs.
- The wave always ends eventually, because an empty building is never at zero speed (WR2, OUR-47).

## Where it goes

- `WaveSimulation`: `ArrivalsClosed` becomes true when wave time ≥ `waveTimer` and every scheduled unit has
  arrived. Keep serving after that. Add **`IsFinished`** = arrivals closed AND every queue and seat empty.
  OUR-35 uses it.
- The `// OUR-41` hook in `ResortGame.Tick()`.

## Done when

- With Food 20 on the empty Restaurant, at 90.5 s the wave is still running, arrivals are closed, Food still has
  guests waiting, and Food's total is still 20.
- A few minutes later every unit is served and `IsFinished` is true.

## Tests to write

- The case above (AC3/AC4).
- `IsFinished` is false while anything is seated, and true only after the last service ends.
- A wave with demand 0 for every need: arrivals close at 90 s, then it finishes.
