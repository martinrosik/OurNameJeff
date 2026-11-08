namespace ResortEmpire.Core
{
    /// <summary>
    /// A building's live state. Read it from views; change it only through <see cref="ResortGame"/>.
    /// </summary>
    public sealed class BuildingRuntime
    {
        internal BuildingRuntime(BuildingDefinition definition)
        {
            Definition = definition;
            Level = 1;
            HasWorker = definition.staffedAtStart;
        }

        public BuildingDefinition Definition { get; }
        public BuildingId Id => Definition.building;
        public NeedId Need => Definition.need;

        /// <summary>1-based level. Upgrades (later sprint) raise it.</summary>
        public int Level { get; internal set; }

        /// <summary>
        /// Whether a Worker stands here (OUR-48 shows it). Set from the config at the start, changed by
        /// <see cref="ResortGame.MoveWorker"/> (OUR-45).
        /// </summary>
        public bool HasWorker { get; internal set; }

        /// <summary>Capacity: guests served at the same time (OUR-50 shows it as seat squares).</summary>
        public int Seats => Definition.GetLevel(Level).seats;
    }
}
