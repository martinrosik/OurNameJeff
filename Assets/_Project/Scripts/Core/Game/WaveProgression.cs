using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave difficulty: every wave must be harder than the one played before it (Build doc §4.8 "Progresia vĺn").
    /// <para>
    /// A wave takes its numbers from the wave table, limited to needs with a building on the island (WR9). When those
    /// numbers are not bigger in total than the previous wave's, the previous wave grows by
    /// <see cref="WaveSettings.growthIfNotHarder"/> instead. Today that happens in waves 4 and 7: they debut Relax and
    /// Ocean View, whose buildings can't be built yet, so the needs on the island would only hold.
    /// </para>
    /// </summary>
    public static class WaveProgression
    {
        // A float growth like 0.1 makes 10 × 1.1 = 11.000000000000002, which must still round up to 11, not 12.
        private const double RoundingTolerance = 1e-6;

        /// <summary>The demand of wave <paramref name="waveIndex"/> as the player plays it.</summary>
        public static WaveForecast GetForecast(GameConfig config, int waveIndex)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (waveIndex < 0 || waveIndex >= config.waves.Count) throw new ArgumentOutOfRangeException(nameof(waveIndex));

            WaveForecast forecast = GetTableForecast(config, 0);
            for (int i = 1; i <= waveIndex; i++)
            {
                WaveForecast tableForecast = GetTableForecast(config, i);
                forecast = GetTotalDemand(tableForecast) > GetTotalDemand(forecast)
                    ? tableForecast
                    : GrowForecast(forecast, i, config.wave.growthIfNotHarder);
            }

            return forecast;
        }

        /// <summary>Need units of a wave in total, over every need on the island.</summary>
        public static int GetTotalDemand(WaveForecast forecast)
        {
            int total = 0;
            foreach (NeedDemand entry in forecast.Demand) total += entry.Units;
            return total;
        }

        private static WaveForecast GetTableForecast(GameConfig config, int waveIndex)
        {
            WaveDefinition wave = config.waves[waveIndex];
            var demand = new List<NeedDemand>();
            foreach (NeedDefinition need in config.needs)
            {
                if (config.GetBuildingForNeed(need.need) != null)
                {
                    demand.Add(new NeedDemand(need.need, wave.GetDemand(need.need)));
                }
            }

            return new WaveForecast(waveIndex, wave.guests, demand);
        }

        private static WaveForecast GrowForecast(WaveForecast previous, int waveIndex, float growth)
        {
            var demand = new List<NeedDemand>();
            foreach (NeedDemand entry in previous.Demand)
            {
                demand.Add(new NeedDemand(entry.Need, GrowAmount(entry.Units, growth)));
            }

            return new WaveForecast(waveIndex, GrowAmount(previous.Guests, growth), demand);
        }

        // Rounds up, so any amount above zero grows by at least one.
        private static int GrowAmount(int amount, float growth) =>
            (int)Math.Ceiling(amount * (1.0 + growth) - RoundingTolerance);
    }
}
