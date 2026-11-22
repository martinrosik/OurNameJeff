using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave Complete breakdown "What went well / Needs improvement" (UI doc S4, Build doc AC6), computed once at the end
    /// of the wave. Each need is scored like Wave Satisfaction, but only from its own guests. A need that reaches
    /// <see cref="WaveBreakdownSettings.wentGreatSatisfaction"/> went great. The rest need a Worker when their building
    /// served without one for most of the wave, otherwise more capacity.
    /// </summary>
    public sealed class WaveBreakdown
    {
        public static readonly WaveBreakdown Empty = new WaveBreakdown(Array.Empty<NeedId>(), Array.Empty<NeedId>(), Array.Empty<NeedId>());

        public WaveBreakdown(IReadOnlyList<NeedId> wentGreat, IReadOnlyList<NeedId> needsWorker, IReadOnlyList<NeedId> needsCapacity)
        {
            WentGreat = wentGreat ?? Array.Empty<NeedId>();
            NeedsWorker = needsWorker ?? Array.Empty<NeedId>();
            NeedsCapacity = needsCapacity ?? Array.Empty<NeedId>();
        }

        /// <summary>Needs whose guests were happy enough, in the wave's need order.</summary>
        public IReadOnlyList<NeedId> WentGreat { get; }

        /// <summary>Needs below the bar whose building served without a Worker for most of the wave.</summary>
        public IReadOnlyList<NeedId> NeedsWorker { get; }

        /// <summary>Needs below the bar that had a Worker most of the time: their building needs more capacity.</summary>
        public IReadOnlyList<NeedId> NeedsCapacity { get; }

        /// <summary>The breakdown of a finished <paramref name="wave"/> that lasted <paramref name="duration"/> seconds.</summary>
        public static WaveBreakdown Create(WaveSimulation wave, double duration, SatisfactionSettings satisfaction, WaveBreakdownSettings settings)
        {
            if (wave == null || satisfaction == null || settings == null) return Empty;

            var wentGreat = new List<NeedId>();
            var needsWorker = new List<NeedId>();
            var needsCapacity = new List<NeedId>();
            foreach (NeedId need in wave.Needs)
            {
                if (GetNeedSatisfaction(wave, need, satisfaction) + 1e-6 >= settings.wentGreatSatisfaction) wentGreat.Add(need);
                else if (duration > 0 && wave.GetSlowServiceSeconds(need) / duration > settings.noWorkerShare) needsWorker.Add(need);
                else needsCapacity.Add(need);
            }

            return new WaveBreakdown(wentGreat, needsWorker, needsCapacity);
        }

        /// <summary>One need's satisfaction in %: the Wave Satisfaction formula over only that need's guests.</summary>
        public static double GetNeedSatisfaction(WaveSimulation wave, NeedId need, SatisfactionSettings satisfaction)
        {
            var delays = new List<(NeedId, double)>();
            foreach (double delay in wave.GetDelays(need)) delays.Add((need, delay));
            return WaveSatisfaction.Calculate(delays, satisfaction);
        }
    }
}
