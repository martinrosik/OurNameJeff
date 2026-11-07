using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Wave behaviour from the Build doc's acceptance criteria (§4.10) and hard rules (§4.8).
    /// Default layout: Reception and Beach Bar have Workers, Restaurant (Food) is empty.
    /// One seat serves a unit in 12 s with a Worker and ~36 s without.
    /// </summary>
    public class WaveRulesTests : CoreTestBase
    {
        [Test]
        public void AC1_GrowingQueueAboveThresholdBecomesBottleneck()
        {
            UseSingleWave(rooms: 4, drinks: 4, food: 20);
            ResortGame game = NewGame();
            var entered = new List<(NeedId need, int waiting)>();
            game.NeedStateChanged += (need, from, to) =>
            {
                if (to == NeedState.Bottleneck) entered.Add((need, game.GetNeedStatus(need).Waiting));
            };

            PlayWaveToEnd(game);

            Assert.That(entered.Select(e => e.need), Has.Member(NeedId.Food), "Food on the empty Restaurant should bottleneck.");
            Assert.That(entered.Select(e => e.need), Has.No.Member(NeedId.Rooms), "4 Rooms on a staffed Reception never queue.");
            Assert.That(entered.All(e => e.waiting > Config.wave.bottleneckThreshold), "Warning must only show above the queue threshold.");
        }

        [Test]
        public void AC1b_UnitSeatedRightAwayCountsFullEvenIfServiceIsSlow()
        {
            UseSingleWave(rooms: 0, drinks: 0, food: 1);
            ResortGame game = NewGame();

            WaveResult result = PlayWaveToEnd(game);

            NeedWaveStats food = result.Needs.Single(n => n.Need == NeedId.Food);
            Assert.AreEqual(0, food.AverageWait, 1e-9);
            Assert.AreEqual(1.0, food.AverageContribution, 1e-9);
            Assert.AreEqual(StarRating.Perfect, result.Rating);
            Assert.Greater(result.DurationSeconds, Config.wave.serviceTime * 2, "Unstaffed service is slow, but serving is not waiting.");
        }

        [Test]
        public void AC2_MovingAWorkerTurnsBottleneckIntoRecoveringImmediately()
        {
            UseSingleWave(rooms: 2, drinks: 2, food: 20);
            ResortGame game = NewGame();
            StartWaveAndArrive(game);
            TickUntil(game, () => game.GetNeedStatus(NeedId.Food).State == NeedState.Bottleneck);
            Assert.AreEqual(Config.wave.utilizationEmpty, game.GetServiceSpeed(BuildingId.Restaurant), 1e-9);

            MoveWorkerResult moved = game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);

            Assert.AreEqual(MoveWorkerResult.Moved, moved);
            Assert.AreEqual(NeedState.Recovering, game.GetNeedStatus(NeedId.Food).State, "Feedback must come in the same interaction.");
            Assert.AreEqual(Config.wave.utilizationStaffed, game.GetServiceSpeed(BuildingId.Restaurant), 1e-9);
        }

        [Test]
        public void AC2b_TheSourceBuildingVisiblyLosesItsWorker()
        {
            UseSingleWave(rooms: 2, drinks: 2, food: 20);
            ResortGame game = NewGame();
            StartWaveAndArrive(game);

            game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);

            Assert.IsFalse(game.GetBuilding(BuildingId.Reception).HasWorker);
            Assert.AreEqual(Config.wave.utilizationEmpty, game.GetServiceSpeed(BuildingId.Reception), 1e-9);
            Assert.AreEqual(BuildingId.Reception, game.GetEmptyBuilding());
        }

        [Test]
        public void AC2_SpeedChangeAppliesToGuestsAlreadySeated()
        {
            // One Food unit sits down at 90 s on the empty Restaurant (~36 s service).
            // Moving a Worker 6 s later must speed up that same guest, not only the next ones.
            UseSingleWave(rooms: 0, drinks: 0, food: 1);
            ResortGame slow = NewGame();
            double slowDuration = PlayWaveToEnd(slow).DurationSeconds;

            ResortGame fast = NewGame();
            StartWaveAndArrive(fast);
            TickUntil(fast, () => fast.GetNeedStatus(NeedId.Food).Seated == 1);
            fast.Tick(6);
            fast.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);
            double fastDuration = PlayWaveToEnd(fast).DurationSeconds;

            Assert.Less(fastDuration, slowDuration - 10);
        }

        [Test]
        public void AC2c_UnstaffedBuildingIsSlowNotDead()
        {
            UseSingleWave(rooms: 0, drinks: 0, food: 14);
            ResortGame game = NewGame();

            WaveResult result = PlayWaveToEnd(game);

            Assert.AreEqual(14, result.UnitsServed);
            Assert.IsFalse(game.GetBuilding(BuildingId.Restaurant).HasWorker);
        }

        [Test]
        public void AC3_AC4_TimerClosesArrivalsButTheWaveRunsUntilQueuesAreEmpty()
        {
            UseSingleWave(rooms: 2, drinks: 2, food: 20);
            ResortGame game = NewGame();
            StartWaveAndArrive(game);

            TickUntil(game, () => WaveTime(game) >= Config.wave.waveTimer + 0.5);

            Assert.AreEqual(GamePhase.WaveActive, game.Phase);
            Assert.IsTrue(game.CurrentWave.ArrivalsClosed);
            Assert.Greater(game.GetNeedStatus(NeedId.Food).Waiting, 0, "Queue is still being drained.");
            Assert.AreEqual(20, game.GetNeedStatus(NeedId.Food).Total, "Nobody leaves unserved.");
            Assert.AreEqual(MoveWorkerResult.Moved, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant), "Moves stay possible during the drain.");

            WaveResult result = PlayWaveToEnd(game);
            Assert.AreEqual(24, result.UnitsServed);
        }

        [Test]
        public void AC4b_WorstWaveGivesWaveFloorOneStarAndSomeCoins()
        {
            UseSingleWave(rooms: 0, drinks: 0, food: 60);
            ResortGame game = NewGame();

            WaveResult result = PlayWaveToEnd(game, step: 1.0);

            Assert.AreEqual(Config.satisfaction.waveFloor, result.Satisfaction, 1e-9);
            Assert.AreEqual(StarRating.OneStar, result.Rating);
            Assert.Greater(result.Coins, 0);
        }

        [Test]
        public void WR9_NeedOfAnUnboughtBuildingNeverSpawns()
        {
            UseSingleWave(rooms: 4, drinks: 4, food: 4, relax: 8, oceanView: 5);
            ResortGame game = NewGame();

            Assert.AreEqual(0, game.GetNextWaveForecast().GetDemand(NeedId.Relax));
            StartWaveAndArrive(game);
            Assert.IsTrue(game.GetNeedStatus(NeedId.Relax).IsLocked);
            Assert.IsTrue(game.GetNeedStatus(NeedId.OceanView).IsLocked);

            WaveResult result = PlayWaveToEnd(game);
            Assert.AreEqual(12, result.UnitsServed);
            Assert.That(result.Needs.Select(n => n.Need), Has.No.Member(NeedId.Relax));
        }

        [Test]
        public void AC8_SuddenRushHitsTheUnstaffedBuildingAndAWorkerMoveSolvesItWithoutEnergy()
        {
            UseSingleWave(rooms: 12, drinks: 16, food: 10, rush: true);
            ResortGame game = NewGame();
            Assert.AreEqual(0, game.Energy);
            Assert.AreEqual(10, game.GetNextWaveForecast().GetDemand(NeedId.Food), "The rush is never in the forecast.");
            BuildingId? rushTarget = null;
            game.RushStarted += building => rushTarget = building;
            StartWaveAndArrive(game);

            TickUntil(game, () => rushTarget.HasValue);

            Assert.AreEqual(BuildingId.Restaurant, rushTarget);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase, "The rush doesn't pause the wave.");
            Assert.AreEqual(16, game.GetNeedStatus(NeedId.Food).Total, "Panel total grows by the rush.");
            Assert.AreEqual(MoveWorkerResult.Moved, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));

            WaveResult result = PlayWaveToEnd(game);
            Assert.AreEqual(12 + 16 + 10 + 6, result.UnitsServed, "Rush units count toward the reward.");
            Assert.AreEqual(BuildingId.Restaurant, result.RushTarget);
        }

        [Test]
        public void SuddenRushFollowsWhicheverBuildingIsEmpty()
        {
            UseSingleWave(rooms: 12, drinks: 16, food: 10, rush: true);
            ResortGame game = NewGame();
            game.MoveWorker(BuildingId.BeachBar, BuildingId.Restaurant);

            WaveResult result = PlayWaveToEnd(game);

            Assert.AreEqual(BuildingId.BeachBar, result.RushTarget);
        }

        [Test]
        public void AC6_RecommendedUpgradeIsTheLongestBottleneck()
        {
            UseSingleWave(rooms: 4, drinks: 4, food: 20);
            ResortGame game = NewGame();

            WaveResult result = PlayWaveToEnd(game);

            Assert.AreEqual(BuildingId.Restaurant, result.RecommendedUpgrade);
            NeedWaveStats food = result.Needs.Single(n => n.Need == NeedId.Food);
            Assert.That(result.Needs.All(n => n.BottleneckSeconds <= food.BottleneckSeconds));
        }

        [Test]
        public void PerfectWaveRecommendsNothing()
        {
            UseSingleWave(rooms: 4, drinks: 4, food: 2);
            ResortGame game = NewGame();

            WaveResult result = PlayWaveToEnd(game);

            Assert.AreEqual(StarRating.Perfect, result.Rating);
            Assert.IsNull(result.RecommendedUpgrade);
        }

        [Test]
        public void ResultsDoNotDependOnFrameRate()
        {
            UseSingleWave(rooms: 12, drinks: 16, food: 10, rush: true);

            WaveResult smallSteps = PlayWaveToEnd(NewGame(), step: 1.0 / 60.0);
            WaveResult bigSteps = PlayWaveToEnd(NewGame(), step: 0.7);

            Assert.AreEqual(smallSteps.Satisfaction, bigSteps.Satisfaction, 1e-6);
            Assert.AreEqual(smallSteps.Coins, bigSteps.Coins);
            Assert.AreEqual(smallSteps.DurationSeconds, bigSteps.DurationSeconds, 1e-6);
        }
    }
}
