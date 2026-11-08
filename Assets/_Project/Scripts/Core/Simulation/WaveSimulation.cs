using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// One wave in progress (Build doc §4.4). Created by <see cref="ResortGame.StartWave"/> and advanced to the
    /// current wave time by <see cref="ResortGame.Tick"/>.
    /// <para>
    /// <b>OUR-34 arrivals:</b> each need's demand arrives evenly over <see cref="WaveSettings.waveTimer"/>:
    /// unit k (0-based) arrives at (k + 1) × waveTimer ÷ demand, so the last unit arrives exactly when the timer
    /// runs out. Arrivals are processed in time order from a precomputed schedule, so the result never depends on
    /// the frame rate. Only needs with a building on the island spawn (WR9). An arriving unit takes a free seat,
    /// or waits in the building's FIFO queue (the arrival time is kept for waiting time and satisfaction later).
    /// </para>
    /// <para>
    /// <b>OUR-41 drain:</b> after the arrival timer nobody new comes, but nobody leaves either (WR4): every unit
    /// that arrived stays until it is served. <see cref="IsFinished"/> turns true once arrivals are closed and every
    /// queue and seat is empty; that ends the wave (OUR-35).
    /// </para>
    /// <para>
    /// <b>OUR-46 / OUR-47 service:</b> each seated unit's progress goes from 0 to 1 at the building's service speed
    /// (utilization ÷ serviceTime per second, read every step, so a Worker arriving or leaving changes it at once for
    /// everyone seated). A finished unit leaves its seat and the first one in the queue sits down at that moment.
    /// Arrivals and service ends are processed as one time-ordered sequence, so big and small steps give the same
    /// result.
    /// </para>
    /// <para>
    /// <b>OUR-38 / OUR-39 need states:</b> each need has a <see cref="NeedState"/> (Build doc §4.4). States are
    /// checked at every event and every <see cref="StateCheckInterval"/> s of wave time (a fixed grid, never at
    /// frame times), so they don't depend on the frame rate either, and right after a Worker move
    /// (<see cref="RefreshStates"/>).
    /// </para>
    /// </summary>
    public sealed class WaveSimulation
    {
        /// <summary>Tolerance for comparing accumulated frame times against the schedule.</summary>
        private const double TimeEpsilon = 1e-6;

        /// <summary>States are re-checked at least this often (wave time), besides every event.</summary>
        public const double StateCheckInterval = 0.25;

        private readonly Dictionary<NeedId, NeedQueue> _needs = new Dictionary<NeedId, NeedQueue>();
        private readonly List<NeedQueue> _needList = new List<NeedQueue>();
        private readonly Func<NeedId, double> _serviceSpeedForNeed;
        private readonly int _bottleneckThreshold;
        private readonly double _bottleneckHysteresis;
        private double _nextStateCheck;
        private double _lastStateCheck;

        /// <param name="forecast">Demand for the needs on the island (already filtered by WR9).</param>
        /// <param name="waveTimer">Length of the arrival phase in seconds.</param>
        /// <param name="seatsForNeed">Seats of the building that serves a need.</param>
        /// <param name="serviceSpeedForNeed">
        /// OUR-46 / OUR-47: progress per second of one seated unit of a need (utilization ÷ serviceTime). Read on every
        /// step. Null: nobody is served automatically (only <see cref="FinishService"/>).
        /// </param>
        /// <param name="settings">
        /// OUR-38 / OUR-39: bottleneck threshold and hysteresis. Null uses the Build doc defaults of
        /// <see cref="WaveSettings"/>.
        /// </param>
        public WaveSimulation(
            WaveForecast forecast,
            double waveTimer,
            Func<NeedId, int> seatsForNeed,
            Func<NeedId, double> serviceSpeedForNeed = null,
            WaveSettings settings = null)
        {
            if (forecast == null) throw new ArgumentNullException(nameof(forecast));
            if (seatsForNeed == null) throw new ArgumentNullException(nameof(seatsForNeed));
            if (waveTimer <= 0) throw new ArgumentOutOfRangeException(nameof(waveTimer), "The arrival phase must be longer than 0 s.");

            _serviceSpeedForNeed = serviceSpeedForNeed;
            settings = settings ?? new WaveSettings();
            _bottleneckThreshold = Math.Max(1, settings.bottleneckThreshold);
            _bottleneckHysteresis = Math.Max(TimeEpsilon, settings.bottleneckHysteresis);
            _nextStateCheck = StateCheckInterval;
            WaveIndex = forecast.WaveIndex;
            GuestCount = forecast.Guests;
            WaveTimer = waveTimer;
            foreach (NeedDemand demand in forecast.Demand)
            {
                var queue = new NeedQueue(demand.Need, demand.Units, waveTimer, seatsForNeed(demand.Need));
                _needs[demand.Need] = queue;
                _needList.Add(queue);
            }

            ReadSpeeds();
        }

        /// <summary>OUR-38 / OUR-39: a need changed state (need, from, to).</summary>
        public event Action<NeedId, NeedState, NeedState> StateChanged;

        /// <summary>
        /// OUR-38 / OUR-39: the need's state. <see cref="NeedState.Locked"/> for a need that isn't in this wave
        /// (its building isn't on the island).
        /// </summary>
        public NeedState GetState(NeedId need) => _needs.TryGetValue(need, out NeedQueue queue) ? queue.State : NeedState.Locked;

        /// <summary>Seconds of wave time the need has spent in BOTTLENECK (AC6 recommends the upgrade for the longest one).</summary>
        public double GetBottleneckSeconds(NeedId need) => _needs.TryGetValue(need, out NeedQueue queue) ? queue.BottleneckSeconds : 0;

        /// <summary>
        /// OUR-39: re-reads the service speeds and re-checks every state now. Called right after a Worker move, so
        /// "catching up" shows in the same moment as the player's decision (AC2), not on the next tick.
        /// </summary>
        public void RefreshStates()
        {
            ReadSpeeds();
            CheckStates();
        }

        public int WaveIndex { get; }
        public int WaveNumber => WaveIndex + 1;

        /// <summary>Guests shown arriving (visual; each carries about 2 of the need units). See <see cref="GuestPlan"/>.</summary>
        public int GuestCount { get; }

        /// <summary>Length of the arrival phase (90 s by default).</summary>
        public double WaveTimer { get; }

        /// <summary>Wave time the simulation has been advanced to.</summary>
        public double Time { get; private set; }

        /// <summary>
        /// True once the arrival timer has run out and every unit has arrived. No more guests come; the queues
        /// still have to be served (OUR-41).
        /// </summary>
        public bool ArrivalsClosed
        {
            get
            {
                if (Time < WaveTimer - TimeEpsilon) return false;
                foreach (NeedQueue need in _needList)
                {
                    if (need.Arrived < need.Total) return false;
                }

                return true;
            }
        }

        /// <summary>
        /// OUR-41 / OUR-35: arrivals are closed and every unit has been served (no queue, no seated guest). The wave
        /// may end. Nobody is ever sent away, so a wave only finishes by serving everyone (WR3, WR4, AC3).
        /// </summary>
        public bool IsFinished
        {
            get
            {
                if (!ArrivalsClosed) return false;
                foreach (NeedQueue need in _needList)
                {
                    if (need.Seated > 0 || need.Waiting.Count > 0) return false;
                }

                return true;
            }
        }

        /// <summary>Units served so far over every need.</summary>
        public int TotalServed
        {
            get
            {
                int served = 0;
                foreach (NeedQueue need in _needList) served += need.Served;
                return served;
            }
        }

        /// <summary>Units in the whole wave over every need.</summary>
        public int TotalUnits
        {
            get
            {
                int total = 0;
                foreach (NeedQueue need in _needList) total += need.Total;
                return total;
            }
        }

        /// <summary>
        /// One seated unit of <paramref name="need"/> is done: it leaves its seat, <see cref="NeedStatus.Served"/>
        /// goes up, and the first unit in the queue sits down at once (first come, first served).
        /// <para><see cref="AdvanceTo"/> does this when a unit's progress reaches 1; tests may call it directly.</para>
        /// </summary>
        /// <returns>False when nobody of that need is seated.</returns>
        internal bool FinishService(NeedId need)
        {
            if (!_needs.TryGetValue(need, out NeedQueue queue) || !queue.FinishService()) return false;
            CheckStates();
            return true;
        }

        /// <summary>Needs that take part in this wave (the ones with a building on the island).</summary>
        public IEnumerable<NeedId> Needs
        {
            get
            {
                foreach (NeedQueue need in _needList) yield return need.Need;
            }
        }

        /// <summary>Arrival time of unit <paramref name="index"/> (0-based) of a need.</summary>
        public double GetArrivalTime(NeedId need, int index) => Get(need).ArrivalTimes[index];

        /// <summary>Units of a need in this wave (0 for a need that isn't in it).</summary>
        public int GetTotal(NeedId need) => _needs.TryGetValue(need, out NeedQueue queue) ? queue.Total : 0;

        /// <summary>Live numbers of one need. All zero for a need that isn't in this wave.</summary>
        public NeedStatus GetNeedStatus(NeedId need)
        {
            return _needs.TryGetValue(need, out NeedQueue queue)
                ? new NeedStatus(need, queue.Total, queue.Arrived, queue.Waiting.Count, queue.Seated, queue.Served)
                : new NeedStatus(need, 0, 0, 0, 0, 0);
        }

        /// <summary>Arrival times of the units waiting for a seat, oldest first.</summary>
        public IReadOnlyCollection<double> GetWaitingArrivalTimes(NeedId need) =>
            _needs.TryGetValue(need, out NeedQueue queue) ? queue.Waiting : (IReadOnlyCollection<double>)Array.Empty<double>();

        /// <summary>Advances the wave to <paramref name="waveTime"/> (seconds since WAVE_ACTIVE began).</summary>
        public void AdvanceTo(double waveTime)
        {
            if (waveTime <= Time) return;

            ReadSpeeds();

            // OUR-34 + OUR-46/47: jump from event to event (the next arrival or service end over every need), so the
            // result never depends on the step size. OUR-38/39: states are checked at every event and on a fixed
            // 0.25 s grid of wave time.
            while (true)
            {
                double next = Math.Min(waveTime, _nextStateCheck);
                foreach (NeedQueue need in _needList)
                {
                    next = Math.Min(next, need.NextEventTime(Time));
                }

                foreach (NeedQueue need in _needList) need.Progress(next - Time);
                Time = next;

                bool any = false;
                foreach (NeedQueue need in _needList) any |= need.ProcessDueEvents(Time);

                bool gridCheck = Time >= _nextStateCheck - TimeEpsilon;
                if (gridCheck) _nextStateCheck += StateCheckInterval;
                if (any || gridCheck) CheckStates();

                if (!any && !gridCheck && Time >= waveTime) break;
            }

            Time = waveTime;
        }

        private void ReadSpeeds()
        {
            foreach (NeedQueue need in _needList)
            {
                need.Speed = _serviceSpeedForNeed != null ? Math.Max(0, _serviceSpeedForNeed(need.Need)) : 0;
            }
        }

        /// <summary>OUR-38 / OUR-39: the state machine of Build doc §4.4, for every need at the current wave time.</summary>
        private void CheckStates()
        {
            foreach (NeedQueue need in _needList)
            {
                need.LogQueue(Time, _bottleneckHysteresis);
                if (need.State == NeedState.Bottleneck) need.BottleneckSeconds += Time - _lastStateCheck;

                // Follow the transitions until the state is stable (e.g. BOTTLENECK -> RECOVERING -> SATISFIED at the
                // last service), raising an event for each step.
                for (int step = 0; step < 4; step++)
                {
                    NeedState next = NextState(need);
                    if (next == need.State) break;
                    NeedState from = need.State;
                    need.State = next;
                    StateChanged?.Invoke(need.Need, from, next);
                }
            }

            _lastStateCheck = Time;
        }

        private NeedState NextState(NeedQueue need)
        {
            int waiting = need.Waiting.Count;
            bool arrivalsClosed = need.Arrived >= need.Total && Time >= WaveTimer - TimeEpsilon;
            bool done = arrivalsClosed && waiting == 0 && need.Seated == 0;

            // "Rastie >= hysteresis": the queue is longer now than it was hysteresis seconds ago.
            bool growing = waiting > need.WaitingAt(Time - _bottleneckHysteresis);

            // OUR-39: catching up = the building serves faster than guests arrive right now (seats x speed versus
            // demand / waveTimer while arrivals are open, 0 after). Reacts at once to a Worker move.
            double arrivalRate = need.Arrived < need.Total ? need.Total / WaveTimer : 0;
            bool catchingUp = need.Seats * need.Speed > arrivalRate + TimeEpsilon;

            switch (need.State)
            {
                case NeedState.Ok:
                    if (done) return NeedState.Satisfied;
                    return waiting > _bottleneckThreshold && growing ? NeedState.Bottleneck : NeedState.Ok;

                case NeedState.Bottleneck:
                    // Never straight to SATISFIED: it always passes RECOVERING (Build doc §4.4).
                    return catchingUp || arrivalsClosed ? NeedState.Recovering : NeedState.Bottleneck;

                case NeedState.Recovering:
                    if (done) return NeedState.Satisfied;
                    if (waiting <= _bottleneckThreshold) return NeedState.Ok;
                    return !catchingUp && growing ? NeedState.Bottleneck : NeedState.Recovering;

                default:
                    return need.State;
            }
        }

        private NeedQueue Get(NeedId need)
        {
            if (!_needs.TryGetValue(need, out NeedQueue queue))
            {
                throw new ArgumentException($"Need {need} is not part of wave {WaveNumber}.", nameof(need));
            }

            return queue;
        }

        private sealed class NeedQueue
        {
            public NeedQueue(NeedId need, int total, double waveTimer, int seats)
            {
                Need = need;
                Total = Math.Max(0, total);
                Seats = Math.Max(0, seats);
                ArrivalTimes = new double[Total];
                for (int k = 0; k < Total; k++)
                {
                    // Steady rate demand ÷ waveTimer: unit k arrives at (k + 1) × waveTimer ÷ demand.
                    ArrivalTimes[k] = (k + 1) * waveTimer / Total;
                }
            }

            public NeedId Need { get; }
            public int Total { get; }
            public int Seats { get; }

            /// <summary>OUR-38 / OUR-39: the need's state (Build doc §4.4).</summary>
            public NeedState State { get; set; } = NeedState.Ok;

            public double BottleneckSeconds { get; set; }

            /// <summary>Queue length after each change: (wave time, waiting), oldest first. Answers "queue 3 s ago".</summary>
            private readonly List<(double Time, int Waiting)> _queueLog = new List<(double, int)>();

            public void LogQueue(double now, double keepSeconds)
            {
                int waiting = Waiting.Count;
                int last = _queueLog.Count - 1;
                if (last >= 0 && _queueLog[last].Waiting == waiting) return;
                if (last >= 0 && _queueLog[last].Time >= now - TimeEpsilon) _queueLog[last] = (now, waiting);
                else _queueLog.Add((now, waiting));

                // Keep one entry older than the window, so WaitingAt still knows the value at its start.
                while (_queueLog.Count > 1 && _queueLog[1].Time < now - keepSeconds - 1) _queueLog.RemoveAt(0);
            }

            /// <summary>Queue length at wave time <paramref name="time"/> (0 before the first guest waited).</summary>
            public int WaitingAt(double time)
            {
                int waiting = 0;
                foreach ((double t, int w) in _queueLog)
                {
                    if (t > time + TimeEpsilon) break;
                    waiting = w;
                }

                return waiting;
            }
            public double[] ArrivalTimes { get; }
            public int Arrived { get; private set; }
            public int Seated => _remaining.Count;
            public int Served { get; private set; }

            /// <summary>Progress per second of each seated unit (OUR-46 / OUR-47).</summary>
            public double Speed { get; set; }

            /// <summary>FIFO of units waiting for a seat, as their arrival times.</summary>
            public Queue<double> Waiting { get; } = new Queue<double>();

            /// <summary>Work left (1 - progress) of each seated unit, in the order they sat down.</summary>
            private readonly List<double> _remaining = new List<double>();

            /// <summary>Wave time of this need's next arrival or service end (infinity when there is none).</summary>
            public double NextEventTime(double now)
            {
                double next = Arrived < Total ? ArrivalTimes[Arrived] : double.PositiveInfinity;
                if (Speed > 0 && _remaining.Count > 0)
                {
                    double least = double.PositiveInfinity;
                    foreach (double r in _remaining) least = Math.Min(least, r);
                    next = Math.Min(next, now + Math.Max(0, least) / Speed);
                }

                return next;
            }

            public void Progress(double seconds)
            {
                if (seconds <= 0 || Speed <= 0) return;
                double work = seconds * Speed;
                for (int i = 0; i < _remaining.Count; i++) _remaining[i] -= work;
            }

            /// <returns>True when something happened at <paramref name="now"/>.</returns>
            public bool ProcessDueEvents(double now)
            {
                bool any = false;

                // Service ends first, so a seat freed at the same moment as an arrival goes to the oldest in line.
                for (int i = 0; i < _remaining.Count;)
                {
                    if (Speed > 0 && _remaining[i] <= TimeEpsilon * Speed)
                    {
                        _remaining.RemoveAt(i);
                        Served++;
                        SeatNextInLine();
                        any = true;
                    }
                    else
                    {
                        i++;
                    }
                }

                while (Arrived < Total && ArrivalTimes[Arrived] <= now + TimeEpsilon)
                {
                    Arrive(ArrivalTimes[Arrived]);
                    any = true;
                }

                return any;
            }

            public bool FinishService()
            {
                if (_remaining.Count == 0) return false;
                _remaining.RemoveAt(0);
                Served++;
                SeatNextInLine();
                return true;
            }

            private void Arrive(double time)
            {
                Arrived++;
                if (_remaining.Count < Seats)
                {
                    _remaining.Add(1);
                }
                else
                {
                    Waiting.Enqueue(time);
                }
            }

            private void SeatNextInLine()
            {
                if (Waiting.Count > 0 && _remaining.Count < Seats)
                {
                    Waiting.Dequeue();
                    _remaining.Add(1);
                }
            }
        }
    }

    /// <summary>Live numbers of one need during a wave (read by the needs panel and building indicators).</summary>
    public readonly struct NeedStatus
    {
        public NeedStatus(NeedId need, int total, int arrived, int waiting, int seated, int served)
        {
            Need = need;
            Total = total;
            Arrived = arrived;
            Waiting = waiting;
            Seated = seated;
            Served = served;
        }

        public NeedId Need { get; }

        /// <summary>Units of this need in the whole wave.</summary>
        public int Total { get; }

        /// <summary>Units that have arrived so far.</summary>
        public int Arrived { get; }

        /// <summary>Units in the queue in front of the building.</summary>
        public int Waiting { get; }

        /// <summary>Units sitting in the building's seats.</summary>
        public int Seated { get; }

        /// <summary>
        /// Units fully served (OUR-46 / OUR-47). Each need's units are served in arrival order
        /// (first come, first served): the walking guests (GuestCrowdView) move on to their next building once
        /// <c>Served</c> has passed their unit.
        /// </summary>
        public int Served { get; }
    }
}
