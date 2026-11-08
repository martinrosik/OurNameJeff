# Game core (ResortEmpire.Core)

The game rules as plain C# in `Assets/_Project/Scripts/Core`: no scenes, no MonoBehaviours, so the same code runs in
the game and in EditMode tests. It follows the Build doc (§4.3–§4.10).

## What exists now

| Type | What it is |
|---|---|
| `GameConfig` | ScriptableObject with every tunable number: `Assets/_Project/Data/Config_Prototype.asset` |
| `ResortGame` | The game: phase state machine, buildings, Workers, forecast, wave start |
| `BuildingRuntime` | One building's live state: level, Worker, seats (capacity) |
| `WaveForecast` | Demand of a wave for the needs on the island (S1 forecast) |
| `GamePhase` | The Build doc's full state machine (only the start of it is implemented) |

```csharp
var game = new ResortGame(config);   // config = the GameConfig asset

game.Tick(Time.deltaTime);           // every frame (GameController does this)
game.StartWave();                    // OUR-27: AWAITING_WAVE → WAVE_INCOMING (boat) → WAVE_ACTIVE

game.Phase, game.WaveTime, game.IncomingRemaining
game.GetNextWaveForecast()           // OUR-33
game.GetBuilding(BuildingId.Restaurant).HasWorker   // OUR-48
game.GetBuilding(BuildingId.Restaurant).Seats       // OUR-50 capacity (3 at level 1)
game.GetSeatedCount(BuildingId.Restaurant)          // OUR-50 occupancy: 0 until guests exist
game.PhaseChanged += (from, to) => { };
```

The wave stays in WAVE_ACTIVE for now. `ResortGame.Tick()` has one `// OUR-xx` comment per sprint ticket that
continues it.

## Tuning

Select the config with **Resort Empire → Config → Select Prototype Config** and edit it in the Inspector (every
field has a tooltip). **Resort Empire → Config → Validate Prototype Config** checks it. `PrototypeConfigDefaults`
(Editor) holds the Build doc values used to create the asset and in tests. When a ticket adds a setting, add it in
three places: the settings class (with the doc default), `PrototypeConfigDefaults`, and the asset.

## The planned model (Build doc §4.4), built by the sprint tickets

- **Arrivals (OUR-34):** each need's units arrive evenly over `waveTimer` (90 s). Unit k arrives at
  (k + 1) × 90 ÷ demand. Needs without a building don't spawn.
- **Seats and service (OUR-46, OUR-47):** a building serves `seats` units at once. Service is progress at speed
  `utilization ÷ serviceTime` (12 s; utilization 1.0 with a Worker, 0.33 without). A speed change applies at once to
  guests already seated. Waiting = arrival → seated.
- **Drain and end (OUR-41, OUR-35, OUR-40):** the 90 s timer closes arrivals, never the wave. The wave ends when
  every queue and seat is empty. Nobody leaves.
- **Need states (OUR-38, OUR-39):**
  - `Ok → Bottleneck` when the queue is above 4 and grew over the last 3 s.
  - `Bottleneck → Recovering` as soon as the building serves faster than guests arrive.
  - `Recovering → Ok` when the queue is back at or below 4.
  - `→ Satisfied` when arrivals are closed and the need is empty.
- **Workers (OUR-45):** one slot per building, one fewer Worker than buildings, free and instant moves before and
  during the wave.
- **Event-driven steps:** process arrivals and service ends in time order inside `Tick`, so the frame rate never
  changes results.

Later sprints add satisfaction and stars, Coins/Energy/Resort Value, Speed Up, the Sudden rush, upgrades,
unlocks, pause/save and the balance check. See [tasks/later.md](tasks/later.md).
