using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// One need being served at its building during a wave: arrival schedule, queue, seats
    /// and the satisfaction bookkeeping (Build doc §4.4 "miesta pri pulte").
    /// A unit arrives, takes a free seat or waits in the queue, then is served. Service is progress
    /// (0 → 1), not a fixed timer, so a speed change applies at once to everyone already seated.
    /// </summary>
    internal sealed class NeedService
    {
        private const double Epsilon = 1e-9;

        private readonly List<double> _schedule = new List<double>();
        private readonly Queue<double> _queue = new Queue<double>();
        private readonly List<double> _seatProgress = new List<double>();
        private readonly List<QueueSample> _queueHistory = new List<QueueSample>();
        private int _nextArrival;

        public NeedService(NeedId need, BuildingRuntime building, float weight)
        {
            Need = need;
            Building = building;
            Weight = weight;
        }

        public NeedId Need { get; }
        public BuildingRuntime Building { get; }
        public float Weight { get; }
        public NeedState State { get; set; }

        /// <summary>Steady arrival rate during the arrival phase: demand ÷ wave_timer.</summary>
        public double BaseArrivalRate { get; set; }

        public double RushStart { get; private set; }
        public double RushEnd { get; private set; }
        public double RushRate { get; private set; }

        public int Total => _schedule.Count;
        public int Arrived => _nextArrival;
        public int Served { get; private set; }
        public int QueueLength => _queue.Count;
        public int Seated => _seatProgress.Count;
        public bool AllArrived => _nextArrival >= _schedule.Count;
        public bool IsEmpty => _queue.Count == 0 && _seatProgress.Count == 0;
        public double NextArrivalTime => AllArrived ? double.PositiveInfinity : _schedule[_nextArrival];

        public double ContributionSum { get; private set; }
        public int UnitsSeated { get; private set; }
        public double WaitSum { get; private set; }
        public double MaxWait { get; private set; }
        public int UnitsWithinGrace { get; private set; }
        public double BottleneckSeconds { get; set; }

        /// <summary>
        /// Adds <paramref name="count"/> units arriving evenly over <paramref name="duration"/> seconds from
        /// <paramref name="start"/>. Unit k arrives at start + (k + 1) × duration ÷ count, i.e. the arrived
        /// count is floor(rate × t), as in the Build doc's demand_arrived.
        /// </summary>
        public void ScheduleEvenly(int count, double start, double duration)
        {
            if (count <= 0)
            {
                return;
            }

            for (int k = 0; k < count; k++)
            {
                _schedule.Add(start + (k + 1) * duration / count);
            }

            _schedule.Sort(_nextArrival, _schedule.Count - _nextArrival, Comparer<double>.Default);
        }

        public void AddRush(double start, double duration, int units)
        {
            RushStart = start;
            RushEnd = start + duration;
            RushRate = duration > 0 ? units / duration : 0;
            ScheduleEvenly(units, start, duration);
        }

        public double NextCompletionTime(double now, double progressPerSecond)
        {
            if (_seatProgress.Count == 0 || progressPerSecond <= 0)
            {
                return double.PositiveInfinity;
            }

            double furthest = 0;
            foreach (double progress in _seatProgress)
            {
                furthest = Math.Max(furthest, progress);
            }

            return now + (1.0 - furthest) / progressPerSecond;
        }

        public void AdvanceService(double progress)
        {
            for (int i = 0; i < _seatProgress.Count; i++)
            {
                _seatProgress[i] += progress;
            }
        }

        public void CompleteFinished()
        {
            for (int i = _seatProgress.Count - 1; i >= 0; i--)
            {
                if (_seatProgress[i] >= 1.0 - Epsilon)
                {
                    _seatProgress.RemoveAt(i);
                    Served++;
                }
            }
        }

        public void ArriveUntil(double now)
        {
            while (!AllArrived && _schedule[_nextArrival] <= now + Epsilon)
            {
                _queue.Enqueue(_schedule[_nextArrival]);
                _nextArrival++;
            }
        }

        /// <summary>Fills free seats from the queue. Waiting ends when a unit sits down.</summary>
        public void SeatFromQueue(double now, int seats, SatisfactionSettings satisfaction)
        {
            while (_seatProgress.Count < seats && _queue.Count > 0)
            {
                double arrival = _queue.Dequeue();
                double wait = Math.Max(0, now - arrival);

                ContributionSum += SatisfactionMath.UnitContribution(wait, satisfaction);
                UnitsSeated++;
                WaitSum += wait;
                MaxWait = Math.Max(MaxWait, wait);
                if (wait <= satisfaction.patienceGrace + Epsilon)
                {
                    UnitsWithinGrace++;
                }

                _seatProgress.Add(0);
            }
        }

        /// <summary>Remembers queue length changes so we can ask "did it grow over the last N seconds?".</summary>
        public void RecordQueue(double now, double window)
        {
            int length = _queue.Count;
            if (_queueHistory.Count == 0 || _queueHistory[_queueHistory.Count - 1].Length != length)
            {
                _queueHistory.Add(new QueueSample(now, length));
            }

            // Keep one sample at or before the window start so QueueAt() stays correct.
            double windowStart = now - window;
            while (_queueHistory.Count >= 2 && _queueHistory[1].Time <= windowStart)
            {
                _queueHistory.RemoveAt(0);
            }
        }

        public int QueueAt(double time)
        {
            for (int i = _queueHistory.Count - 1; i >= 0; i--)
            {
                if (_queueHistory[i].Time <= time + Epsilon)
                {
                    return _queueHistory[i].Length;
                }
            }

            return 0;
        }

        private readonly struct QueueSample
        {
            public QueueSample(double time, int length)
            {
                Time = time;
                Length = length;
            }

            public double Time { get; }
            public int Length { get; }
        }
    }
}
