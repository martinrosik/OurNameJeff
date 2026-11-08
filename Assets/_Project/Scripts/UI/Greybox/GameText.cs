using ResortEmpire.Core;

namespace ResortEmpire.UI
{
    /// <summary>
    /// In-game strings for the greybox (English, as the UI doc requires). Kept in one place so the
    /// Localization pass can replace them later. Add new screens' strings here.
    /// </summary>
    internal static class GameText
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
            $"{building.Definition.displayName}: {(building.HasWorker ? "Worker" : "No Worker (slow)")}";

        /// <summary>"Full" appears only with a Worker: then a queue means the building is too small, not understaffed.</summary>
        public static string NeedQueue(NeedStatus status, BuildingRuntime building)
        {
            string waiting = $"{status.Waiting} waiting";
            bool isFull = building.HasWorker && status.Seated >= building.Seats;
            return isFull ? $"Full: {status.Seated}/{building.Seats} seats, {waiting}" : waiting;
        }

        // OUR-40: Wave Complete.
        public const string GuestsServedLabel = "Guests served";
        public const string ContinueLabel = "CONTINUE";

        public static string WaveCompleteTitle(int waveNumber) => $"WAVE {waveNumber} COMPLETE";

        public static string WaveCompleteMessage(WaveResult result) => $"Wave {result.WaveNumber} has ended. Well done!";

        /// <summary>Everyone is always served (WR4), so both numbers are the wave's guests.</summary>
        public static string GuestsServed(WaveResult result) => $"{result.Guests}/{result.Guests}";

        // After the last wave: the same card, with Play Again.
        public const string AllWavesPlayedTitle = "ALL WAVES PLAYED";
        public const string AllWavesPlayedMessage = "Every wave of guests had a great stay!";
        public const string WavesCompletedLabel = "Waves completed";
        public const string PlayAgainLabel = "PLAY AGAIN";

        public static string WavesCompleted(ResortGame game) => $"{game.CompletedWaves}/{game.Config.waves.Count}";
    }
}
