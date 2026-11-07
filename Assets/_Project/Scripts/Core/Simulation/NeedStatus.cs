namespace ResortEmpire.Core
{
    /// <summary>
    /// Snapshot of one need for the Wave Needs Panel (S2): "Food 2/6", state, queue.
    /// </summary>
    public readonly struct NeedStatus
    {
        public NeedStatus(NeedId need, NeedState state, int served, int total, int waiting, int seated)
        {
            Need = need;
            State = state;
            Served = served;
            Total = total;
            Waiting = waiting;
            Seated = seated;
        }

        public NeedId Need { get; }
        public NeedState State { get; }

        /// <summary>Units fully served so far.</summary>
        public int Served { get; }

        /// <summary>Units this wave will bring in total, including a Sudden rush once it hits.</summary>
        public int Total { get; }

        /// <summary>Units in the queue, not yet seated. This drives the warning.</summary>
        public int Waiting { get; }

        /// <summary>Units currently being served. Full seats are healthy, not a problem.</summary>
        public int Seated { get; }

        public bool IsLocked => State == NeedState.Locked;
    }
}
