using System;

namespace TillWinter.Core
{
    /// <summary>
    /// Plain DTO of everything persistent (GDD §9). Arrays of pairs instead of dictionaries so
    /// Unity's JsonUtility can serialise it. Produced by <see cref="FarmSim.ToSave"/>, consumed by
    /// <see cref="FarmSim.FromSave"/>. Every new persistent field is added here and to the round-trip test.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public long SavedAtUnixSeconds;

        public int Phase;
        public int Year;
        public int Season;
        public float YearTime;
        public bool FrostWarning;
        public double Coins;
        public float CrowSpawnTimer;
        public uint RngState;

        // Generation stats
        public int Generation = 1;
        public double LifetimeCoinsThisGeneration;
        public double LifetimeCoinsTotal;
        public int YearsThisGeneration;
        public int SeedsBanked;
        public int SeedsEarnedTotal;
        public int CrowsScared;
        public int Harvests;

        public int GridSize;
        public PlotSave[] Plots = new PlotSave[0];
        public ApprenticeSave[] Apprentices = new ApprenticeSave[0];
        public LevelPair[] AlmanacLevels = new LevelPair[0];
        public LevelPair[] HeritageLevels = new LevelPair[0];
    }

    [Serializable]
    public sealed class PlotSave
    {
        public int X, Y, Tier, State;
        public float Progress;
        public bool HasCrow;
        public float CrowTimer;
    }

    [Serializable]
    public sealed class ApprenticeSave
    {
        public float X, Y;
    }

    [Serializable]
    public sealed class LevelPair
    {
        public string Id;
        public int Level;
    }

    /// <summary>Version upgrades are pure functions chained here. Unknown versions return null (caller starts fresh).</summary>
    public static class SaveMigrations
    {
        /// <summary>Returns a save at <see cref="SaveData.CurrentSchemaVersion"/>, or null if it cannot be migrated.</summary>
        public static SaveData Migrate(SaveData data)
        {
            if (data == null) return null;
            if (data.SchemaVersion < 1 || data.SchemaVersion > SaveData.CurrentSchemaVersion) return null;
            // Future: while (data.SchemaVersion < Current) data = Migrate_vN_to_vN1(data);
            return data;
        }
    }
}
