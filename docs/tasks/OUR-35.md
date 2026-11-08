# OUR-35 Ukončenie vlny do 2 minút

**The wave ends when everyone is served, which takes about 2 minutes for a well-handled wave.** Builds on OUR-41.

## Important: what "within 2 minutes" means

The Build doc has **no hard 2-minute cutoff**. It says (§4.4): *a well-handled wave takes e.g. ~2 minutes (90 s
arrivals + finishing the last services); a badly handled one takes longer*, and the wave closes only when all
queues are empty (WR3, "nobody leaves", WR4). A hard cutoff would send waiting guests home unserved, which is the
"you lost" feeling the game avoids.

**Recommended:** end the wave the moment everything is served, and make sure wave 1 handled normally ends within
about 2 minutes (in the doc's model it's about 126 s). If the team wants a hard limit instead, that changes WR3/WR4,
so the designer has to decide. See [design-conflicts.md](../design-conflicts.md) row 11.

## Rules (Build doc §4.3)

- State machine: `WAVE_ACTIVE → WAVE_RESOLVING` (all needs served after arrivals closed) `→ WAVE_COMPLETE`.
- `WAVE_RESOLVING` is short: the result is computed, then it moves on.
- `CompletedWaves` goes up by one when the wave completes, so the forecast (OUR-33) shows the next wave.

## Where it goes

- The `// OUR-35` hook in `ResortGame.Tick()`: when `CurrentWave.IsFinished` (OUR-41), go to
  `GamePhase.WaveResolving`, build a small result (wave number, units served, duration in seconds), go to
  `GamePhase.WaveComplete` and raise an event (e.g. `WaveCompleted`).
- OUR-40 shows the result and brings the player back to `AwaitingWave`.

## Done when

- Wave 1 with the starting Workers ends by itself, in ≤ 130 s of wave time.
- The phase becomes WaveComplete exactly when the last guest is served, not on a timer.
- The forecast then shows wave 2.

## Tests to write

- Play wave 1 with `Tick` steps until WaveComplete: duration ≤ 130 s, every unit served.
- PhaseChanged order: WaveIncoming, WaveActive, WaveResolving, WaveComplete.
- A badly handled wave (e.g. Food 30 on the empty Restaurant) runs longer than 2 minutes and still completes.
