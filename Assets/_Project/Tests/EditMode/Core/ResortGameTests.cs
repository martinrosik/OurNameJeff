using System.Collections.Generic;
using NUnit.Framework;
using ResortEmpire.Core;

namespace ResortEmpire.Tests
{
    /// <summary>Core loop, Energy, pause, upgrades, unlocks and rewards (Build doc §4.3, §4.6, §4.8, §4.10).</summary>
    public class ResortGameTests : CoreTestBase
    {
        [Test]
        public void CoreLoopFollowsTheBuildDocStateMachine()
        {
            ResortGame game = NewGame();
            var phases = new List<GamePhase>();
            game.PhaseChanged += (from, to) => phases.Add(to);

            Assert.AreEqual(GamePhase.AwaitingWave, game.Phase);
            Assert.IsTrue(game.StartWave());
            Assert.AreEqual(GamePhase.WaveIncoming, game.Phase);
            PlayWaveToEnd(game);
            Assert.IsTrue(game.ClaimRewards());
            Assert.IsTrue(game.FinishUpgrades());

            CollectionAssert.AreEqual(
                new[]
                {
                    GamePhase.WaveIncoming, GamePhase.WaveActive, GamePhase.WaveResolving,
                    GamePhase.WaveComplete, GamePhase.Upgrade, GamePhase.AwaitingWave,
                },
                phases);
            Assert.AreEqual(1, game.CompletedWaves);
        }

        [Test]
        public void ActionsOutOfOrderAreRefused()
        {
            ResortGame game = NewGame();

            Assert.IsFalse(game.ClaimRewards());
            Assert.IsFalse(game.FinishUpgrades());
            game.StartWave();
            Assert.IsFalse(game.StartWave(), "Only one wave at a time.");
        }

        [Test]
        public void PerfectFirstWavePaysCoinsByFormula()
        {
            // Wave 1: 8 Rooms + 8 Drinks + 6 Food = 22 units, Perfect → 22 × 16 × 1.3 = 457.6 → 458.
            ResortGame game = NewGame();

            WaveResult result = PlayWaveToEnd(game);

            Assert.AreEqual(StarRating.Perfect, result.Rating);
            Assert.AreEqual(22, result.UnitsServed);
            Assert.AreEqual(458, result.Coins);
            Assert.AreEqual(0, game.Coins, "Rewards are paid on Claim, not before.");

            game.ClaimRewards();
            Assert.AreEqual(458, game.Coins);
            Assert.AreEqual(Config.energy.dropByRating[StarRating.Perfect.ToIndex()], game.Energy);
        }

        [Test]
        public void AC4c_SpeedUpHalvesServiceTimeOncePerBuildingPerWave()
        {
            Config.energy.startingEnergy = 3;
            UseSingleWave(rooms: 2, drinks: 2, food: 20);
            ResortGame game = NewGame();
            var ended = new List<BuildingId>();
            game.BoostEnded += building => ended.Add(building);
            StartWaveAndArrive(game);
            game.Tick(20);

            Assert.AreEqual(BoostResult.Boosted, game.BoostBuilding(BuildingId.Restaurant));
            Assert.AreEqual(0, game.Energy);
            Assert.AreEqual(Config.wave.utilizationEmpty * 2, game.GetServiceSpeed(BuildingId.Restaurant), 1e-9);
            Assert.IsFalse(game.CanOfferBoost(BuildingId.Restaurant), "Speed Up disappears for the rest of the wave.");
            Assert.AreEqual(BoostResult.AlreadyBoostedThisWave, game.BoostBuilding(BuildingId.Restaurant));

            game.Tick(Config.energy.boostDuration + 0.1);
            Assert.AreEqual(Config.wave.utilizationEmpty, game.GetServiceSpeed(BuildingId.Restaurant), 1e-9);
            CollectionAssert.AreEqual(new[] { BuildingId.Restaurant }, ended);
        }

        [Test]
        public void HR1_NoEnergyNeverBlocksAnything()
        {
            UseSingleWave(rooms: 2, drinks: 2, food: 20);
            ResortGame game = NewGame();
            StartWaveAndArrive(game);

            Assert.AreEqual(BoostResult.NotEnoughEnergy, game.BoostBuilding(BuildingId.Restaurant));
            Assert.AreEqual(0, game.Energy);
            Assert.IsTrue(game.CanOfferBoost(BuildingId.Restaurant), "The button stays and offers the rewarded video instead.");
            Assert.AreEqual(MoveWorkerResult.Moved, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant), "Moving Workers is always free (WR5).");

            game.GrantRewardedVideoEnergy();
            Assert.AreEqual(BoostResult.Boosted, game.BoostBuilding(BuildingId.Restaurant));
        }

        [Test]
        public void SpeedUpOnlyExistsDuringTheWave()
        {
            Config.energy.startingEnergy = 10;
            ResortGame game = NewGame();

            Assert.AreEqual(BoostResult.NotAllowedNow, game.BoostBuilding(BuildingId.Restaurant));
            Assert.AreEqual(10, game.Energy);
        }

        [Test]
        public void AC4d_WorkersPlacedBeforeTheWaveStayThereAfterwards()
        {
            ResortGame game = NewGame();

            Assert.AreEqual(MoveWorkerResult.Moved, game.MoveWorker(BuildingId.BeachBar, BuildingId.Restaurant));
            StartWaveAndArrive(game);
            Assert.IsTrue(game.GetBuilding(BuildingId.Restaurant).HasWorker);
            PlayWaveToEnd(game);
            game.ClaimRewards();
            game.FinishUpgrades();

            Assert.IsTrue(game.GetBuilding(BuildingId.Restaurant).HasWorker);
            Assert.IsFalse(game.GetBuilding(BuildingId.BeachBar).HasWorker);
        }

        [Test]
        public void WorkersCanOnlyGoToAnEmptyOwnedBuilding()
        {
            ResortGame game = NewGame();

            Assert.AreEqual(MoveWorkerResult.TargetFull, game.MoveWorker(BuildingId.Reception, BuildingId.BeachBar));
            Assert.AreEqual(MoveWorkerResult.NoWorkerAtSource, game.MoveWorker(BuildingId.Restaurant, BuildingId.Reception));
            Assert.AreEqual(MoveWorkerResult.BuildingNotOwned, game.MoveWorker(BuildingId.Reception, BuildingId.Pool));
            Assert.AreEqual(MoveWorkerResult.SameBuilding, game.MoveWorker(BuildingId.Reception, BuildingId.Reception));
            Assert.AreEqual(2, game.WorkerCount, "Always one fewer Worker than owned buildings.");
        }

        [Test]
        public void AC5_PauseFreezesEverythingUntilResume()
        {
            UseSingleWave(rooms: 6, drinks: 6, food: 20);
            ResortGame game = NewGame();
            StartWaveAndArrive(game);
            game.Tick(40);
            double time = WaveTime(game);
            NeedStatus food = game.GetNeedStatus(NeedId.Food);

            game.Pause();
            game.Tick(10_000);

            Assert.AreEqual(GamePhase.WavePaused, game.Phase);
            Assert.AreEqual(time, WaveTime(game));
            Assert.AreEqual(food.Served, game.GetNeedStatus(NeedId.Food).Served);
            Assert.AreEqual(food.Waiting, game.GetNeedStatus(NeedId.Food).Waiting);
            Assert.AreEqual(MoveWorkerResult.NotAllowedNow, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));

            game.Resume();
            Assert.AreEqual(GamePhase.WaveActive, game.Phase);
            game.Tick(1);
            Assert.AreEqual(time + 1, WaveTime(game), 1e-9);
        }

        [Test]
        public void PauseDuringBoatArrivalResumesTheBoatArrival()
        {
            ResortGame game = NewGame();
            game.StartWave();

            game.Pause();
            game.Resume();

            Assert.AreEqual(GamePhase.WaveIncoming, game.Phase);
        }

        [Test]
        public void WR6_NoUpgradesOrUnlocksDuringTheWave()
        {
            ResortGame game = NewGame();
            game.DebugAddCoins(10_000);
            game.DebugSetCompletedWaves(3);
            StartWaveAndArrive(game);

            Assert.AreEqual(UpgradeResult.NotAllowedNow, game.UpgradeBuilding(BuildingId.Restaurant));
            Assert.AreEqual(UnlockResult.NotAllowedNow, game.UnlockBuilding(BuildingId.Pool));
            Assert.AreEqual(1, game.GetBuilding(BuildingId.Restaurant).Level);
        }

        [Test]
        public void UpgradeAddsSeatsCostsCoinsAndAddsResortValue()
        {
            ResortGame game = NewGame();
            int resortValue = game.ResortValue;

            Assert.AreEqual(UpgradeResult.NotEnoughCoins, game.UpgradeBuilding(BuildingId.Restaurant));
            game.DebugAddCoins(1700);

            Assert.AreEqual(500, game.GetUpgradeCost(BuildingId.Restaurant));
            Assert.AreEqual(UpgradeResult.Upgraded, game.UpgradeBuilding(BuildingId.Restaurant));
            Assert.AreEqual(5, game.GetBuilding(BuildingId.Restaurant).Seats);
            Assert.AreEqual(UpgradeResult.Upgraded, game.UpgradeBuilding(BuildingId.Restaurant));
            Assert.AreEqual(7, game.GetBuilding(BuildingId.Restaurant).Seats);
            Assert.AreEqual(0, game.Coins);
            Assert.AreEqual(resortValue + 20 + 40, game.ResortValue);
            Assert.AreEqual(UpgradeResult.AlreadyMaxLevel, game.UpgradeBuilding(BuildingId.Restaurant));
            Assert.IsNull(game.GetUpgradeCost(BuildingId.Restaurant));
            Assert.AreEqual(Config.wave.utilizationEmpty, game.GetServiceSpeed(BuildingId.Restaurant), 1e-9, "Upgrades never change speed.");
        }

        [Test]
        public void AC7a_PoolUnlockGateThenCoinsThenNeedAndWorker()
        {
            ResortGame game = NewGame();

            game.DebugSetCompletedWaves(2);
            Assert.AreEqual(UnlockStatus.Locked, game.GetUnlockStatus(BuildingId.Pool));
            Assert.AreEqual(UnlockResult.GateNotMet, game.UnlockBuilding(BuildingId.Pool));

            game.DebugSetCompletedWaves(3);
            Assert.AreEqual(UnlockStatus.NotAffordable, game.GetUnlockStatus(BuildingId.Pool));
            CollectionAssert.AreEqual(new[] { BuildingId.Pool }, game.GetAvailableUnlocks());

            game.DebugAddCoins(1200);
            Assert.AreEqual(UnlockStatus.Available, game.GetUnlockStatus(BuildingId.Pool));
            int resortValue = game.ResortValue;
            Assert.AreEqual(UnlockResult.Unlocked, game.UnlockBuilding(BuildingId.Pool));

            Assert.AreEqual(0, game.Coins);
            Assert.AreEqual(3, game.WorkerCount, "The Worker comes with the building.");
            Assert.IsTrue(game.GetBuilding(BuildingId.Pool).HasWorker, "…and stands on the new building.");
            Assert.AreEqual(8, game.GetNextWaveForecast().GetDemand(NeedId.Relax));
            Assert.Greater(game.ResortValue, resortValue);
            Assert.AreEqual(UnlockResult.AlreadyUnlocked, game.UnlockBuilding(BuildingId.Pool));
        }

        [Test]
        public void AC7b_BungalowsUseTheSameUnlockFlowASecondTime()
        {
            ResortGame game = NewGame();
            game.DebugSetCompletedWaves(6);
            game.DebugAddCoins(1200 + 2500);

            Assert.AreEqual(UnlockResult.Unlocked, game.UnlockBuilding(BuildingId.Pool));
            Assert.AreEqual(UnlockResult.Unlocked, game.UnlockBuilding(BuildingId.Bungalows));

            Assert.AreEqual(4, game.WorkerCount);
            Assert.AreEqual(10, game.GetNextWaveForecast().GetDemand(NeedId.OceanView));
        }

        [Test]
        public void ResortValueNeverDrops()
        {
            ResortGame game = NewGame();
            int lowest = game.ResortValue;
            game.CurrenciesChanged += () =>
            {
                Assert.GreaterOrEqual(game.ResortValue, lowest);
                lowest = game.ResortValue;
            };

            for (int wave = 0; wave < 3; wave++)
            {
                PlayWaveToEnd(game, step: 0.5);
                game.ClaimRewards();
                game.UpgradeBuilding(BuildingId.Restaurant);
                game.FinishUpgrades();
            }

            game.UnlockBuilding(BuildingId.Pool);
            Assert.Greater(game.ResortValue, Config.economy.startingResortValue);
        }

        [Test]
        public void NoWaveAfterTheLastOne()
        {
            ResortGame game = NewGame();
            game.DebugSetCompletedWaves(Config.waves.Count);

            Assert.IsFalse(game.HasNextWave);
            Assert.IsNull(game.GetNextWaveForecast());
            Assert.IsFalse(game.StartWave());
        }
    }
}
