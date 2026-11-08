# OUR-39 Zmena stavu z preťaženia budovy na pozitívny stav

**When an overloaded building starts coping, the warning turns into a positive "catching up" signal.**
Builds on OUR-38.

## What the player sees

Right after the player helps (moves a Worker there, OUR-45), the red warning becomes a **green "Restaurant is
catching up"**. It appears in the **same moment** as the action, so the player sees the decision worked (US8).
The building is still busy and the queue is still there, so it must not look "done". Once the queue is small
again, the building goes back to calm.

## Rules (Build doc §4.4, AC2)

- **BOTTLENECK → RECOVERING** as soon as the building serves faster than guests currently arrive:
  `seats × speed > arrival_rate`. Here speed is `utilization ÷ service_time`, and arrival_rate is
  `demand ÷ wave_timer` while arrivals are open, 0 after. This reacts immediately to a Worker move. Waiting for the
  queue to shrink first would come seconds late.
- **RECOVERING → OK** when the queue is back at or below `bottleneck_threshold`.
- **RECOVERING → BOTTLENECK** if it's not catching up any more (the Worker left) and the queue grew over the last
  `bottleneck_hysteresis_s`.
- **OK / RECOVERING → SATISFIED** when arrivals are closed and nothing is waiting or seated.
- Why the state exists (doc): if the warning simply vanished, the screen would say "calm" while guests still queue,
  and the player might move the Worker away again too early.

## Where it goes

- Core: the state machine in `WaveSimulation` (next to OUR-38's). Call the state check right after a Worker move
  (OUR-45), not only on the next Tick.
- Island: `BuildingView` tints the ring **green** for Recovering.
- Toast: "Restaurant is catching up" on `Bottleneck → Recovering`.

## Done when

- Food bottlenecks on the empty Restaurant. A Worker is moved there, and the state is Recovering immediately.
- Later it becomes Ok, then Satisfied after arrivals close.

## Tests to write

- AC2: right after the move (no Tick), the state is Recovering.
- When even a staffed building can't keep up (demand above ~22 at level 1), it stays Bottleneck after a move.
- Recovering → Ok when the queue drops to ≤ 4.
