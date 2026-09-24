using System;

namespace TillWinter.Core
{
    /// <summary>One page of the family album (GDD §8.2 v2.4): what a generation did, written when it hands the farm on.</summary>
    [Serializable]
    public sealed class AlbumEntry
    {
        public int Generation;
        public int Years;
        public double Coins;
        public int Seeds;
        public int Harvests;
        /// <summary>The best year's stars, 0 if no year was graded.</summary>
        public int BestGrade;
        /// <summary><see cref="HeirTrait"/> and <see cref="ChallengeKind"/> as ints (JsonUtility-friendly).</summary>
        public int Trait;
        public int Challenge;
        /// <summary>The New Game+ round it was played in.</summary>
        public int NgPlus;
    }

    /// <summary>
    /// After the ending (GDD §8.3–§8.4 v2.4): New Game+ starts the family over, harder, keeping its album and heirlooms;
    /// the daily farm is one year on a field set by the date, offline, played apart from the main farm.
    /// </summary>
    public static class AfterEnding
    {
        /// <summary>
        /// A fresh farm for the next New Game+ round: the album and the heirlooms (achievements) carry over, everything
        /// else starts again. Null unless the ending has been seen.
        /// </summary>
        public static FarmSim NewGamePlus(FarmSim ended, FarmConfig config, int seed)
        {
            if (ended == null || !ended.State.EndingSeen) return null;
            var sim = new FarmSim(config, seed);
            var s = sim.State;
            s.NgPlus = ended.State.NgPlus + 1;
            s.Generation.Achievements = ended.State.Generation.Achievements;
            s.AlbumList.AddRange(ended.State.AlbumList);
            // The family's lifetime record and what it has already been taught stay too: hints never fire twice.
            var from = ended.State.Generation;
            var to = s.Generation;
            to.LifetimeCoinsTotal = from.LifetimeCoinsTotal;
            to.SeedsEarnedTotal = from.SeedsEarnedTotal;
            to.CrowsScared = from.CrowsScared;
            to.Harvests = from.Harvests;
            to.HarvestsHand = from.HarvestsHand;
            to.HarvestsApprentice = from.HarvestsApprentice;
            to.HarvestsTractor = from.HarvestsTractor;
            to.HarvestsLateFrost = from.HarvestsLateFrost;
            to.HarvestsAtGenerationStart = from.Harvests;
            to.GoldenHarvests = from.GoldenHarvests;
            to.BestCombo = from.BestCombo;
            to.Strikes = from.Strikes;
            to.Crits = from.Crits;
            to.Breaks = from.Breaks;
            to.DeepestLayer = from.DeepestLayer;
            to.TimePlayedSeconds = from.TimePlayedSeconds;
            to.YearsTotal = from.YearsTotal + 1; // and the new round's first year
            to.GoalsMet = from.GoalsMet;
            to.PestsStopped = from.PestsStopped;
            s.Onboarding.Bits = ended.State.Onboarding.Bits;
            sim.RefreshStats();
            return sim;
        }

        /// <summary>A stable seed for a calendar day (yyyymmdd): the same day gives every player the same farm.</summary>
        public static int DailySeed(int yyyymmdd)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < 4; i++)
                {
                    h ^= (uint)((yyyymmdd >> (i * 8)) & 0xff);
                    h *= 16777619u;
                }
                return (int)(h & 0x7fffffff) | 1;
            }
        }

        /// <summary>
        /// The daily farm: one year, starting in spring, with a field and a few Almanac levels chosen by the day, and
        /// goals, weather, pests and luck from the first year. No heirs, no heirlooms, no trader: the day is the same
        /// for everyone. The score is the year's coins.
        /// </summary>
        public static FarmSim Daily(int yyyymmdd)
        {
            return DailyFrom(DailyConfig(), DailySeed(yyyymmdd), yyyymmdd);
        }

        /// <summary>
        /// The same day's farm again, where it was (a language or text-size change rebuilds the scene): the daily is
        /// never written to disk, so the scene hands its <see cref="FarmSim.ToSave"/> across in memory.
        /// </summary>
        public static FarmSim ResumeDaily(SaveData data, int yyyymmdd)
        {
            if (data == null || yyyymmdd == 0) return null;
            var sim = FarmSim.FromSave(data, DailyConfig());
            if (sim == null) return null;
            sim.State.Daily = yyyymmdd;
            return sim;
        }

        private static FarmConfig DailyConfig()
        {
            return new FarmConfig
            {
                GoalFirstYear = 1,
                WeatherFirstYear = 1,
                WeatherChance = 1,
                PestFirstYear = 1,
                LuckyFirstYear = 1,
                TraderFirstYear = int.MaxValue,
                HeirsEnabled = false,
                HeirloomsEnabled = false,
            };
        }

        private static FarmSim DailyFrom(FarmConfig cfg, int seed, int yyyymmdd)
        {
            var sim = new FarmSim(cfg, seed);
            var pick = new Rng(seed ^ 0x2545F491); // its own stream: the sim's RNG stays the day's
            int Roll(int min, int max) => min + Math.Min(max - min, (int)(pick.NextDouble() * (max - min + 1)));
            sim.DebugSetLevel("expand_field", Roll(1, 2));
            sim.DebugSetLevel("hoe_damage", Roll(1, 3));
            sim.DebugSetLevel("growth", Roll(1, 3));
            sim.DebugSetLevel("soft_ground", Roll(0, 2));
            sim.DebugSetLevel("apprentice_count", Roll(0, 2));
            sim.DebugSetLevel("scarecrow", Roll(0, 1));
            int crops = Roll(1, 3);
            sim.DebugSetLevel("unlock_tomato", 1);
            if (crops >= 2) sim.DebugSetLevel("unlock_corn", 1);
            if (crops >= 3) sim.DebugSetLevel("unlock_pumpkin", 1);
            foreach (var p in sim.State.PlotArray) p.BedTier = Math.Min(crops, 1 + (p.Pos.X + p.Pos.Y) % (crops + 1));
            sim.State.Daily = yyyymmdd;
            foreach (Hint h in Enum.GetValues(typeof(Hint))) sim.MarkHint(h); // the family farm teaches; the daily farm just plays
            sim.BeginFirstYear();
            return sim;
        }
    }
}
