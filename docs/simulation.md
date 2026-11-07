# Wave simulation (ResortEmpire.Core)

The prototype's game rules as plain C# in `Assets/_Project/Scripts/Core`. No scenes, no MonoBehaviours, so the
same code runs in the game, in EditMode tests and in the balance tool. It implements Build doc §4.3–§4.10.

## Pieces

| Type | What it is |
|---|---|
| `GameConfig` | ScriptableObject with every tunable number: `Assets/_Project/Data/Config_Prototype.asset` |
| `ResortGame` | The whole game: phase state machine, Workers, Energy, Coins, Resort Value, upgrades, unlocks |
| `WaveSimulation` | One running wave: arrivals, seats, queues, waiting, need states, Sudden rush |
| `WaveResult` | Rating, Wave Satisfaction, rewards, per-need breakdown, recommended upgrade (S4/S5) |
| `WaveBalanceSimulator` | Headless balance check, the C# version of `wave_sim.py` (WR7/WR8) |

## Using it from the Game layer (Phase 2)

```csharp
var game = new ResortGame(config);         // config = the GameConfig asset

// every frame
game.Tick(Time.deltaTime);

// app lifecycle (OnApplicationPause)
game.Pause();  game.Resume();

// player intents
game.StartWave();                                         // S1 Start Wave
game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);  // S3, free and instant
game.BoostBuilding(BuildingId.BeachBar);                  // Speed Up ⚡3, during the wave only
game.ClaimRewards();                                      // S4
game.UpgradeBuilding(BuildingId.Restaurant);              // S5, between waves only
game.UnlockBuilding(BuildingId.Pool);                     // S9
game.FinishUpgrades();                                    // back to AWAITING_WAVE

// reading state for the UI
game.Phase, game.Coins, game.Energy, game.ResortValue
game.GetNeedStatus(NeedId.Food)        // "Food 2/6", state, waiting, seated
game.GetServiceSpeed(BuildingId.BeachBar)  // 0.33 empty, 1.0 staffed, ×2 boosted
game.GetNextWaveForecast()             // S1 forecast
game.GetUnlockStatus(BuildingId.Pool)  // S9 states
game.LastWaveResult                    // S4/S5

// events for feedback: PhaseChanged, NeedStateChanged, WorkerMoved, BoostStarted/Ended,
// RushStarted, WaveCompleted, BuildingUpgraded, BuildingUnlocked, CurrenciesChanged
```

Every action returns a result enum (`Moved`, `NotAllowedNow`, `NotEnoughEnergy`, …) instead of throwing, so the
UI can react (for example, `NotEnoughEnergy` → offer the rewarded video).

## Tuning

1. Select the config: menu **Resort Empire → Config → Select Prototype Config** and edit it in the Inspector.
   Every field has a tooltip with the Build doc name and range.
2. Check it: **Resort Empire → Balance → Wave Balance Report** prints the passive-player table and flags WR7/WR8
   violations. **Resort Empire → Config → Validate Prototype Config** checks WR1, WR2 and data errors.
3. Run the tests. `PrototypeBalanceTests` fails the build if the asset breaks WR1/WR2/WR7/WR8 or the debut-wave rules.

With the Build doc numbers the report reproduces the doc's "no intervention" table exactly
(Perfect, 4★, 4★, Perfect, 4★, 4★, 5★, 3★; passive Coins 1,930 for waves 1–3 and 3,901 for waves 4–6).
A test keeps the model honest against that table.

## How the model works (short)

- **Arrivals**: each need's units arrive evenly over `waveTimer`. Unit k arrives at (k+1) × waveTimer ÷ demand.
- **Seats**: a building serves `seats` units at once. Service is progress at speed
  `utilization × boost ÷ serviceTime`, so moving a Worker or a boost changes the speed of guests already seated.
- **Waiting** is arrival → seated. Contribution: 100 % up to `patienceGrace`, linear down to `unitFloor` at
  `patienceFloorAfter`. Wave Satisfaction is the weighted average, never below `waveFloor`.
- **End**: `waveTimer` closes arrivals. The wave ends when every queue and seat is empty. Nobody ever leaves.
- **Need states**:
  - `Ok → Bottleneck` when the queue is above `bottleneckThreshold` and has grown over the last `bottleneckHysteresis` seconds.
  - `Bottleneck → Recovering` as soon as the building serves faster than guests currently arrive.
    That's immediate after a Worker move or a boost (AC2).
  - `Recovering → Ok` when the queue is back at or below the threshold.
  - `Ok → Satisfied` once arrivals have closed and the need is empty.
- **Event-driven**: inside each `Tick` the wave jumps from event to event, so the frame rate never changes the result.

## Assumptions to confirm (not specified in the Build doc)

These are in the config asset as placeholders. Please confirm or replace them, and update this list.

| Topic | Current value | Where |
|---|---|---|
| Starting Coins / Energy | 0 / 0 | `economy.startingCoins`, `energy.startingEnergy` |
| Energy drop for Perfect | 5 (doc lists 5 values `[0,1,2,3,5]` for six tiers; Perfect assumed = 5★) | `energy.dropByRating` |
| Rewarded video reward | +3 Energy (one boost) | `energy.rewardedVideoEnergy` |
| Resort Value | starts at 100; +5…+30 per wave by rating; upgrades +20 (Lv2), +40 (Lv3); Pool +100, Bungalows +150 | `economy.*`, building levels |
| First Worker placement | Reception and Beach Bar staffed, Restaurant empty | `buildings[].staffedAtStart` |
| WAVE_INCOMING length | 3 s (doc: ~2–4 s) | `wave.incomingDuration` |
| Worker moves allowed in | AWAITING_WAVE, WAVE_INCOMING, WAVE_ACTIVE, UPGRADE (not paused, not on the result screen) | `ResortGame.MoveWorker` |
| After wave 8 | No more waves (`HasNextWave` false) | `ResortGame` |
| Arrival timing | unit k at (k+1)·T/N | `NeedService.ScheduleEvenly` |

The Energy spec, Decision Log and `wave_sim.py` may answer some of these. Share them when you can.
