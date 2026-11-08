# OUR-38 Zobrazenie preťaženia jednotlivých budov pri veľkom dopyte

**Show which building is overloaded when demand is high.** Builds on OUR-34 (queues) and OUR-46/47 (service speed).

## What the player sees

When guests pile up at a building, that building gets a clear warning:
- on the island: a **red ring/glow** around the building and its **queue of waiting guests** under it;
- a short message: **"! Restaurant queue growing"**;
- a guest quote such as **"Food takes a bit too long."**;
- on the side panel (OUR-37): the need's row turns red, with "Queue growing!".

This is how the player spots the problem and decides what to do. **Never show a live satisfaction number or a
countdown** (decided 2026-09-29): the queue and the warning are the signal.

## Rules (Build doc §4.4 "Stav jedného needu", AC1)

- Only the **queue** counts: units waiting, not seated. Full seats are healthy.
- **OK → BOTTLENECK** when `queue > bottleneck_threshold` **and** the queue has grown over the last
  `bottleneck_hysteresis_s` seconds (compare the queue now with the queue 3 s ago). The second condition stops
  the warning from flickering.
- **`bottleneck_threshold` = 4** units (TUNING 1–10); **`bottleneck_hysteresis_s` = 3 s** (TUNING 1–10).
- Need states (shared with OUR-39): `Locked` (building not on the island), `Ok`, `Bottleneck`, `Recovering`
  (OUR-39), `Satisfied` (arrivals closed and nothing waiting or seated).
- Re-check states at every event and at least every 0.25 s of wave time, so the result doesn't depend on the
  frame rate.

## Where it goes

- Core: `Scripts/Core/Model/NeedState.cs` (enum), state per need in `WaveSimulation`, a small queue history to
  answer "queue 3 s ago", and an event `NeedStateChanged(need, from, to)` that `ResortGame` forwards.
  Config: `WaveSettings.bottleneckThreshold` and `bottleneckHysteresis`.
- Island: `Scripts/Game/Greybox/BuildingView.cs`. Add a rounded `SpriteRenderer` behind the building (sorting
  order 3) tinted red for Bottleneck, and queue dots under the plate (local y ≈ −1.1) if OUR-34 didn't add them.
  `GreyboxSceneBuilder` creates the objects. Re-run **Resort Empire → Greybox → Rebuild Greybox Scene**.
- Message and quote: a small toast view under `Scripts/UI/Greybox/` (fading text over the island), triggered by
  `NeedStateChanged`.

## Done when

- Food 20 on the empty Restaurant turns Bottleneck during the wave, and only once its queue is above 4.
- Rooms 4 on the staffed Reception never turns Bottleneck.
- The building turns red and the message appears.

## Tests to write

- AC1: the case above. Every entry into Bottleneck happens with waiting > threshold.
- No flicker: a queue hovering at the threshold doesn't toggle the state every frame.
- PlayMode: the red ring is visible on the Restaurant during a heavy wave.
