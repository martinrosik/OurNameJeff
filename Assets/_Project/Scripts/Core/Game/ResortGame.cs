using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// The whole prototype game in plain C#: the core loop state machine (Build doc §4.3), Workers (§4.5),
    /// Energy boosts (§4.6), currencies, upgrades and area unlocks (§4.8). No Unity scene access, so it runs
    /// the same in EditMode tests, the balance tool and the game.
    /// <para>
    /// The Game layer calls <see cref="Tick"/> every frame, <see cref="Pause"/> when the app goes to the
    /// background, and forwards player intents (Start Wave, move Worker, Speed Up, Claim, Upgrade, Unlock).
    /// The UI reads state and listens to the events.
    /// </para>
    /// </summary>
    public sealed class ResortGame
    {
        private static readonly int BuildingCount = Enum.GetValues(typeof(BuildingId)).Length;

        private readonly BuildingRuntime[] _buildings;
        private readonly List<BuildingRuntime> _buildingList = new List<BuildingRuntime>();
        private WaveSimulation _wave;
        private GamePhase _phaseBeforePause;
        private double _incomingRemaining;

        public ResortGame(GameConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));

            _buildings = new BuildingRuntime[BuildingCount];
            foreach (BuildingDefinition definition in config.buildings)
            {
                var building = new BuildingRuntime(definition)
                {
                    IsUnlocked = definition.unlockedAtStart,
                    HasWorker = definition.unlockedAtStart && definition.staffedAtStart,
                };
                _buildings[(int)definition.building] = building;
                _buildingList.Add(building);
            }

            Coins = config.economy.startingCoins;
            Energy = config.energy.startingEnergy;
            ResortValue = config.economy.startingResortValue;
            Phase = GamePhase.AwaitingWave;
        }

        public GameConfig Config { get; }
        public GamePhase Phase { get; private set; }

        public int Coins { get; private set; }
        public int Energy { get; private set; }

        /// <summary>Status / prestige. Only ever grows (GDD §7.6).</summary>
        public int ResortValue { get; private set; }

        /// <summary>Waves finished so far. Also the index of the next wave, and the unlock gate in the prototype.</summary>
        public int CompletedWaves { get; private set; }

        public bool HasNextWave => CompletedWaves < Config.waves.Count;

        /// <summary>The wave being played (also during WAVE_COMPLETE), otherwise null.</summary>
        public WaveSimulation CurrentWave => _wave;

        /// <summary>Result of the most recent wave; stays available until the next one finishes.</summary>
        public WaveResult LastWaveResult { get; private set; }

        /// <summary>Seconds left of WAVE_INCOMING.</summary>
        public double IncomingRemaining => Phase == GamePhase.WaveIncoming ? _incomingRemaining : 0;

        /// <summary>All buildings in config order (owned and not owned).</summary>
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
        public event Action<NeedId, NeedState, NeedState> NeedStateChanged;
        public event Action<BuildingId, BuildingId> WorkerMoved;
        public event Action<BuildingId> BoostStarted;
        public event Action<BuildingId> BoostEnded;
        public event Action<BuildingId> RushStarted;
        public event Action<WaveResult> WaveCompleted;
        public event Action<BuildingId> BuildingUpgraded;
        public event Action<BuildingId> BuildingUnlocked;

        /// <summary>Coins, Energy or Resort Value changed.</summary>
        public event Action CurrenciesChanged;

        public BuildingRuntime GetBuilding(BuildingId id) => _buildings[(int)id];

        public bool IsWaveRunning =>
            Phase == GamePhase.WaveIncoming || Phase == GamePhase.WaveActive || Phase == GamePhase.WavePaused;

        // ------------------------------------------------------------------ queries

        public NeedStatus GetNeedStatus(NeedId need)
        {
            if (_wave != null)
            {
                return _wave.GetNeedStatus(need);
            }

            BuildingDefinition definition = Config.GetBuildingForNeed(need);
            BuildingRuntime building = definition == null ? null : _buildings[(int)definition.building];
            NeedState state = building != null && building.IsUnlocked ? NeedState.Ok : NeedState.Locked;
            return new NeedStatus(need, state, 0, 0, 0, 0);
        }

        /// <summary>Relative service speed of a building right now (see <see cref="WaveSimulation.GetServiceSpeed"/>).</summary>
        public double GetServiceSpeed(BuildingId id)
        {
            if (_wave != null)
            {
                return _wave.GetServiceSpeed(id);
            }

            BuildingRuntime building = _buildings[(int)id];
            if (building == null) return 0;
            return building.HasWorker ? Config.wave.utilizationStaffed : Config.wave.utilizationEmpty;
        }

        /// <summary>Forecast for the next wave, or null after the last one.</summary>
        public WaveForecast GetNextWaveForecast() => HasNextWave ? GetForecast(CompletedWaves) : null;

        public WaveForecast GetForecast(int waveIndex)
        {
            WaveDefinition wave = Config.waves[waveIndex];
            var demand = new List<NeedDemand>();
            foreach (NeedDefinition need in Config.needs)
            {
                BuildingDefinition definition = Config.GetBuildingForNeed(need.need);
                BuildingRuntime building = definition == null ? null : _buildings[(int)definition.building];
                if (building != null && building.IsUnlocked)
                {
                    demand.Add(new NeedDemand(need.need, wave.GetDemand(need.need)));
                }
            }

            return new WaveForecast(waveIndex, wave.guests, demand);
        }

        /// <summary>Coins needed for the next level, or null at max level.</summary>
        public int? GetUpgradeCost(BuildingId id)
        {
            BuildingRuntime building = _buildings[(int)id];
            if (building == null || building.IsMaxLevel) return null;
            return building.Definition.GetLevel(building.Level + 1).cost;
        }

        public UnlockStatus GetUnlockStatus(BuildingId id)
        {
            BuildingRuntime building = _buildings[(int)id];
            if (building == null) return UnlockStatus.Locked;
            if (building.IsUnlocked) return UnlockStatus.Unlocked;
            if (CompletedWaves < building.Definition.unlockAfterWave) return UnlockStatus.Locked;
            return Coins >= building.Definition.unlockCost ? UnlockStatus.Available : UnlockStatus.NotAffordable;
        }

        /// <summary>Buildings whose gate is met but which the player hasn't bought (S9 candidates).</summary>
        public List<BuildingId> GetAvailableUnlocks()
        {
            var result = new List<BuildingId>();
            foreach (BuildingRuntime building in _buildingList)
            {
                UnlockStatus status = GetUnlockStatus(building.Id);
                if (status == UnlockStatus.Available || status == UnlockStatus.NotAffordable)
                {
                    result.Add(building.Id);
                }
            }

            return result;
        }

        // ------------------------------------------------------------------ core loop

        /// <summary>AWAITING_WAVE → WAVE_INCOMING. Returns false if not possible now.</summary>
        public bool StartWave()
        {
            if (Phase != GamePhase.AwaitingWave || !HasNextWave)
            {
                return false;
            }

            DetachWave();
            _wave = new WaveSimulation(Config, CompletedWaves, _buildings);
            _wave.NeedStateChanged += OnNeedStateChanged;
            _wave.RushStarted += OnRushStarted;
            _wave.BoostEnded += OnBoostEnded;

            _incomingRemaining = Config.wave.incomingDuration;
            SetPhase(_incomingRemaining > 0 ? GamePhase.WaveIncoming : GamePhase.WaveActive);
            return true;
        }

        /// <summary>Advances the game by <paramref name="deltaSeconds"/> of real time. Does nothing while paused.</summary>
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

            _wave.Advance(deltaSeconds);
            if (_wave.IsFinished)
            {
                ResolveWave();
            }
        }

        /// <summary>App went to the background or is closing: freeze everything (WAVE_PAUSED).</summary>
        public void Pause()
        {
            if (Phase != GamePhase.WaveIncoming && Phase != GamePhase.WaveActive)
            {
                return;
            }

            _phaseBeforePause = Phase;
            SetPhase(GamePhase.WavePaused);
        }

        /// <summary>Back exactly where the player left off (AC5).</summary>
        public void Resume()
        {
            if (Phase == GamePhase.WavePaused)
            {
                SetPhase(_phaseBeforePause);
            }
        }

        /// <summary>WAVE_COMPLETE → UPGRADE: pays out the wave's rewards.</summary>
        public bool ClaimRewards()
        {
            if (Phase != GamePhase.WaveComplete || LastWaveResult == null)
            {
                return false;
            }

            Coins += LastWaveResult.Coins;
            Energy += LastWaveResult.Energy;
            ResortValue += LastWaveResult.ResortValue;
            CurrenciesChanged?.Invoke();
            SetPhase(GamePhase.Upgrade);
            return true;
        }

        /// <summary>UPGRADE → AWAITING_WAVE (Upgrade Now done, or Later).</summary>
        public bool FinishUpgrades()
        {
            if (Phase != GamePhase.Upgrade)
            {
                return false;
            }

            DetachWave();
            SetPhase(GamePhase.AwaitingWave);
            return true;
        }

        // ------------------------------------------------------------------ player actions

        /// <summary>
        /// Moves a Worker. Free and instant, before or during a wave (WR5). In the prototype every building has
        /// one slot, so the target must be empty: the choice is "which building do I leave empty?".
        /// </summary>
        public MoveWorkerResult MoveWorker(BuildingId from, BuildingId to)
        {
            if (Phase != GamePhase.AwaitingWave && Phase != GamePhase.Upgrade
                && Phase != GamePhase.WaveIncoming && Phase != GamePhase.WaveActive)
            {
                return MoveWorkerResult.NotAllowedNow;
            }

            if (from == to) return MoveWorkerResult.SameBuilding;

            BuildingRuntime source = _buildings[(int)from];
            BuildingRuntime target = _buildings[(int)to];
            if (source == null || target == null || !source.IsUnlocked || !target.IsUnlocked)
            {
                return MoveWorkerResult.BuildingNotOwned;
            }

            if (!source.HasWorker) return MoveWorkerResult.NoWorkerAtSource;
            if (target.HasWorker) return MoveWorkerResult.TargetFull;

            source.HasWorker = false;
            target.HasWorker = true;
            _wave?.OnServiceSpeedChanged();
            WorkerMoved?.Invoke(from, to);
            return MoveWorkerResult.Moved;
        }

        /// <summary>The single owned building without a Worker (prototype deficit of 1), or null.</summary>
        public BuildingId? GetEmptyBuilding()
        {
            foreach (BuildingRuntime building in _buildingList)
            {
                if (building.IsUnlocked && !building.HasWorker) return building.Id;
            }

            return null;
        }

        /// <summary>Energy Mode A: Speed Up one building for a while, once per building per wave (AC4c).</summary>
        public BoostResult BoostBuilding(BuildingId id)
        {
            if (Phase != GamePhase.WaveActive) return BoostResult.NotAllowedNow;

            BuildingRuntime building = _buildings[(int)id];
            if (building == null || !building.IsUnlocked) return BoostResult.BuildingNotOwned;
            if (building.BoostsUsedThisWave >= Config.energy.maxBoostsPerBuildingPerWave) return BoostResult.AlreadyBoostedThisWave;
            if (Energy < Config.energy.boostCost) return BoostResult.NotEnoughEnergy;

            Energy -= Config.energy.boostCost;
            building.BoostsUsedThisWave++;
            building.BoostRemaining = Config.energy.boostDuration;
            _wave.OnServiceSpeedChanged();
            CurrenciesChanged?.Invoke();
            BoostStarted?.Invoke(id);
            return BoostResult.Boosted;
        }

        /// <summary>Whether Speed Up should be offered on this building right now.</summary>
        public bool CanOfferBoost(BuildingId id)
        {
            BuildingRuntime building = _buildings[(int)id];
            return Phase == GamePhase.WaveActive && building != null && building.IsUnlocked
                   && building.BoostsUsedThisWave < Config.energy.maxBoostsPerBuildingPerWave;
        }

        /// <summary>Reward for a voluntary rewarded video ("Watch ad for ⚡"). The ad itself is the Game layer's job.</summary>
        public void GrantRewardedVideoEnergy()
        {
            Energy += Config.energy.rewardedVideoEnergy;
            CurrenciesChanged?.Invoke();
        }

        /// <summary>More seats, between waves only (WR6). Upgrades never change service speed.</summary>
        public UpgradeResult UpgradeBuilding(BuildingId id)
        {
            if (Phase != GamePhase.AwaitingWave && Phase != GamePhase.Upgrade) return UpgradeResult.NotAllowedNow;

            BuildingRuntime building = _buildings[(int)id];
            if (building == null || !building.IsUnlocked) return UpgradeResult.BuildingNotOwned;
            if (building.IsMaxLevel) return UpgradeResult.AlreadyMaxLevel;

            BuildingLevel next = building.Definition.GetLevel(building.Level + 1);
            if (Coins < next.cost) return UpgradeResult.NotEnoughCoins;

            Coins -= next.cost;
            building.Level++;
            ResortValue += next.resortValue;
            CurrenciesChanged?.Invoke();
            BuildingUpgraded?.Invoke(id);
            return UpgradeResult.Upgraded;
        }

        /// <summary>
        /// Buys an available area (S9). The building comes with one Worker, placed on it, so the
        /// deficit stays 1 and every need can always be served (AC7a/b).
        /// </summary>
        public UnlockResult UnlockBuilding(BuildingId id)
        {
            if (Phase != GamePhase.AwaitingWave && Phase != GamePhase.Upgrade) return UnlockResult.NotAllowedNow;

            BuildingRuntime building = _buildings[(int)id];
            if (building == null) return UnlockResult.NotInGame;

            switch (GetUnlockStatus(id))
            {
                case UnlockStatus.Unlocked: return UnlockResult.AlreadyUnlocked;
                case UnlockStatus.Locked: return UnlockResult.GateNotMet;
                case UnlockStatus.NotAffordable: return UnlockResult.NotEnoughCoins;
            }

            Coins -= building.Definition.unlockCost;
            building.IsUnlocked = true;
            building.Level = 1;
            building.HasWorker = true;
            ResortValue += building.Definition.unlockResortValue;
            CurrenciesChanged?.Invoke();
            BuildingUnlocked?.Invoke(id);
            return UnlockResult.Unlocked;
        }

        // ------------------------------------------------------------------ debug / playtest panel

        /// <summary>Playtest cheat: add Coins. Never call from gameplay code.</summary>
        public void DebugAddCoins(int amount)
        {
            Coins = Math.Max(0, Coins + amount);
            CurrenciesChanged?.Invoke();
        }

        /// <summary>Playtest cheat: add Energy. Never call from gameplay code.</summary>
        public void DebugAddEnergy(int amount)
        {
            Energy = Math.Max(0, Energy + amount);
            CurrenciesChanged?.Invoke();
        }

        /// <summary>Playtest cheat: jump to a wave (between waves only). Unlock gates follow the wave count.</summary>
        public bool DebugSetCompletedWaves(int completedWaves)
        {
            if (Phase != GamePhase.AwaitingWave) return false;
            CompletedWaves = Math.Max(0, Math.Min(completedWaves, Config.waves.Count));
            return true;
        }

        // ------------------------------------------------------------------ internals

        private void ResolveWave()
        {
            SetPhase(GamePhase.WaveResolving);
            LastWaveResult = _wave.BuildResult();
            CompletedWaves++;
            SetPhase(GamePhase.WaveComplete);
            WaveCompleted?.Invoke(LastWaveResult);
        }

        private void SetPhase(GamePhase phase)
        {
            if (Phase == phase) return;
            GamePhase previous = Phase;
            Phase = phase;
            PhaseChanged?.Invoke(previous, phase);
        }

        private void DetachWave()
        {
            if (_wave == null) return;
            _wave.NeedStateChanged -= OnNeedStateChanged;
            _wave.RushStarted -= OnRushStarted;
            _wave.BoostEnded -= OnBoostEnded;
            _wave = null;
        }

        private void OnNeedStateChanged(NeedId need, NeedState from, NeedState to) => NeedStateChanged?.Invoke(need, from, to);
        private void OnRushStarted(BuildingId building) => RushStarted?.Invoke(building);
        private void OnBoostEnded(BuildingId building) => BoostEnded?.Invoke(building);
    }
}
