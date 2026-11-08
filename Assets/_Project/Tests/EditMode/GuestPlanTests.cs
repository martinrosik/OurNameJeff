using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// The wave's need units shared out among its guests: every unit is visited exactly once, so the guests match
    /// the forecast (wave 1: 12 guests, Rooms 8, Drinks 8, Food 6).
    /// </summary>
    public class GuestPlanTests
    {
        private GameConfig _config;

        [SetUp]
        public void CreateConfig() => _config = PrototypeConfigDefaults.Create();

        [TearDown]
        public void DestroyConfig() => UnityEngine.Object.DestroyImmediate(_config);

        private GuestPlan PlanFor(int waveIndex)
        {
            var game = new ResortGame(_config);
            WaveForecast forecast = game.GetForecast(waveIndex);
            var wave = new WaveSimulation(forecast, _config.wave.waveTimer, _ => 3);
            return GuestPlan.Build(wave);
        }

        [Test]
        public void VisitsMatchTheForecastForEveryWave()
        {
            var game = new ResortGame(_config);
            for (int i = 0; i < _config.waves.Count; i++)
            {
                GuestPlan plan = PlanFor(i);
                WaveForecast forecast = game.GetForecast(i);
                foreach (NeedDemand demand in forecast.Demand)
                {
                    Assert.AreEqual(demand.Units, plan.CountVisits(demand.Need), $"Wave {i + 1} {demand.Need}");
                }

                Assert.AreEqual(0, plan.CountVisits(NeedId.Relax), "No Pool: no Relax visits (WR9).");
                Assert.AreEqual(forecast.Guests, plan.Guests.Count, $"Wave {i + 1} has exactly the guests the game announces.");
            }
        }

        [Test]
        public void EveryUnitIsVisitedExactlyOnce()
        {
            GuestPlan plan = PlanFor(0);

            var visits = plan.Guests.SelectMany(g => g.Visits).Select(v => (v.Need, v.Unit)).ToList();

            Assert.AreEqual(22, visits.Count);
            Assert.AreEqual(visits.Count, visits.Distinct().Count());
        }

        [Test]
        public void GuestsVisitSeveralBuildingsButEachNeedOnce()
        {
            GuestPlan plan = PlanFor(0);

            Assert.That(plan.Guests.Any(g => g.Count > 1), "Guests carry more than one need.");
            foreach (PlannedGuest guest in plan.Guests)
            {
                Assert.AreEqual(guest.Count, guest.Visits.Select(v => v.Need).Distinct().Count(), "A guest never needs the same thing twice.");
                Assert.LessOrEqual(guest.Count, 2, "Wave 1: 22 units over 12 guests is at most 2 each.");
                CollectionAssert.IsOrdered(guest.Visits.Select(v => v.Time), "Visits follow the Core's release order.");
            }
        }

        [Test]
        public void FirstGuestGoesToTheReceptionThenTheBeachBar()
        {
            GuestPlan plan = PlanFor(0);

            GuestVisit[] first = plan.Guests[0].Visits.ToArray();
            Assert.AreEqual(NeedId.Rooms, first[0].Need);
            Assert.AreEqual(NeedId.Drinks, first[1].Need);
            Assert.AreEqual(11.25, first[0].Time, 1e-9);
        }
    }
}
