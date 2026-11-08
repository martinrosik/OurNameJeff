using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// The game in plain C# (no Unity scene access), so it runs the same in EditMode tests and in the game.
    /// <para>
    /// Implemented now: the starting island (OUR-31), Workers placed from the config (shown by OUR-48),
    /// building capacity (shown by OUR-50), the next-wave forecast (OUR-33), the manual wave start (OUR-27):
    /// AWAITING_WAVE → WAVE_INCOMING (boat) → WAVE_ACTIVE, guest arrivals over the 90 s arrival phase
    /// (OUR-34, <see cref="WaveSimulation"/>), the drain after it (OUR-41) and the wave end (OUR-35):
    /// WAVE_ACTIVE → WAVE_RESOLVING (everyone served, guests return to the boat) → WAVE_COMPLETE. The rest comes with the
    /// teammates' tickets add guests and the wave end. Each one has a <c>// OUR-xx</c> hook below and a guide in
    /// <c>docs/tasks/</c>.
    /// </para>
    /// The Game layer calls <see cref="Tick"/> every frame and forwards player intents; views read state.
    /// </summary>
    public sealed class ResortGame
    {
        private static readonly int BuildingCount = Enum.GetValues(typeof(BuildingId)).Length;

        private readonly BuildingRuntime[] _buildings;
        private readonly List<BuildingRuntime> _buildingList = new List<BuildingRuntime>();
        private double _incomingRemaining;

        public ResortGame(GameConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));

            _buildings = new BuildingRuntime[BuildingCount];
            foreach (BuildingDefinition definition in config.buildings)
            {
                var building = new BuildingRuntime(definition);
                _buildings[(int)definition.building] = building;
                _buildingList.Add(building);
            }

            Phase = GamePhase.AwaitingWave;
        }

        public GameConfig Config { get; }
        public GamePhase Phase { get; private set; }

        /// <summary>Waves finished so far; also the index of the next wave. Grows when a wave completes (OUR-35).</summary>
        public int CompletedWaves { get; private set; }

        public bool HasNextWave => CompletedWaves < Config.waves.Count;

        /// <summary>Index of the wave being played (valid from WAVE_INCOMING to WAVE_RESOLVING).</summary>
        public int CurrentWaveIndex => CompletedWaves;

        /// <summary>Seconds of WAVE_ACTIVE so far. OUR-34 uses it for the 90 s arrival phase.</summary>
        public double WaveTime { get; private set; }

        /// <summary>
        /// OUR-34: the wave being played (arrivals, queues, seats). Created by <see cref="StartWave"/>, null before
        /// the first wave.
        /// </summary>
        public WaveSimulation CurrentWave { get; private set; }

        /// <summary>Seconds left of WAVE_INCOMING (the boat sailing in).</summary>
        public double IncomingRemaining => Phase == GamePhase.WaveIncoming ? _incomingRemaining : 0;

        /// <summary>Buildings on the island, in config order.</summary>
        public IReadOnlyList<BuildingRuntime> Buildings => _buildingList;

        public int WorkerCount
        {
            get
            {
                int count = 0;
                foreach (BuildingRuntime building in _buildingList)
                {
                    if (building.HasWorker) count++;
                }

                return count;
            }
        }

        public event Action<GamePhase, GamePhase> PhaseChanged;

        /// <summary>OUR-35: raised when a wave reaches WAVE_COMPLETE. OUR-40 tells the player.</summary>
        public event Action<WaveResult> WaveCompleted;

        /// <summary>OUR-35: the result of the last finished wave (null before the first one).</summary>
        public WaveResult LastWaveResult { get; private set; }

        public BuildingRuntime GetBuilding(BuildingId id) => _buildings[(int)id];

        /// <summary>
        /// Guests currently being served at a building (fills OUR-50's seat squares).
        /// Seats fill as guests arrive (OUR-34); they empty again once service exists (OUR-46/47).
        /// </summary>
        public int GetSeatedCount(BuildingId building)
        {
            BuildingRuntime runtime = GetBuilding(building);
            return runtime == null ? 0 : GetNeedStatus(runtime.Need).Seated;
        }

        /// <summary>OUR-34: live numbers of a need in the current wave (all zero outside a wave).</summary>
        public NeedStatus GetNeedStatus(NeedId need) =>
            CurrentWave != null ? CurrentWave.GetNeedStatus(need) : new NeedStatus(need, 0, 0, 0, 0, 0);

        /// <summary>OUR-33: "Next wave: Rooms 8 · Drinks 8 · Food 6" before Start Wave. Null after the last wave.</summary>
        public WaveForecast GetNextWaveForecast() => HasNextWave ? GetForecast(CompletedWaves) : null;

        /// <summary>Demand for a wave, limited to needs that have a building on the island.</summary>
        public WaveForecast GetForecast(int waveIndex)
        {
            WaveDefinition wave = Config.waves[waveIndex];
            var demand = new List<NeedDemand>();
            foreach (NeedDefinition need in Config.needs)
            {
                if (Config.GetBuildingForNeed(need.need) != null)
                {
                    demand.Add(new NeedDemand(need.need, wave.GetDemand(need.need)));
                }
            }

            return new WaveForecast(waveIndex, wave.guests, demand);
        }

        /// <summary>OUR-27: the player starts the wave manually. AWAITING_WAVE → WAVE_INCOMING.</summary>
        public bool StartWave()
        {
            if (Phase != GamePhase.AwaitingWave || !HasNextWave)
            {
                return false;
            }

            WaveTime = 0;
            CurrentWave = new WaveSimulation(
                GetForecast(CompletedWaves),
                Config.wave.waveTimer,
                need => Config.GetBuildingForNeed(need) is BuildingDefinition b ? GetBuilding(b.building).Seats : 0);
            _incomingRemaining = Config.wave.incomingDuration;
            SetPhase(_incomingRemaining > 0 ? GamePhase.WaveIncoming : GamePhase.WaveActive);
            return true;
        }

        /// <summary>Advances the game by <paramref name="deltaSeconds"/>.</summary>
        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds <= 0)
            {
                return;
            }

            if (Phase == GamePhase.WaveIncoming)
            {
                _incomingRemaining -= deltaSeconds;
                if (_incomingRemaining > 0)
                {
                    return;
                }

                deltaSeconds = -_incomingRemaining;
                _incomingRemaining = 0;
                SetPhase(GamePhase.WaveActive);
            }

            if (Phase != GamePhase.WaveActive)
            {
                return;
            }

            WaveTime += deltaSeconds;

            // OUR-34: release this wave's demand evenly over the 90 s arrival phase (unit k at (k+1)·90/N s).
            CurrentWave.AdvanceTo(WaveTime);

            // OUR-46 / OUR-47: serve seated guests at full speed with a Worker, about a third of it without.
            // OUR-38 / OUR-39: update each need's state (queue growing → BOTTLENECK, catching up → RECOVERING).
            // OUR-41: after 90 s no new guests come (CurrentWave.ArrivalsClosed), but nobody leaves: the queues are
            // still served until they are empty (WR3, WR4). Nothing ends the wave on a timer.
            // OUR-35: once everyone is served, the wave resolves. The guests then walk back to the boat (Game layer)
            // and FinishResolving() completes the wave. OUR-40 tells the player.
            if (CurrentWave.IsFinished)
            {
                LastWaveResult = new WaveResult(
                    CurrentWave.WaveIndex, CurrentWave.GuestCount, CurrentWave.TotalServed, CurrentWave.TotalUnits, WaveTime);
                SetPhase(GamePhase.WaveResolving);
            }
        }

        /// <summary>
        /// OUR-35: WAVE_RESOLVING → WAVE_COMPLETE. The Game layer calls it once every guest is back on the boat
        /// (or right away when there are no guest views). Counts the wave and shows the next forecast.
        /// </summary>
        public bool FinishResolving()
        {
            if (Phase != GamePhase.WaveResolving) return false;

            CompletedWaves++;
            SetPhase(GamePhase.WaveComplete);
            WaveCompleted?.Invoke(LastWaveResult);
            return true;
        }

        private void SetPhase(GamePhase phase)
        {
            if (Phase == phase) return;
            GamePhase previous = Phase;
            Phase = phase;
            PhaseChanged?.Invoke(previous, phase);
        }
    }
}
