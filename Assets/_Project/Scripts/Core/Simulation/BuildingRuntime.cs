namespace ResortEmpire.Core
{
    /// <summary>
    /// A building's live state: owned, level, Worker, Energy boost. Read it from UI;
    /// change it only through <see cref="ResortGame"/>.
    /// </summary>
    public sealed class BuildingRuntime
    {
        internal BuildingRuntime(BuildingDefinition definition)
        {
            Definition = definition;
            Level = 1;
        }

        public BuildingDefinition Definition { get; }
        public BuildingId Id => Definition.building;
        public NeedId Need => Definition.need;

        public bool IsUnlocked { get; internal set; }

        /// <summary>1-based level.</summary>
        public int Level { get; internal set; }

        public bool HasWorker { get; internal set; }

        /// <summary>Seconds of wave time left on the Energy boost; 0 when not boosted.</summary>
        public double BoostRemaining { get; internal set; }

        public int BoostsUsedThisWave { get; internal set; }

        public bool IsBoosted => BoostRemaining > 0;
        public int Seats => Definition.GetLevel(Level).seats;
        public bool IsMaxLevel => Level >= Definition.MaxLevel;
    }
}
