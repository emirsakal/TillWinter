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
        public const int CurrentSchemaVersion = 16;

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
        /// <summary>v7: the ring footprint the player chose.</summary>
        public int RingShape;
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

        // v5: the Winter screen's year summary
        public double CoinsThisYear;
        public int HarvestsThisYear;

        // v10: grade, yearly goal, weather (GDD §3/§5.4 v1.9)
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

        // v11: placed scarecrows (plot corners) and the farm dog's rest (GDD §4/§5.1 v2.0)
        public int[] ScarecrowX = new int[0];
        public int[] ScarecrowY = new int[0];
        public float DogCooldown;

        // v12: pests, lucky moments, the trader, their check timers (GDD §5.5–§5.7 v2.1)
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

        // v13: the barn and the Almanac respec (GDD §3.5/§6.3 v2.2)
        public double BarnStock;
        public int BarnCount;
        public float BarnStoreShare, BarnStoreAcc;
        public double BarnMarketPrice = 1;
        public double BarnJars;
        public double AlmanacSpent;
        public bool RespecUsed;

        // v14: heirs, challenge, achievements (GDD §7.4–§7.6 v2.3)
        public int Trait;
        public int[] HeirOffer = new int[0];
        public int Challenge;
        public int Achievements;
        public int GoalsMet, PestsStopped;

        // v15: the family album and New Game+ (GDD §8.2–§8.3 v2.4)
        public int BestGradeThisGeneration;
        public int HarvestsAtGenerationStart;
        public int NgPlus;
        public AlbumEntry[] Album = new AlbumEntry[0];

        // v16: the first generation's checklist and the away plan (GDD §10.5/§10.7 v2.5)
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
        /// <summary>v2</summary>
        public bool Golden;
        /// <summary>v7: seconds the plot has stood Ripe (over-ripening).</summary>
        public float RipeAge;
        /// <summary>v8: the crop picked from the seed bag, -1 = follow the bed; <c>Tier</c> is the bed's quality.</summary>
        public int Choice = -1;
        /// <summary>v9: <see cref="PlotKind"/> (plain, fertile, stony).</summary>
        public int Kind;
        /// <summary>v9: last year's crop, -1 = none (crop rotation).</summary>
        public int LastYearTier = -1;
        /// <summary>v10: seconds of drought on a Wet plot.</summary>
        public float DryTimer;
    }

    [Serializable]
    public sealed class ApprenticeSave
    {
        public float X, Y;
        /// <summary>v11: <see cref="ApprenticeRole"/>.</summary>
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
                    case 4: data = V4ToV5(data); break;
                    case 5: data = V5ToV6(data); break;
                    case 6: data = V6ToV7(data); break;
                    case 7: data = V7ToV8(data); break;
                    case 8: data = V8ToV9(data); break;
                    case 9: data = V9ToV10(data); break;
                    case 10: data = V10ToV11(data); break;
                    case 11: data = V11ToV12(data); break;
                    case 12: data = V12ToV13(data); break;
                    case 13: data = V13ToV14(data); break;
                    case 14: data = V14ToV15(data); break;
                    case 15: data = V15ToV16(data); break;
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

        /// <summary>v4 → v5: the running year's coins and harvests were never counted, so this year starts at zero.</summary>
        private static SaveData V4ToV5(SaveData d)
        {
            d.CoinsThisYear = 0;
            d.HarvestsThisYear = 0;
            d.SchemaVersion = 5;
            return d;
        }

        /// <summary>v6 → v7: crops never aged and the ring was always round; both defaults are already right.</summary>
        private static SaveData V6ToV7(SaveData d)
        {
            if (d.Plots != null) foreach (var p in d.Plots) p.RipeAge = 0f;
            d.RingShape = 0;
            d.SchemaVersion = 7;
            return d;
        }

        /// <summary>v7 → v8: no crop was ever picked, so every plot follows its bed; the saved tier becomes the bed's quality.</summary>
        private static SaveData V7ToV8(SaveData d)
        {
            if (d.Plots != null) foreach (var p in d.Plots) if (p != null) p.Choice = -1;
            d.SchemaVersion = 8;
            return d;
        }

        /// <summary>v8 → v9: every plot is plain ground and none remembers a crop from last year.</summary>
        private static SaveData V8ToV9(SaveData d)
        {
            if (d.Plots != null) foreach (var p in d.Plots) if (p != null) { p.Kind = 0; p.LastYearTier = -1; }
            d.SchemaVersion = 9;
            return d;
        }

        /// <summary>v9 → v10: no year graded or goal set yet, clear skies, no drought under way.</summary>
        private static SaveData V9ToV10(SaveData d)
        {
            d.YearFreshSum = 0;
            d.CropsLostThisYear = 0;
            d.LastGrade = 0;
            d.LastGradeBonus = 0;
            d.LastYearCoins = 0;
            d.GoalType = 0;
            d.GoalTier = 0;
            d.GoalTarget = d.GoalProgress = d.GoalReward = 0;
            d.GoalDone = false;
            d.Weather = 0;
            d.WeatherLeft = 0f;
            d.PlannedWeather = 0;
            d.PlannedWeatherTime = 0f;
            if (d.Plots != null) foreach (var p in d.Plots) if (p != null) p.DryTimer = 0f;
            d.SchemaVersion = 10;
            return d;
        }

        /// <summary>v10 → v11: scarecrows get placed on load (none remembered), the dog is rested, every apprentice harvests.</summary>
        private static SaveData V10ToV11(SaveData d)
        {
            d.ScarecrowX = new int[0];
            d.ScarecrowY = new int[0];
            d.DogCooldown = 0f;
            if (d.Apprentices != null) foreach (var a in d.Apprentices) if (a != null) a.Role = 0;
            d.SchemaVersion = 11;
            return d;
        }

        /// <summary>v11 → v12: no pest, no luck under way, no trader coming this year, fresh check timers.</summary>
        private static SaveData V11ToV12(SaveData d)
        {
            d.PestKind = 0;
            d.PestX = d.PestY = 0;
            d.PestTimer = d.PestShoo = 0f;
            d.HenCooldown = 0f;
            d.CloverX = d.CloverY = 0;
            d.CloverLeft = d.StarLeft = d.RushLeft = 0f;
            d.TraderActive = false;
            d.TraderTimeLeft = 0f;
            d.TraderPlannedTime = -1f;
            d.TraderSeedPrice = d.TraderRarePrice = 0;
            d.TraderSeedSold = d.TraderRareSold = false;
            d.PestCheckTimer = d.LuckyCheckTimer = 0f;
            d.SchemaVersion = 12;
            return d;
        }

        /// <summary>
        /// v15 → v16: a farm saved before the checklist is past what it teaches, so every step counts as done; the
        /// apprentices keep their own roles while away.
        /// </summary>
        private static SaveData V15ToV16(SaveData d)
        {
            d.ChecklistBits = (1 << FarmSim.ChecklistSteps) - 1;
            d.AwayPlan = 0;
            d.SchemaVersion = 16;
            return d;
        }

        /// <summary>
        /// v14 → v15: an empty album (earlier generations were never written down), no New Game+, and the running
        /// generation's page counts harvests from now on.
        /// </summary>
        private static SaveData V14ToV15(SaveData d)
        {
            d.Album = new AlbumEntry[0];
            d.NgPlus = 0;
            d.BestGradeThisGeneration = Math.Max(0, d.LastGrade);
            d.HarvestsAtGenerationStart = d.Harvests;
            d.SchemaVersion = 15;
            return d;
        }

        /// <summary>
        /// v13 → v14: no heir (the farm was handed on before heirs existed), no challenge, no heirs on offer. Achievements
        /// start empty; the first tick earns every one the saved stats already meet.
        /// </summary>
        private static SaveData V13ToV14(SaveData d)
        {
            d.Trait = 0;
            d.HeirOffer = new int[0];
            d.Challenge = 0;
            d.Achievements = 0;
            d.GoalsMet = d.PestsStopped = 0;
            d.SchemaVersion = 14;
            return d;
        }

        /// <summary>
        /// v12 → v13: an empty barn, no respec used. What the Almanac cost this generation was never counted, so it is
        /// estimated from the levels at the tables' base prices (without Heritage discounts), which is what a respec refunds.
        /// </summary>
        private static SaveData V12ToV13(SaveData d)
        {
            d.BarnStock = 0;
            d.BarnCount = 0;
            d.BarnStoreShare = d.BarnStoreAcc = 0f;
            d.BarnMarketPrice = 1;
            d.BarnJars = 0;
            d.RespecUsed = false;
            double spent = 0;
            foreach (var pair in d.AlmanacLevels ?? new LevelPair[0])
            {
                var node = pair == null ? null : AlmanacData.Get(pair.Id);
                if (node == null) continue;
                for (int l = 0; l < pair.Level; l++) spent += Math.Round(node.BaseCost * Math.Pow(node.CostGrowth, l));
            }
            d.AlmanacSpent = spent;
            d.SchemaVersion = 13;
            return d;
        }

        /// <summary>
        /// v5 -> v6: the remembered tree views are forgotten. They were pan offsets and zooms into the old lane layout;
        /// against the radial one they open the tree off-centre with branches cut off, so each tree opens framed again.
        /// </summary>
        private static SaveData V5ToV6(SaveData d)
        {
            d.AlmanacViewHas = false;
            d.AlmanacViewX = d.AlmanacViewY = 0f;
            d.AlmanacViewZoom = 1f;
            d.HeritageViewHas = false;
            d.HeritageViewX = d.HeritageViewY = 0f;
            d.HeritageViewZoom = 1f;
            d.SchemaVersion = 6;
            return d;
        }
    }
}
