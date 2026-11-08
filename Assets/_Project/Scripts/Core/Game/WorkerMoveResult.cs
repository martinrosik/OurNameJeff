namespace ResortEmpire.Core
{
    /// <summary>OUR-45: what <see cref="ResortGame.MoveWorker"/> did.</summary>
    public enum WorkerMoveResult
    {
        /// <summary>The Worker moved. The speed of both buildings changed at once.</summary>
        Moved = 0,

        /// <summary>Workers can't be moved in this phase (only before the wave, while the boat comes in, and during it).</summary>
        NotAllowedNow = 1,

        /// <summary>From and to are the same building.</summary>
        SameBuilding = 2,

        /// <summary>The source building has no Worker to give.</summary>
        NoWorkerAtSource = 3,

        /// <summary>The target already has its one Worker (one slot per building).</summary>
        TargetFull = 4,

        /// <summary>One of the buildings isn't on the island.</summary>
        BuildingNotOnIsland = 5,
    }
}
