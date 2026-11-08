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
    /// <para>
    /// The whole state machine is listed as the shared contract. Implemented today: AwaitingWave → WaveIncoming →
    /// WaveActive (OUR-27) → WaveResolving → WaveComplete (OUR-35) → AwaitingWave (OUR-40, skips Upgrade for now).
    /// The others are reached once their tickets exist (see docs/tasks).
    /// </para>
    /// </summary>
    public enum GamePhase
    {
        /// <summary>Resort is "Ready for Guests": forecast shown (OUR-33), Start Wave available (OUR-27).</summary>
        AwaitingWave = 0,

        /// <summary>The guest boat sails in after Start Wave. Short and transient. Implemented.</summary>
        WaveIncoming = 1,

        /// <summary>The wave runs. Implemented as a state; arrivals (OUR-34), service (OUR-46/47) and drain (OUR-41) fill it.</summary>
        WaveActive = 2,

        /// <summary>App in background or closed; every timer frozen. Not implemented yet (later sprint).</summary>
        WavePaused = 3,

        /// <summary>
        /// All queues empty after arrivals closed (OUR-35): the result is computed and the guests walk back to the
        /// boat. Ends with <see cref="ResortGame.FinishResolving"/>.
        /// </summary>
        WaveResolving = 4,

        /// <summary>The wave is over and counted (OUR-35). Ends with <see cref="ResortGame.ContinueAfterWave"/> (OUR-40).</summary>
        WaveComplete = 5,

        /// <summary>Upgrade suggestion (S5) and area unlocks (S9) after a wave. Not implemented yet (later sprint).</summary>
        Upgrade = 6,
    }
}
