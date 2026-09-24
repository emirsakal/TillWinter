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
        /// <summary>v18: core loop v3 (GDD §2v3). Saves of the ring game (v1–v17) start fresh: the field was a different game.</summary>
        public const int CurrentSchemaVersion = 18;
        /// <summary>The oldest schema that still loads.</summary>
        public const int OldestSupportedSchemaVersion = 18;

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
        /// <summary>v18: the depot (GDD §2v3.5).</summary>
        public float Stamina;

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

        // Rain cloud, tractor, combo, greenhouse (golden flag lives on PlotSave)
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

        // Onboarding flags, per-tree canvas memory
        public int OnboardingBits;
        public bool AlmanacViewHas;
        public float AlmanacViewX, AlmanacViewY, AlmanacViewZoom = 1f;
        public bool HeritageViewHas;
        public float HeritageViewX, HeritageViewY, HeritageViewZoom = 1f;

        // Ending and stats screen
        public bool EndingSeen;
        public bool GoldenYearActive;
        public int HarvestsHand;
        public int HarvestsApprentice;
        public int HarvestsTractor;
        public int HarvestsLateFrost;
        public int GoldenHarvests;
        public int BestCombo;
        /// <summary>v18: the hoe's lifetime counters.</summary>
        public int Strikes, Crits, Breaks, DeepestLayer;
        public double TimePlayedSeconds;
        public int YearsTotal;

        // The Winter screen's year summary
        public double CoinsThisYear;
        public int HarvestsThisYear;

        // Grade, yearly goal, weather (GDD §3/§5.4 v1.9)
        public double YearFreshSum;
        public int CropsLostThisYear;
        public int LastGrade;
        public double LastGradeBonus;
        public double LastYearCoins;
        public int GoalType;
        public int GoalTier;
        public double GoalTarget;
        public double GoalProgress;
        public bool GoalDone;
        public double GoalReward;
        public int Weather;
        public float WeatherLeft;
        public int PlannedWeather;
        public float PlannedWeatherTime;

        // Placed scarecrows (plot corners) and the farm dog's rest (GDD §4/§5.1 v2.0)
        public int[] ScarecrowX = new int[0];
        public int[] ScarecrowY = new int[0];
        public float DogCooldown;

        // Pests, lucky moments, the trader, their check timers (GDD §5.5–§5.7 v2.1)
        public int PestKind;
        public int PestX, PestY;
        public float PestTimer, PestShoo;
        public float HenCooldown;
        public int CloverX, CloverY;
        public float CloverLeft, StarLeft, RushLeft;
        public bool TraderActive;
        public float TraderTimeLeft;
        public float TraderPlannedTime = -1f;
        public double TraderSeedPrice, TraderRarePrice;
        public bool TraderSeedSold, TraderRareSold;
        public float PestCheckTimer, LuckyCheckTimer;

        // The barn and the Almanac respec (GDD §3.5/§6.3 v2.2)
        public double BarnStock;
        public int BarnCount;
        public float BarnStoreShare, BarnStoreAcc;
        public double BarnMarketPrice = 1;
        public double BarnJars;
        public double AlmanacSpent;
        public bool RespecUsed;

        // Heirs, challenge, achievements (GDD §7.4–§7.6 v2.3)
        public int Trait;
        public int[] HeirOffer = new int[0];
        public int Challenge;
        public int Achievements;
        public int GoalsMet, PestsStopped;

        // The family album and New Game+ (GDD §8.2–§8.3 v2.4)
        public int BestGradeThisGeneration;
        public int HarvestsAtGenerationStart;
        public int NgPlus;
        public AlbumEntry[] Album = new AlbumEntry[0];

        // The first generation's checklist and the away plan (GDD §10.5/§10.7 v2.5)
        public int ChecklistBits;
        public int AwayPlan;
    }

    [Serializable]
    public sealed class PlotSave
    {
        public int X, Y, Tier, State;
        public float Progress;
        public bool HasCrow;
        public float CrowTimer;
        public bool Golden;
        /// <summary>Seconds the plot has stood Ripe (over-ripening).</summary>
        public float RipeAge;
        /// <summary>The crop picked from the seed bag, -1 = follow the bed; <c>Tier</c> is the bed's quality.</summary>
        public int Choice = -1;
        /// <summary><see cref="PlotKind"/> (plain, fertile).</summary>
        public int Kind;
        /// <summary>Last year's crop, -1 = none (crop rotation).</summary>
        public int LastYearTier = -1;
        /// <summary>v18: the layer (GDD §2v3.3), its specials, its HP and the coins it hides; the crop's own layer.</summary>
        public int Layer;
        public bool Hardpan, Chest;
        public double Hp, MaxHp, HiddenBonus;
        public int CropLayer;
    }

    [Serializable]
    public sealed class ApprenticeSave
    {
        public float X, Y;
        /// <summary><see cref="ApprenticeRole"/>.</summary>
        public int Role;
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
        /// <summary>
        /// Returns a save at <see cref="SaveData.CurrentSchemaVersion"/>, or null if it cannot be migrated. Everything
        /// before v18 was the ring game (a different field, different nodes): it starts fresh, by decision (no players yet).
        /// </summary>
        public static SaveData Migrate(SaveData data, FarmConfig config = null)
        {
            if (data == null) return null;
            if (data.SchemaVersion < SaveData.OldestSupportedSchemaVersion || data.SchemaVersion > SaveData.CurrentSchemaVersion) return null;
            while (data.SchemaVersion < SaveData.CurrentSchemaVersion)
            {
                switch (data.SchemaVersion)
                {
                    default: return null;
                }
            }
            return data;
        }
    }
}
