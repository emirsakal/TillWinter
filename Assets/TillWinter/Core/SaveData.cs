using System;

namespace TillWinter.Core
{
    /// <summary>
    /// Plain DTO of everything persistent (GDD §9). Arrays of pairs instead of dictionaries so
    /// Unity's JsonUtility can serialise it. Produced by <see cref="FarmSim.ToSave"/>, consumed by
    /// <see cref="FarmSim.FromSave"/>. Every new persistent field is added here, to the round-trip
    /// test, and gets a migration step plus a fixture of the previous version.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 4;

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

        // v2: rain cloud, tractor, combo, greenhouse (golden flag lives on PlotSave)
        public bool CloudActive;
        public float CloudX;
        public float CloudTimeLeft;
        public bool CloudSpawnedThisYear;
        public float CloudSpawnTime;
        public int TractorRow;
        public float TractorX;
        public bool TractorSweeping;
        public float TractorTimeToNextSweep;
        public int TractorPassed;
        public int Combo;
        public float ComboTimer;
        public float GreenhouseSecondsLeft;
        public double GreenhouseCoinsThisWinter;

        // v3: onboarding flags, per-tree canvas memory
        public int OnboardingBits;
        public bool AlmanacViewHas;
        public float AlmanacViewX, AlmanacViewY, AlmanacViewZoom = 1f;
        public bool HeritageViewHas;
        public float HeritageViewX, HeritageViewY, HeritageViewZoom = 1f;

        // v4: ending and stats screen
        public bool EndingSeen;
        public bool GoldenYearActive;
        public int HarvestsRing;
        public int HarvestsApprentice;
        public int HarvestsTractor;
        public int HarvestsLateFrost;
        public int GoldenHarvests;
        public int BestCombo;
        public double TimePlayedSeconds;
        public int YearsTotal;
    }

    [Serializable]
    public sealed class PlotSave
    {
        public int X, Y, Tier, State;
        public float Progress;
        public bool HasCrow;
        public float CrowTimer;
        /// <summary>v2</summary>
        public bool Golden;
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
        public static SaveData Migrate(SaveData data, FarmConfig config = null)
        {
            if (data == null) return null;
            if (data.SchemaVersion < 1 || data.SchemaVersion > SaveData.CurrentSchemaVersion) return null;
            while (data.SchemaVersion < SaveData.CurrentSchemaVersion)
            {
                switch (data.SchemaVersion)
                {
                    case 1: data = V1ToV2(data, config ?? new FarmConfig()); break;
                    case 2: data = V2ToV3(data); break;
                    case 3: data = V3ToV4(data); break;
                    default: return null;
                }
            }
            return data;
        }

        /// <summary>v1 → v2: no cloud, tractor idle with a full interval, no combo, no greenhouse accrual, no golden crops.</summary>
        private static SaveData V1ToV2(SaveData d, FarmConfig cfg)
        {
            d.CloudActive = false;
            d.CloudX = 0f;
            d.CloudTimeLeft = 0f;
            d.CloudSpawnedThisYear = false;
            d.CloudSpawnTime = float.MaxValue;
            d.TractorRow = 0;
            d.TractorX = -0.5f;
            d.TractorSweeping = false;
            d.TractorPassed = 0;
            int tractorLevel = 0;
            foreach (var p in d.AlmanacLevels ?? new LevelPair[0]) if (p.Id == "tractor") tractorLevel = p.Level;
            d.TractorTimeToNextSweep = tractorLevel > 0 && tractorLevel < cfg.TractorIntervalByLevel.Length ? cfg.TractorIntervalByLevel[tractorLevel] : 0f;
            d.Combo = 0;
            d.ComboTimer = 0f;
            d.GreenhouseSecondsLeft = d.Phase == (int)Phase.Winter ? cfg.GreenhouseWinterCapSeconds : 0f;
            d.GreenhouseCoinsThisWinter = 0;
            if (d.Plots != null) foreach (var p in d.Plots) if (p != null) p.Golden = false;
            d.SchemaVersion = 2;
            return d;
        }

        /// <summary>v2 → v3: no hints shown yet, no remembered canvas views.</summary>
        private static SaveData V2ToV3(SaveData d)
        {
            d.OnboardingBits = 0;
            d.AlmanacViewHas = false;
            d.AlmanacViewX = d.AlmanacViewY = 0f;
            d.AlmanacViewZoom = 1f;
            d.HeritageViewHas = false;
            d.HeritageViewX = d.HeritageViewY = 0f;
            d.HeritageViewZoom = 1f;
            d.SchemaVersion = 3;
            return d;
        }

        /// <summary>
        /// v3 → v4: ending not seen, no Golden Year running. The per-source harvest split, golden count, best combo and
        /// time played start at 0: v3 kept only the total, which stays in <c>Harvests</c> and cannot be split honestly.
        /// </summary>
        private static SaveData V3ToV4(SaveData d)
        {
            d.EndingSeen = false;
            d.GoldenYearActive = false;
            d.HarvestsRing = d.HarvestsApprentice = d.HarvestsTractor = d.HarvestsLateFrost = 0;
            d.GoldenHarvests = 0;
            d.BestCombo = 0;
            d.TimePlayedSeconds = 0;
            d.YearsTotal = Math.Max(0, d.YearsThisGeneration); // earlier generations' years were never counted
            d.SchemaVersion = 4;
            return d;
        }
    }
}
