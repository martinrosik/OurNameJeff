namespace ResortEmpire.Core
{
    /// <summary>
    /// State of one need during a wave (Build doc §4.4, "Stav jedného needu").
    /// Driven by the queue of waiting units only; guests already seated never count.
    /// </summary>
    public enum NeedState
    {
        /// <summary>Its building isn't bought yet. The need doesn't spawn and isn't shown (WR9).</summary>
        Locked = 0,

        /// <summary>Calm.</summary>
        Ok = 1,

        /// <summary>Queue above the threshold and growing: "⚠ Beach Bar queue growing".</summary>
        Bottleneck = 2,

        /// <summary>Still above the threshold but the building now serves faster than guests arrive: "Beach Bar is catching up".</summary>
        Recovering = 3,

        /// <summary>Arrivals closed, nobody waiting, nobody seated. Done for this wave.</summary>
        Satisfied = 4,
    }
}
