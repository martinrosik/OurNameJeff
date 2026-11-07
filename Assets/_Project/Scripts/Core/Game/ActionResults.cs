namespace ResortEmpire.Core
{
    public enum MoveWorkerResult
    {
        Moved,

        /// <summary>Not possible in this phase (paused, result screen…).</summary>
        NotAllowedNow,

        SameBuilding,
        BuildingNotOwned,

        /// <summary>The source building has no Worker to give.</summary>
        NoWorkerAtSource,

        /// <summary>The target already has a Worker; every building has 1 slot in the prototype.</summary>
        TargetFull,
    }

    public enum BoostResult
    {
        Boosted,

        /// <summary>Speed Up exists only during WAVE_ACTIVE.</summary>
        NotAllowedNow,

        BuildingNotOwned,

        /// <summary>The per-wave limit for this building is used up; the button should be gone already.</summary>
        AlreadyBoostedThisWave,

        /// <summary>Never blocks service: offer the rewarded video instead (HR1).</summary>
        NotEnoughEnergy,
    }

    public enum UpgradeResult
    {
        Upgraded,

        /// <summary>Upgrades happen only between waves (WR6). The UI must not even show the button during a wave.</summary>
        NotAllowedNow,

        BuildingNotOwned,
        AlreadyMaxLevel,
        NotEnoughCoins,
    }

    public enum UnlockResult
    {
        Unlocked,
        NotAllowedNow,
        AlreadyUnlocked,

        /// <summary>The gate isn't met yet (prototype: wave count; MVP: Reputation).</summary>
        GateNotMet,

        NotEnoughCoins,
        NotInGame,
    }

    /// <summary>S9 states for a construction site.</summary>
    public enum UnlockStatus
    {
        /// <summary>Gate not met. Show what will open it.</summary>
        Locked,

        /// <summary>Gate met and the player can pay.</summary>
        Available,

        /// <summary>Gate met but Coins are missing ("Missing: 400 Coins").</summary>
        NotAffordable,

        Unlocked,
    }
}
