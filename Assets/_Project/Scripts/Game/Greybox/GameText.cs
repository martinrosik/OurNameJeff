using System;
using ResortEmpire.Core;
using UnityEngine;

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

        /// <summary>Live satisfaction bar on the Wave Needs Panel (Figma S2).</summary>
        public const string SatisfactionLabel = "SATISFACTION";

        /// <summary>Live satisfaction bar: the comment before any guest has walked out of a building.</summary>
        public const string NoGuestFeedbackYet = "Guests are on their way.";

        /// <summary>Live satisfaction bar: "86%" (rounded down, like Wave Complete), or "-" before the first guest.</summary>
        public static string LiveSatisfaction(double? satisfaction) =>
            satisfaction.HasValue ? $"{(int)Math.Floor(satisfaction.Value + 1e-6)}%" : "-";

        // {0} is the building's name. Short enough for one line on the side panel.
        private static readonly string[] HappyComments =
        {
            "No waiting at the {0}. Lovely!",
            "The {0} was quick, thanks!",
            "Great service, thank you!",
        };

        private static readonly string[] NeutralComments =
        {
            "The {0} was okay, a bit slow.",
            "Not bad, but I waited a while.",
            "Fine. Could be quicker.",
        };

        private static readonly string[] UnhappyComments =
        {
            "Way too long at the {0}!",
            "I waited ages. Not happy!",
            "The {0} took forever. Ugh.",
        };

        /// <summary>
        /// Live satisfaction bar: what a guest says as it walks out of a building, from its mood (the same one as its
        /// bubble): glad when it sat down quickly, annoyed when it waited long. <paramref name="number"/> picks one of
        /// a few lines, so guests in a row don't all say the same.
        /// </summary>
        public static string GuestFeedback(ResortGame game, NeedId need, GuestMood mood, int number)
        {
            string[] lines;
            switch (mood)
            {
                case GuestMood.Happy: lines = HappyComments; break;
                case GuestMood.Neutral: lines = NeutralComments; break;
                default: lines = UnhappyComments; break;
            }

            BuildingDefinition building = game.Config.GetBuildingForNeed(need);
            string place = building != null ? Building(game, building.building) : Need(game, need);
            string line = lines[(number % lines.Length + lines.Length) % lines.Length];
            return $"\"{string.Format(line, place)}\"";
        }

        /// <summary>Sudden rush (Build doc §4.6): "Sudden rush at Beach Bar!".</summary>
        public static string SuddenRushTitle(ResortGame game, BuildingId building) => $"Sudden rush at {Building(game, building)}!";

        /// <summary>Sudden rush: "+6 guests want Drinks, and the Beach Bar has no Worker."</summary>
        public static string SuddenRushDetail(ResortGame game, SuddenRush rush) =>
            $"+{rush.Units} guests want {Need(game, rush.Need)}, and the {Building(game, rush.Building)} has no Worker.";

        /// <summary>Sudden rush: the main choice, a Worker move (Build doc §4.6 "Move Worker here").</summary>
        public const string SuddenRushMoveWorker = "Move Worker here";

        /// <summary>Sudden rush: close the prompt; the guests come anyway and are served slowly (§4.9).</summary>
        public const string SuddenRushIgnore = "Ignore";

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

        /// <summary>"Food  2/6", with the served count tinted by how the need is going.</summary>
        public static string NeedProgress(ResortGame game, NeedStatus status, Color countColor) =>
            $"{Need(game, status.Need)}  {Colored($"{status.Served}/{status.Total}", countColor)}";

        /// <summary>Rich text in one color, e.g. the red "Restaurant: No Worker" on the forecast.</summary>
        public static string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

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

        /// <summary>
        /// "Full: 3/3 seats, 2 waiting" whenever every seat is taken, with or without a Worker; whether the building is
        /// staffed is the row's other line ("Restaurant: No Worker (slow)" in red), so small and empty stay apart (AC2d).
        /// </summary>
        public static string NeedQueue(NeedStatus status, BuildingRuntime building)
        {
            string waiting = $"{status.Waiting} waiting";
            bool isFull = status.Seated >= building.Seats;
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

        // Resort Value: the status number in the top bar, and what a wave added on Wave Complete.
        public const string ResortValueLabel = "Resort Value";

        /// <summary>"1,250" for the top bar.</summary>
        public static string ResortValue(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>"+100", or "+0" after a weak wave: Resort Value never drops.</summary>
        public static string ResortValueGain(WaveResult result) => $"+{ResortValue(result.ResortValueGain)}";

        // After the last wave: the same card, with Play Again.
        public const string AllWavesPlayedTitle = "ALL WAVES PLAYED";
        public const string AllWavesPlayedMessage = "Every wave of guests had a great stay!";
        public const string WavesCompletedLabel = "Waves completed";
        public const string PlayAgainLabel = "PLAY AGAIN";

        public static string WavesCompleted(ResortGame game) => $"{game.CompletedWaves}/{game.Config.waves.Count}";

        // Settings: the menu behind the gear in the top-right corner.
        public const string SettingsTitle = "Settings";
        public const string SoundLabel = "Sound";
        public const string SoundOnLabel = "ON";
        public const string SoundOffLabel = "OFF";
        public const string TutorialReplayLabel = "PLAY TUTORIAL AGAIN";
        public const string TutorialReplayNote = "A short practice wave. Your resort waits just as you left it.";

        // Tutorial: its bubbles over the practice wave (Tutorial scene).
        public const string TutorialNextLabel = "NEXT";
        public const string TutorialSkipLabel = "SKIP TUTORIAL";
        public const string TutorialFinishLabel = "START MY RESORT";
        public const string TutorialBackLabel = "BACK TO MY RESORT"; // a replay from the settings

        public const string TutorialWelcomeTitle = "Welcome to Paradise!";
        public const string TutorialWelcome =
            "Guests come to your island by boat, in waves. Keep them happy by sending your Workers where they're needed most.";

        public const string TutorialBuildingsTitle = "Your buildings";

        /// <summary>"Each building serves one need: Reception gives Rooms, ...". Names come from the config.</summary>
        public static string TutorialBuildings(ResortGame game)
        {
            var services = new System.Collections.Generic.List<string>();
            foreach (BuildingRuntime building in game.Buildings)
            {
                services.Add($"{building.Definition.displayName}: {Need(game, building.Need)}");
            }

            return $"Each building serves one need ({string.Join(", ", services)}). Every square is a seat for one guest.";
        }

        public const string TutorialWorkersTitle = "Workers";

        /// <summary>How much slower a building is without a Worker, from the config (about 3 times in the prototype).</summary>
        public static string TutorialWorkers(ResortGame game)
        {
            double times = game.Config.wave.utilizationStaffed / game.Config.wave.utilizationEmpty;
            return $"A green {WorkerBadge} means a Worker is here, so the building serves at full speed. Without one, it's about {Math.Round(times)} times slower.";
        }

        public const string TutorialForecastTitle = "The forecast";
        public const string TutorialForecast = "Before every wave you see what the guests will need. Plan your Workers before the boat arrives.";

        public const string TutorialMoveWorkerTitle = "Move a Worker";

        public static string TutorialMoveWorker(ResortGame game, BuildingId emptyBuilding) =>
            $"You have {game.WorkerCount} Workers for {game.Buildings.Count} buildings. Try it: tap the {Building(game, emptyBuilding)} and take a Worker over. Moving is free and instant.";

        public const string TutorialWelcomeGuestsTitle = "Ready?";
        public const string TutorialWelcomeGuests = "Tap Welcome Guests to bring the boat in.";

        public const string TutorialBoatArriving = "Here they come!";

        public const string TutorialNeedsPanelTitle = "The needs panel";
        public const string TutorialNeedsPanel =
            "During the wave this panel shows every need: how many guests are served, and how many wait in line. The wave waits while you read.";

        public const string TutorialWatchQueues = "Watch the buildings. A growing queue means a building needs help.";

        public static string TutorialOverloadTitle(ResortGame game, BuildingId building) => $"The {Building(game, building)} can't keep up!";

        public static string TutorialOverload(ResortGame game, BuildingId building) =>
            $"It has no Worker. Tap the {Building(game, building)} and bring a Worker over. The wave waits for you.";

        public const string TutorialCatchingUpTitle = "Nice move!";

        public static string TutorialCatchingUp(ResortGame game, BuildingId building) =>
            $"The {Building(game, building)} is catching up. You can move Workers during a wave too, whenever a queue grows.";

        public const string TutorialFinishWave = "Now let the guests finish their stay. If another queue grows, move a Worker again.";

        public const string TutorialResultTitle = "Wave complete!";
        public const string TutorialResult =
            "Quick service makes happy guests and more stars. That was a practice wave: your first real guests are next!";

        public const string TutorialResultReplay =
            "Quick service makes happy guests and more stars. That was a practice wave: your resort is waiting just as you left it.";
    }
}
