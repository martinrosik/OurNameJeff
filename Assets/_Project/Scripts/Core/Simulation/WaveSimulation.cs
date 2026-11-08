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
    /// </summary>
    public sealed class WaveSimulation
    {
        /// <summary>Tolerance for comparing accumulated frame times against the schedule.</summary>
        private const double TimeEpsilon = 1e-6;

        private readonly Dictionary<NeedId, NeedQueue> _needs = new Dictionary<NeedId, NeedQueue>();
        private readonly List<NeedQueue> _needList = new List<NeedQueue>();
        private readonly Func<NeedId, double> _serviceSpeedForNeed;

        /// <param name="forecast">Demand for the needs on the island (already filtered by WR9).</param>
        /// <param name="waveTimer">Length of the arrival phase in seconds.</param>
        /// <param name="seatsForNeed">Seats of the building that serves a need.</param>
        /// <param name="serviceSpeedForNeed">
        /// OUR-46 / OUR-47: progress per second of one seated unit of a need (utilization ÷ serviceTime). Read on every
        /// step. Null: nobody is served automatically (only <see cref="FinishService"/>).
        /// </param>
        public WaveSimulation(
            WaveForecast forecast, double waveTimer, Func<NeedId, int> seatsForNeed, Func<NeedId, double> serviceSpeedForNeed = null)
        {
            if (forecast == null) throw new ArgumentNullException(nameof(forecast));
            if (seatsForNeed == null) throw new ArgumentNullException(nameof(seatsForNeed));
            if (waveTimer <= 0) throw new ArgumentOutOfRangeException(nameof(waveTimer), "The arrival phase must be longer than 0 s.");

            _serviceSpeedForNeed = serviceSpeedForNeed;
            WaveIndex = forecast.WaveIndex;
            GuestCount = forecast.Guests;
            WaveTimer = waveTimer;
            foreach (NeedDemand demand in forecast.Demand)
            {
                var queue = new NeedQueue(demand.Need, demand.Units, waveTimer, seatsForNeed(demand.Need));
                _needs[demand.Need] = queue;
                _needList.Add(queue);
            }
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
        internal bool FinishService(NeedId need) => _needs.TryGetValue(need, out NeedQueue queue) && queue.FinishService();

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

            foreach (NeedQueue need in _needList)
            {
                need.Speed = _serviceSpeedForNeed != null ? Math.Max(0, _serviceSpeedForNeed(need.Need)) : 0;
            }

            // OUR-34 + OUR-46/47: jump from event to event (the next arrival or service end over every need), so the
            // result never depends on the step size.
            while (true)
            {
                double next = waveTime;
                foreach (NeedQueue need in _needList)
                {
                    next = Math.Min(next, need.NextEventTime(Time));
                }

                foreach (NeedQueue need in _needList) need.Progress(next - Time);
                Time = next;

                bool any = false;
                foreach (NeedQueue need in _needList) any |= need.ProcessDueEvents(Time);
                if (!any && Time >= waveTime) break;
            }

            Time = waveTime;
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
