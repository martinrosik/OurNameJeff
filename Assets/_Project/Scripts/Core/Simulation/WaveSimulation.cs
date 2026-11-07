using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// One running wave (Build doc §4.4–§4.6): steady arrivals for <see cref="WaveSettings.waveTimer"/> seconds,
    /// then drain until every queue is empty. Tracks seats, queues, per-unit waiting, need states and the
    /// Sudden rush. Inside each <see cref="Advance"/> call it jumps from event to event (arrival, service done,
    /// boost end…), so results don't depend on the frame rate.
    /// </summary>
    public sealed class WaveSimulation
    {
        private const double Epsilon = 1e-9;

        /// <summary>Need states are re-checked at least this often (seconds of wave time). Affects UI signals only.</summary>
        private const double StateEvaluationStep = 0.25;

        private static readonly int NeedCount = Enum.GetValues(typeof(NeedId)).Length;

        private readonly GameConfig _config;
        private readonly WaveDefinition _definition;
        private readonly BuildingRuntime[] _buildings;
        private readonly NeedService[] _needs;
        private readonly List<NeedService> _activeNeeds = new List<NeedService>();
        private readonly List<BuildingId> _endedBoosts = new List<BuildingId>();
        private bool _rushTriggered;

        /// <param name="buildings">Indexed by <see cref="BuildingId"/>; entries may be null for buildings not in the config.</param>
        public WaveSimulation(GameConfig config, int waveIndex, BuildingRuntime[] buildings)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (waveIndex < 0 || waveIndex >= config.waves.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex), $"Config has {config.waves.Count} waves.");
            }

            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _definition = config.waves[waveIndex];
            WaveIndex = waveIndex;

            foreach (BuildingRuntime building in _buildings)
            {
                if (building != null)
                {
                    building.BoostRemaining = 0;
                    building.BoostsUsedThisWave = 0;
                }
            }

            _needs = new NeedService[NeedCount];
            foreach (NeedDefinition needDefinition in config.needs)
            {
                BuildingDefinition buildingDefinition = config.GetBuildingForNeed(needDefinition.need);
                if (buildingDefinition == null)
                {
                    continue;
                }

                BuildingRuntime building = _buildings[(int)buildingDefinition.building];
                var service = new NeedService(needDefinition.need, building, needDefinition.satisfactionWeight);
                _needs[(int)needDefinition.need] = service;

                // WR9: the need of a building the player hasn't bought never spawns.
                if (building == null || !building.IsUnlocked)
                {
                    service.State = NeedState.Locked;
                    continue;
                }

                int demand = _definition.GetDemand(needDefinition.need);
                service.ScheduleEvenly(demand, 0, config.wave.waveTimer);
                service.BaseArrivalRate = demand / (double)config.wave.waveTimer;
                service.State = NeedState.Ok;
                service.RecordQueue(0, config.wave.bottleneckHysteresis);
                _activeNeeds.Add(service);
            }

            ProcessInstant();
        }

        public int WaveIndex { get; }
        public WaveDefinition Definition => _definition;

        /// <summary>Seconds since the first guest could arrive.</summary>
        public double Time { get; private set; }

        /// <summary>True once wave_timer has passed and every scheduled unit (rush included) has arrived.</summary>
        public bool ArrivalsClosed { get; private set; }

        /// <summary>Arrivals closed and every queue and seat is empty.</summary>
        public bool IsFinished { get; private set; }

        public BuildingId? RushTarget { get; private set; }

        public event Action<NeedId, NeedState, NeedState> NeedStateChanged;
        public event Action<BuildingId> RushStarted;
        public event Action<BuildingId> BoostEnded;

        public NeedStatus GetNeedStatus(NeedId need)
        {
            NeedService service = _needs[(int)need];
            if (service == null || service.State == NeedState.Locked)
            {
                return new NeedStatus(need, NeedState.Locked, 0, 0, 0, 0);
            }

            return new NeedStatus(need, service.State, service.Served, service.Total, service.QueueLength, service.Seated);
        }

        /// <summary>
        /// Service speed relative to a staffed, unboosted building: 0.33 without a Worker, 1.0 with one,
        /// doubled while an Energy boost runs (default numbers).
        /// </summary>
        public double GetServiceSpeed(BuildingId building)
        {
            BuildingRuntime runtime = _buildings[(int)building];
            return runtime == null ? 0 : Utilization(runtime) * BoostFactor(runtime);
        }

        /// <summary>Moves the wave forward by <paramref name="seconds"/> of wave time.</summary>
        public void Advance(double seconds)
        {
            if (IsFinished || seconds <= 0)
            {
                return;
            }

            double target = Time + seconds;
            while (!IsFinished && Time < target - Epsilon)
            {
                double next = NextEventTime(target);
                double step = next - Time;
                if (step > 0)
                {
                    Integrate(step);
                }

                Time = next;
                ProcessInstant();
            }
        }

        /// <summary>
        /// Call after a Worker move or Energy boost so the panel reacts in the same interaction (AC2):
        /// BOTTLENECK turns into RECOVERING as soon as the building serves faster than guests arrive.
        /// </summary>
        internal void OnServiceSpeedChanged() => EvaluateNeedStates();

        public WaveResult BuildResult()
        {
            var stats = new List<NeedWaveStats>(_activeNeeds.Count);
            double weightedContributions = 0;
            double weights = 0;
            int units = 0;

            foreach (NeedService need in _activeNeeds)
            {
                weightedContributions += need.ContributionSum * need.Weight;
                weights += need.UnitsSeated * need.Weight;
                units += need.Served;

                double averageContribution = need.UnitsSeated > 0 ? need.ContributionSum / need.UnitsSeated : 1.0;
                double averageWait = need.UnitsSeated > 0 ? need.WaitSum / need.UnitsSeated : 0;
                stats.Add(new NeedWaveStats(
                    need.Need,
                    need.Building.Id,
                    need.Served,
                    averageContribution,
                    averageWait,
                    need.MaxWait,
                    need.UnitsWithinGrace,
                    need.BottleneckSeconds));
            }

            double satisfaction = SatisfactionMath.WaveSatisfaction(weightedContributions, weights, _config.satisfaction);
            StarRating rating = SatisfactionMath.RatingFor(satisfaction, _config.satisfaction);
            int tier = rating.ToIndex();

            float coinMultiplier = ValueAt(_config.economy.coinsRatingMultipliers, tier, 1f);
            int coins = (int)Math.Round(units * _config.economy.coinsPerUnit * (double)coinMultiplier, MidpointRounding.AwayFromZero);
            int energy = ValueAt(_config.energy.dropByRating, tier, 0);
            int resortValue = ValueAt(_config.economy.resortValueByRating, tier, 0);

            return new WaveResult(
                WaveIndex,
                _definition.guests,
                units,
                satisfaction,
                rating,
                coins,
                energy,
                resortValue,
                Time,
                stats,
                RecommendUpgrade(stats),
                RushTarget);
        }

        private double NextEventTime(double target)
        {
            double next = target;

            double grid = (Math.Floor(Time / StateEvaluationStep + 1e-6) + 1) * StateEvaluationStep;
            next = Math.Min(next, grid);

            if (Time < _config.wave.waveTimer)
            {
                next = Math.Min(next, _config.wave.waveTimer);
            }

            if (IsRushPending)
            {
                next = Math.Min(next, _definition.rush.startTime);
            }

            foreach (NeedService need in _activeNeeds)
            {
                next = Math.Min(next, need.NextArrivalTime);
                next = Math.Min(next, need.NextCompletionTime(Time, ProgressPerSecond(need.Building)));
            }

            foreach (BuildingRuntime building in _buildings)
            {
                if (building != null && building.BoostRemaining > 0)
                {
                    next = Math.Min(next, Time + building.BoostRemaining);
                }
            }

            return Math.Max(next, Time);
        }

        private void Integrate(double step)
        {
            foreach (NeedService need in _activeNeeds)
            {
                need.AdvanceService(ProgressPerSecond(need.Building) * step);
                if (need.State == NeedState.Bottleneck)
                {
                    need.BottleneckSeconds += step;
                }
            }

            foreach (BuildingRuntime building in _buildings)
            {
                if (building == null || building.BoostRemaining <= 0)
                {
                    continue;
                }

                building.BoostRemaining -= step;
                if (building.BoostRemaining <= Epsilon)
                {
                    building.BoostRemaining = 0;
                    _endedBoosts.Add(building.Id);
                }
            }
        }

        /// <summary>Handles everything that happens exactly at <see cref="Time"/>.</summary>
        private void ProcessInstant()
        {
            foreach (NeedService need in _activeNeeds)
            {
                need.CompleteFinished();
            }

            if (_endedBoosts.Count > 0)
            {
                foreach (BuildingId building in _endedBoosts)
                {
                    BoostEnded?.Invoke(building);
                }

                _endedBoosts.Clear();
            }

            if (IsRushPending && Time >= _definition.rush.startTime - Epsilon)
            {
                TriggerRush();
            }

            foreach (NeedService need in _activeNeeds)
            {
                need.ArriveUntil(Time);
                need.SeatFromQueue(Time, need.Building.Seats, _config.satisfaction);
                need.RecordQueue(Time, _config.wave.bottleneckHysteresis);
            }

            if (!ArrivalsClosed && Time >= _config.wave.waveTimer - Epsilon && !IsRushPending && AllArrived())
            {
                ArrivalsClosed = true;
            }

            EvaluateNeedStates();

            if (ArrivalsClosed && AllEmpty())
            {
                IsFinished = true;
            }
        }

        private bool IsRushPending => _definition.rush != null && _definition.rush.enabled && !_rushTriggered;

        /// <summary>Sudden rush hits the building that is unstaffed right now, so moving a Worker always solves it (AC8).</summary>
        private void TriggerRush()
        {
            _rushTriggered = true;
            RushEventDefinition rush = _definition.rush;
            if (rush.units <= 0)
            {
                return;
            }

            NeedService target = null;
            foreach (NeedService need in _activeNeeds)
            {
                if (!need.Building.HasWorker && (target == null || need.Building.Id < target.Building.Id))
                {
                    target = need;
                }
            }

            if (target == null && _activeNeeds.Count > 0)
            {
                target = _activeNeeds[0];
            }

            if (target == null)
            {
                return;
            }

            target.AddRush(rush.startTime, rush.duration, rush.units);
            RushTarget = target.Building.Id;
            RushStarted?.Invoke(target.Building.Id);
        }

        private void EvaluateNeedStates()
        {
            int threshold = _config.wave.bottleneckThreshold;
            double hysteresis = _config.wave.bottleneckHysteresis;

            foreach (NeedService need in _activeNeeds)
            {
                NeedState before = need.State;
                double arrivalRate = CurrentArrivalRate(need);
                double fillRate = need.Building.Seats * ProgressPerSecond(need.Building);
                bool catchingUp = fillRate > arrivalRate + Epsilon;
                int waiting = need.QueueLength;
                bool grew = waiting > need.QueueAt(Time - hysteresis);
                bool done = ArrivalsClosed && need.AllArrived && need.IsEmpty;

                for (int guard = 0; guard < 4; guard++)
                {
                    NeedState next = need.State;
                    switch (need.State)
                    {
                        case NeedState.Ok:
                            if (done) next = NeedState.Satisfied;
                            else if (waiting > threshold && grew) next = NeedState.Bottleneck;
                            break;
                        case NeedState.Bottleneck:
                            if (catchingUp) next = NeedState.Recovering;
                            break;
                        case NeedState.Recovering:
                            if (waiting <= threshold) next = NeedState.Ok;
                            else if (!catchingUp && grew) next = NeedState.Bottleneck;
                            break;
                    }

                    if (next == need.State)
                    {
                        break;
                    }

                    need.State = next;
                }

                if (need.State != before)
                {
                    NeedStateChanged?.Invoke(need.Need, before, need.State);
                }
            }
        }

        private double CurrentArrivalRate(NeedService need)
        {
            double rate = Time < _config.wave.waveTimer - Epsilon ? need.BaseArrivalRate : 0;
            if (need.RushRate > 0 && Time >= need.RushStart - Epsilon && Time < need.RushEnd - Epsilon)
            {
                rate += need.RushRate;
            }

            return rate;
        }

        private double ProgressPerSecond(BuildingRuntime building) =>
            Utilization(building) * BoostFactor(building) / _config.wave.serviceTime;

        private double Utilization(BuildingRuntime building) =>
            building.HasWorker ? _config.wave.utilizationStaffed : _config.wave.utilizationEmpty;

        private double BoostFactor(BuildingRuntime building) =>
            building.BoostRemaining > 0 ? 1.0 / (1.0 - _config.energy.speedReductionPct) : 1.0;

        private bool AllArrived()
        {
            foreach (NeedService need in _activeNeeds)
            {
                if (!need.AllArrived) return false;
            }

            return true;
        }

        private bool AllEmpty()
        {
            foreach (NeedService need in _activeNeeds)
            {
                if (!need.IsEmpty) return false;
            }

            return true;
        }

        /// <summary>AC6: suggest the building whose need was in BOTTLENECK longest, if it can still be upgraded.</summary>
        private BuildingId? RecommendUpgrade(List<NeedWaveStats> stats)
        {
            NeedWaveStats best = null;
            foreach (NeedWaveStats candidate in stats)
            {
                BuildingRuntime building = _buildings[(int)candidate.Building];
                if (building == null || building.IsMaxLevel)
                {
                    continue;
                }

                if (best == null
                    || candidate.BottleneckSeconds > best.BottleneckSeconds + Epsilon
                    || (Math.Abs(candidate.BottleneckSeconds - best.BottleneckSeconds) <= Epsilon
                        && candidate.AverageContribution < best.AverageContribution - Epsilon))
                {
                    best = candidate;
                }
            }

            if (best == null || (best.BottleneckSeconds <= Epsilon && best.AverageContribution >= 1.0 - Epsilon))
            {
                return null;
            }

            return best.Building;
        }

        private static T ValueAt<T>(T[] values, int index, T fallback) =>
            values != null && index >= 0 && index < values.Length ? values[index] : fallback;
    }
}
