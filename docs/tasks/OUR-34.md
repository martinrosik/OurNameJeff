# OUR-34 Generovanie návštevníkov počas 90 s od prijatia vlny

**Guests arrive during the 90 seconds after the wave starts.** First ticket of the wave chain; everything else
builds on it.

## What the player sees

After **Welcome Guests**, the boat docks (already built) and guests start arriving. Over 90 seconds every
building receives the guests the forecast promised (Rooms 8, Drinks 8, Food 6 in wave 1). Guests who arrive
when all seats are full wait in front of the building. Showing the queue as dots under the building is a good
first visual; OUR-38 colours it later.

## Rules (Build doc §4.4, §4.8)

- **Demand** per need per wave comes from the wave table in the config (`WaveDefinition.rooms/drinks/food/…`).
  These are *need units*, not guests. A guest carries about 2 needs; the "guests" count is only visual.
- **Steady arrivals:** `arrival_rate = demand_total ÷ wave_timer`, constant for the whole arrival phase.
  The arrived count is `floor(rate × t)`, so **unit k (0-based) arrives at (k + 1) × wave_timer ÷ demand**.
  The last unit arrives exactly at 90 s.
- **`wave_timer` = 90 s** (TUNING, range 45–180). It closes the **arrivals**, never the wave (WR3, that's OUR-41/35).
- **WR9:** a need whose building isn't on the island never spawns, even if the table lists demand (Relax before the
  Pool exists). `ResortGame.GetForecast()` already filters this way. Do the same.
- There's no spawning before WAVE_ACTIVE: arrivals start at wave time 0, after the boat (`ResortGame.WaveTime`).

## Where it goes

- `Scripts/Core/Config/WaveSettings.cs`: add `[Min(1f)] public float waveTimer = 90f;` with a tooltip. Also set it in
  `Scripts/Editor/PrototypeConfigDefaults.cs`, and in `Config_Prototype.asset` (select it via
  **Resort Empire → Config → Select Prototype Config**, or delete and recreate it).
- New `Scripts/Core/Simulation/WaveSimulation.cs` (plain C#): created by `ResortGame.StartWave()` (or when the
  phase becomes WaveActive) and advanced from the `// OUR-34` hook in `ResortGame.Tick()`. Suggested shape:
  - per need: the arrival schedule (list of times), arrived count, a FIFO **queue** of arrival times (arrival times
    are needed later for waiting time and satisfaction), and the total demand
  - `NeedStatus GetNeedStatus(NeedId)` returning Served / Total / Waiting / Seated (OUR-37, OUR-38 read it)
  - `bool ArrivalsClosed` (true once t ≥ waveTimer and every unit has arrived) for OUR-41
- `ResortGame`: expose the current wave (`CurrentWave`) and `GetNeedStatus(NeedId)`.
- Optional visual: in `Scripts/Game/Greybox/BuildingView.cs`, show `Waiting` as small dots under the plate
  (around local y −1.1, 2 rows of 6, then "+N").

## Done when

- Starting wave 1 makes 8 Rooms, 8 Drinks and 6 Food units arrive, spread evenly over 90 s.
- After 45 s exactly `floor(45 × 8 / 90) = 4` Rooms units have arrived.
- After 90 s every unit has arrived and none come later.
- Needs without a building never spawn.
- The result doesn't depend on the frame rate.

## Tests to write (EditMode, `Tests/EditMode`)

- Arrival times for demand 8 over 90 s: 11.25, 22.5, …, 90.
- Count after `Tick(45)` = 4; after `Tick(90)` = 8; after `Tick(200)` still 8.
- Relax demand in the table but no Pool building → 0 Relax units.
- Same wave with `Tick(1/60)` steps vs `Tick(0.7)` steps → identical counts at the same wave time.

## Pitfalls

- Don't spawn "per frame": compute when the next unit is due and process arrivals in time order inside `Tick`,
  even when one Tick spans several arrivals.
- Keep the arrival **time** of each waiting unit, not just a count. OUR-38 and later satisfaction need it.
