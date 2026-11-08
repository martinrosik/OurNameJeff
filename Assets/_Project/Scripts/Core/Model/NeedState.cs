namespace ResortEmpire.Core
{
    /// <summary>
    /// State of one need during a wave (Build doc §4.4 "Stav jedného needu", OUR-38 / OUR-39). Each need is served
    /// by one building, so this is also that building's state on the island. Only the queue counts; guests in the
    /// seats are healthy.
    /// <code>
    /// OK ──(queue &gt; threshold and grew over the last hysteresis s)──▶ BOTTLENECK
    /// BOTTLENECK ──(the building serves faster than guests arrive)──▶ RECOVERING
    /// RECOVERING ──(queue ≤ threshold)──▶ OK
    /// RECOVERING ──(not catching up and the queue grew again)──▶ BOTTLENECK
    /// OK / RECOVERING ──(arrivals closed, nobody waiting or seated)──▶ SATISFIED
    /// </code>
    /// </summary>
    public enum NeedState
    {
        /// <summary>The need's building isn't on the island: no demand, not shown (WR9).</summary>
        Locked = 0,

        /// <summary>Calm: the building keeps up.</summary>
        Ok = 1,

        /// <summary>"! Beach Bar queue growing": more than the threshold waiting, and the queue is growing (OUR-38).</summary>
        Bottleneck = 2,

        /// <summary>"Beach Bar is catching up": still a queue, but the building now serves faster than guests arrive (OUR-39).</summary>
        Recovering = 3,

        /// <summary>The need is done for this wave: arrivals closed, nobody waiting or seated.</summary>
        Satisfied = 4,
    }
}
