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
    /// Service (seats emptying again) comes with OUR-46 / OUR-47, the drain after 90 s with OUR-41.
    /// </summary>
    public sealed class WaveSimulation
    {
        /// <summary>Tolerance for comparing accumulated frame times against the schedule.</summary>
        private const double TimeEpsilon = 1e-6;

        private readonly Dictionary<NeedId, NeedQueue> _needs = new Dictionary<NeedId, NeedQueue>();
        private readonly List<NeedQueue> _needList = new List<NeedQueue>();

        /// <param name="forecast">Demand for the needs on the island (already filtered by WR9).</param>
        /// <param name="waveTimer">Length of the arrival phase in seconds.</param>
        /// <param name="seatsForNeed">Seats of the building that serves a need.</param>
        public WaveSimulation(WaveForecast forecast, double waveTimer, Func<NeedId, int> seatsForNeed)
        {
            if (forecast == null) throw new ArgumentNullException(nameof(forecast));
            if (seatsForNeed == null) throw new ArgumentNullException(nameof(seatsForNeed));
            if (waveTimer <= 0) throw new ArgumentOutOfRangeException(nameof(waveTimer), "The arrival phase must be longer than 0 s.");

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

            // OUR-34: release every unit whose arrival time has come, in time order per need.
            // Needs don't interact yet; once service exists (OUR-46) arrivals and service ends must be merged
            // into one time-ordered sequence.
            foreach (NeedQueue need in _needList)
            {
                while (need.Arrived < need.Total && need.ArrivalTimes[need.Arrived] <= waveTime + TimeEpsilon)
                {
                    need.Arrive(need.ArrivalTimes[need.Arrived]);
                }
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
            public int Seated { get; private set; }
            public int Served { get; private set; }

            /// <summary>FIFO of units waiting for a seat, as their arrival times.</summary>
            public Queue<double> Waiting { get; } = new Queue<double>();

            public void Arrive(double time)
            {
                Arrived++;
                if (Seated < Seats)
                {
                    Seated++;
                }
                else
                {
                    Waiting.Enqueue(time);
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
        /// Units fully served. Stays 0 until service exists (OUR-46). Serve each need's units in arrival order
        /// (first come, first served): the walking guests (GuestCrowdView) move on to their next building once
        /// <c>Served</c> has passed their unit.
        /// </summary>
        public int Served { get; }
    }
}
