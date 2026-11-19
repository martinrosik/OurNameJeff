using System;
using ResortEmpire.Core;

namespace ResortEmpire.Game
{
    /// <summary>
    /// In-game strings for the greybox (English, as the UI doc requires). Kept in one place so the
    /// Localization pass can replace them later. Add new screens' strings here.
    /// It lives in the Game layer, next to <see cref="GreyboxPalette"/>, so the island's labels and the UI screens share it.
    /// </summary>
    public static class GameText
    {
        public static string Need(ResortGame game, NeedId need) =>
            game.Config.GetNeed(need)?.displayName ?? need.ToString();

        public static string Building(ResortGame game, BuildingId building) =>
            game.Config.GetBuilding(building)?.displayName ?? building.ToString();

        /// <summary>OUR-38: "! Beach Bar queue growing".</summary>
        public static string QueueGrowing(ResortGame game, BuildingId building) => $"! {Building(game, building)} queue growing";

        /// <summary>OUR-38: a guest's feedback on a bottleneck, e.g. "Drinks takes a bit too long."</summary>
        public static string GuestComplaint(ResortGame game, NeedId need) => $"\"{Need(game, need)} takes a bit too long.\"";

        /// <summary>OUR-39: "Beach Bar is catching up".</summary>
        public static string CatchingUp(ResortGame game, BuildingId building) => $"{Building(game, building)} is catching up";

        // OUR-48: the Worker badge on the island, and the "No Worker" every screen shows.
        public const string NoWorkerLabel = "No Worker";
        public const string WorkerBadge = "W";
        public const string NoWorkerBadge = "-";

        public static string Status(ResortGame game)
        {
            int wave = game.CurrentWaveIndex + 1;
            switch (game.Phase)
            {
                case GamePhase.AwaitingWave: return game.HasNextWave ? "READY FOR GUESTS" : AllWavesPlayedTitle;
                case GamePhase.WaveIncoming: return "BOAT ARRIVING";
                case GamePhase.WaveActive: return $"WAVE {wave} IN PROGRESS";
                case GamePhase.WavePaused: return "PAUSED";
                case GamePhase.WaveResolving: return $"WAVE {wave}: GUESTS LEAVING";
                case GamePhase.WaveComplete: return WaveCompleteTitle(game.LastWaveResult?.WaveNumber ?? wave);
                case GamePhase.Upgrade: return "BETWEEN WAVES";
                default: return string.Empty;
            }
        }

        // S1 Home: Welcome Guests (OUR-27) and the next-wave forecast (OUR-33).
        public const string WelcomeGuestsLabel = "WELCOME GUESTS";
        public const string HomeHint = "Check what the guests will need. Tap a building to move a Worker.";
        public const string ForecastNote = "Each number is how many guests will need that service. One building always stays without a Worker.";

        public static string WelcomeGuestsForWave(WaveForecast forecast) => $"{WelcomeGuestsLabel}\n<size=65%>Wave {forecast.WaveNumber}</size>";

        public static string ForecastTitle(WaveForecast forecast) => $"NEXT: WAVE {forecast.WaveNumber}";

        public static string ForecastGuests(WaveForecast forecast) => $"{forecast.Guests} guests are coming";

        public static string ForecastDemand(ResortGame game, NeedDemand demand) => $"{Need(game, demand.Need)}  {demand.Units}";

        /// <summary>"Restaurant: Worker" or "Restaurant: No Worker" beside a forecast row.</summary>
        public static string ForecastStaffing(string buildingName, bool hasWorker) => $"{buildingName}: {(hasWorker ? "Worker" : NoWorkerLabel)}";

        // OUR-37: the Wave Needs Panel.
        public const string NeedsPanelHint = "Tap a need to move a Worker.";

        public static string WaveTitle(WaveSimulation wave) => $"WAVE {wave.WaveNumber}";

        public static string WaveGuests(WaveSimulation wave) => $"{wave.GuestCount} guests in this wave";

        public static string WaveArrivals(WaveSimulation wave) =>
            wave.ArrivalsClosed ? "All guests arrived" : "Guests arriving...";

        public static string NeedProgress(ResortGame game, NeedStatus status) =>
            $"{Need(game, status.Need)}  {status.Served}/{status.Total}";

        public static string NeedStateLabel(NeedState state, bool isPremium)
        {
            string label;
            switch (state)
            {
                case NeedState.Bottleneck: label = "Queue growing!"; break;
                case NeedState.Recovering: label = "Catching up"; break;
                case NeedState.Satisfied: label = "All served"; break;
                default: label = "OK"; break;
            }

            return isPremium ? $"PREMIUM: {label}" : label;
        }

        public static string BuildingStaffing(BuildingRuntime building) =>
            $"{building.Definition.displayName}: {(building.HasWorker ? "Worker" : $"{NoWorkerLabel} (slow)")}";

        /// <summary>"Full" appears only with a Worker: then a queue means the building is too small, not understaffed.</summary>
        public static string NeedQueue(NeedStatus status, BuildingRuntime building)
        {
            string waiting = $"{status.Waiting} waiting";
            bool isFull = building.HasWorker && status.Seated >= building.Seats;
            return isFull ? $"Full: {status.Seated}/{building.Seats} seats, {waiting}" : waiting;
        }

        // OUR-45: the Worker Allocation sheet (S3).
        public const string CloseLabel = "CLOSE";

        public static string WorkerSheetTitle(string buildingName) => $"WORKER ALLOCATION · {buildingName.ToUpperInvariant()}";

        /// <summary>"TAKE FROM BEACH BAR": what the other building is doing, and that it will be left without a Worker.</summary>
        public static string TakeWorkerOption(ResortGame game, BuildingRuntime other)
        {
            string otherName = other.Definition.displayName;
            return $"TAKE FROM {otherName.ToUpperInvariant()}\n<size=75%>{WorkerSheetActivity(game, other)} · {otherName} will be left without a Worker</size>";
        }

        /// <summary>"MOVE TO RESTAURANT": what the other building is doing, and that this one will be left without a Worker.</summary>
        public static string MoveWorkerOption(ResortGame game, BuildingRuntime here, BuildingRuntime other)
        {
            string otherName = other.Definition.displayName;
            return $"MOVE TO {otherName.ToUpperInvariant()}\n<size=75%>{WorkerSheetActivity(game, other)} · {here.Definition.displayName} will be left without a Worker</size>";
        }

        public static string WorkerSheetSubtitle(ResortGame game, BuildingRuntime building)
        {
            string activity = WorkerSheetActivity(game, building);
            if (!building.HasWorker)
            {
                return $"{NoWorkerLabel}: serving slowly. {activity}. Take a Worker from another building:";
            }

            // Too small only while the queue still grows with a Worker (BOTTLENECK), not while it is catching up.
            if (game.GetNeedState(building.Need) == NeedState.Bottleneck)
            {
                int waiting = game.GetNeedStatus(building.Need).Waiting;
                return $"Has its Worker, but the queue is long ({waiting}): the building is too small. Upgrade it after the wave.";
            }

            return $"Has its Worker. {activity}. Send it to the building without one:";
        }

        /// <summary>What a building is doing: the next wave's demand before the wave, its guests during it.</summary>
        public static string WorkerSheetActivity(ResortGame game, BuildingRuntime building)
        {
            string need = Need(game, building.Need);
            if (game.Phase == GamePhase.AwaitingWave)
            {
                WaveForecast forecast = game.GetNextWaveForecast();
                int units = forecast != null ? forecast.GetDemand(building.Need) : 0;
                return $"Next wave: {need} {units}";
            }

            NeedStatus status = game.GetNeedStatus(building.Need);
            return $"{need}: {status.Served}/{status.Total} served, {status.Waiting} waiting";
        }

        // OUR-40: Wave Complete.
        public const string GuestsServedLabel = "Guests served";
        public const string ContinueLabel = "CONTINUE";

        public static string WaveCompleteTitle(int waveNumber) => $"WAVE {waveNumber} COMPLETE";

        public static string WaveCompleteMessage(WaveResult result) => $"Wave {result.WaveNumber} has ended. Well done!";

        /// <summary>Everyone is always served (WR4), so both numbers are the wave's guests.</summary>
        public static string GuestsServed(WaveResult result) => $"{result.Guests}/{result.Guests}";

        // Wave evaluation: the rating stars and Wave Satisfaction, shown for the first time on Wave Complete.
        public const string PerfectLabel = "PERFECT!";
        public const string WaveSatisfactionLabel = "Wave Satisfaction";

        /// <summary>"88%". Rounded down, so the shown % never reaches a star threshold the wave missed.</summary>
        public static string WaveSatisfaction(WaveResult result) => $"{(int)Math.Floor(result.Satisfaction + 1e-6)}%";

        // After the last wave: the same card, with Play Again.
        public const string AllWavesPlayedTitle = "ALL WAVES PLAYED";
        public const string AllWavesPlayedMessage = "Every wave of guests had a great stay!";
        public const string WavesCompletedLabel = "Waves completed";
        public const string PlayAgainLabel = "PLAY AGAIN";

        public static string WavesCompleted(ResortGame game) => $"{game.CompletedWaves}/{game.Config.waves.Count}";
    }
}
