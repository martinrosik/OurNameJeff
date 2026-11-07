using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using UnityEditor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Guards for the shipped config asset (what the economist tunes) and for the simulation model itself.
    /// Build doc §4.12: before a playtest, the balance check must show no WR7 or WR8 violation.
    /// </summary>
    public class PrototypeBalanceTests
    {
        private static GameConfig LoadAsset()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(PrototypeConfigDefaults.AssetPath);
            Assert.IsNotNull(config, $"Missing {PrototypeConfigDefaults.AssetPath}. Use Resort Empire/Config/Create Prototype Config.");
            return config;
        }

        [Test]
        public void ConfigAssetPassesValidation()
        {
            List<string> errors = GameConfigValidator.Validate(LoadAsset());

            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void ConfigAssetHasNoWr7OrWr8Violations()
        {
            WaveBalanceReport report = WaveBalanceSimulator.BuildReport(LoadAsset());

            Assert.IsFalse(report.HasViolations, "Balance broke a hard rule:\n" + report.ToTable());
        }

        [Test]
        public void ConfigAssetDebutWavesAreSoft()
        {
            // §4.8 growth rules 2 and 3 / AC7c: a new need starts well below the established ones,
            // and the established ones don't grow in the debut wave.
            GameConfig config = LoadAsset();
            foreach (BuildingDefinition unlock in config.buildings.Where(b => !b.unlockedAtStart))
            {
                int debut = unlock.unlockAfterWave;
                if (debut <= 0 || debut >= config.waves.Count) continue;

                WaveDefinition wave = config.waves[debut];
                WaveDefinition before = config.waves[debut - 1];
                int newDemand = wave.GetDemand(unlock.need);
                foreach (BuildingDefinition established in WaveBalanceSimulator.OwnedBeforeWave(config, debut - 1).Select(config.GetBuilding))
                {
                    int demand = wave.GetDemand(established.need);
                    Assert.Less(newDemand, demand, $"Wave {debut + 1}: debut need {unlock.need} should start below {established.need}.");
                    Assert.AreEqual(before.GetDemand(established.need), demand, $"Wave {debut + 1}: {established.need} must hold while {unlock.need} debuts.");
                }

                Assert.IsFalse(wave.rush != null && wave.rush.enabled, $"Wave {debut + 1}: no Sudden rush in a debut wave.");
            }
        }

        [Test]
        public void SimulationReproducesTheBuildDocNoInterventionTable()
        {
            // Build doc §4.8 "Čo ktorá vlna testuje": result of a passive player with the doc's numbers,
            // computed by the economist's wave_sim.py. Matching it means our model is the same model.
            // Uses the code baseline, not the asset, so tuning the asset doesn't break this test.
            GameConfig config = PrototypeConfigDefaults.Create();
            try
            {
                WaveBalanceReport report = WaveBalanceSimulator.BuildReport(config);
                StarRating[] expected =
                {
                    StarRating.Perfect, StarRating.FourStars, StarRating.FourStars, StarRating.Perfect,
                    StarRating.FourStars, StarRating.FourStars, StarRating.FiveStars, StarRating.ThreeStars,
                };

                CollectionAssert.AreEqual(expected, report.Rows.Select(r => r.Passive.Result.Rating).ToArray(), report.ToTable());
                Assert.That(report.Rows.All(r => r.Prepared.Result.Rating >= StarRating.FiveStars), "Fully upgraded resort reaches 5★ everywhere.\n" + report.ToTable());

                // "vlny 1–3 ~1 900 Coins, vlny 4–6 ~3 900 Coins" for a passive player.
                Assert.AreEqual(1900, report.PassiveCoins(1, 3), 100);
                Assert.AreEqual(3900, report.PassiveCoins(4, 6), 150);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
