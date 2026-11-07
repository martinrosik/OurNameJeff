namespace ResortEmpire.Core
{
    /// <summary>
    /// Core loop state machine (Build doc §4.3).
    /// <code>
    /// AWAITING_WAVE → WAVE_INCOMING → WAVE_ACTIVE ⇄ WAVE_PAUSED
    ///                                     ↓
    /// AWAITING_WAVE ← UPGRADE ← WAVE_COMPLETE ← WAVE_RESOLVING
    /// </code>
    /// The resting state between waves is <see cref="AwaitingWave"/>. Never call it anything else.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>Resort is "Ready for Guests". Start Wave, Worker moves, upgrades and unlocks are available.</summary>
        AwaitingWave = 0,

        /// <summary>Boat arrives, guests step off. Short and transient.</summary>
        WaveIncoming = 1,

        /// <summary>Arrivals, then drain. The player moves Workers and uses Energy boosts. No upgrades (WR6).</summary>
        WaveActive = 2,

        /// <summary>App in background or closed. Every timer is frozen; nothing progresses, nothing decays.</summary>
        WavePaused = 3,

        /// <summary>All queues are empty after arrivals closed; the result is being computed.</summary>
        WaveResolving = 4,

        /// <summary>Result screen (S4): rating, rewards, breakdown.</summary>
        WaveComplete = 5,

        /// <summary>Optional upgrade suggestion (S5) and area unlocks (S9) after a wave.</summary>
        Upgrade = 6,
    }
}
