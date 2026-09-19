using System;

namespace TillWinter.Core
{
    /// <summary>GDD §7.4 (v2.3): the trait an heir brings to their generation.</summary>
    public enum HeirTrait
    {
        None = 0,
        /// <summary>Irrigation and the Sun work faster.</summary>
        GreenThumb = 1,
        /// <summary>The ring waters, grows and harvests faster.</summary>
        QuickHands = 2,
        /// <summary>The Almanac costs less.</summary>
        Merchant = 3,
        /// <summary>Every crop sells for a little more.</summary>
        Steady = 4,
        /// <summary>Apprentices walk faster.</summary>
        Shepherd = 5,
        /// <summary>Fewer crows come.</summary>
        Watchful = 6,
    }

    /// <summary>GDD §7.5 (v2.3): a harder generation for more seeds.</summary>
    public enum ChallengeKind
    {
        None = 0,
        /// <summary>No apprentices and no tractor.</summary>
        NoHelpers = 1,
        /// <summary>Every year is shorter.</summary>
        ShortYears = 2,
    }

    /// <summary>GDD §7.6 (v2.3): achievements; each leaves an heirloom with a small permanent bonus.</summary>
    public enum AchievementId
    {
        FirstHarvest = 0,
        Combo25 = 1,
        Crows50 = 2,
        Golden10 = 3,
        ThreeStars = 4,
        Harvests1000 = 5,
        SecondGeneration = 6,
        GoldenWheat = 7,
        Goals5 = 8,
        TraderDeal = 9,
        Pests10 = 10,
        MarketSale = 11,
    }

    /// <summary>What an heirloom adds, and how much.</summary>
    public enum HeirloomBonus
    {
        CropValue,
        RingSpeeds,
        PassiveGrowth,
        FewerCrows,
        GoldenChance,
        AlmanacDiscount,
    }

    /// <summary>
    /// Heirs, challenges and heirlooms (GDD §7.4–§7.6 v2.3). None of them are tree levels, so they do not go through
    /// <see cref="StatResolver"/>: <see cref="Apply"/> adjusts the resolved stats afterwards, as the Golden Year does.
    /// </summary>
    public static class Legacy
    {
        public const int TraitCount = 6;
        public static readonly int AchievementCount = Enum.GetValues(typeof(AchievementId)).Length;

        /// <summary>The heirloom each achievement leaves: its bonus and amount. Small on purpose: a keepsake, not a tree.</summary>
        public static (HeirloomBonus bonus, double value) Heirloom(AchievementId id)
        {
            switch (id)
            {
                case AchievementId.FirstHarvest: return (HeirloomBonus.CropValue, 0.01);
                case AchievementId.Combo25: return (HeirloomBonus.RingSpeeds, 0.015);
                case AchievementId.Crows50: return (HeirloomBonus.FewerCrows, 0.05);
                case AchievementId.Golden10: return (HeirloomBonus.GoldenChance, 0.003);
                case AchievementId.ThreeStars: return (HeirloomBonus.CropValue, 0.01);
                case AchievementId.Harvests1000: return (HeirloomBonus.PassiveGrowth, 0.015);
                case AchievementId.SecondGeneration: return (HeirloomBonus.AlmanacDiscount, 0.01);
                case AchievementId.GoldenWheat: return (HeirloomBonus.CropValue, 0.015);
                case AchievementId.Goals5: return (HeirloomBonus.RingSpeeds, 0.015);
                case AchievementId.TraderDeal: return (HeirloomBonus.AlmanacDiscount, 0.01);
                case AchievementId.Pests10: return (HeirloomBonus.PassiveGrowth, 0.015);
                case AchievementId.MarketSale: return (HeirloomBonus.CropValue, 0.01);
                default: return (HeirloomBonus.CropValue, 0);
            }
        }

        public static bool Has(int bits, AchievementId id) => (bits & (1 << (int)id)) != 0;

        /// <summary>Heirlooms, then the heir's trait, then the challenge, on top of the resolved stats.</summary>
        public static void Apply(Stats s, FarmConfig cfg, int achievements, HeirTrait trait, ChallengeKind challenge, int ngPlus = 0)
        {
            double crop = 0, ring = 0, passive = 0, crows = 0, golden = 0, discount = 0;
            for (int i = 0; i < AchievementCount; i++)
            {
                if ((achievements & (1 << i)) == 0) continue;
                var (bonus, v) = Heirloom((AchievementId)i);
                switch (bonus)
                {
                    case HeirloomBonus.CropValue: crop += v; break;
                    case HeirloomBonus.RingSpeeds: ring += v; break;
                    case HeirloomBonus.PassiveGrowth: passive += v; break;
                    case HeirloomBonus.FewerCrows: crows += v; break;
                    case HeirloomBonus.GoldenChance: golden += v; break;
                    case HeirloomBonus.AlmanacDiscount: discount += v; break;
                }
            }
            switch (trait)
            {
                case HeirTrait.GreenThumb: passive += cfg.TraitGreenThumb; break;
                case HeirTrait.QuickHands: ring += cfg.TraitQuickHands; break;
                case HeirTrait.Merchant: discount += cfg.TraitMerchant; break;
                case HeirTrait.Steady: crop += cfg.TraitSteady; break;
                case HeirTrait.Shepherd: s.ApprenticeSpeed *= 1f + cfg.TraitShepherd; break;
                case HeirTrait.Watchful: crows += cfg.TraitWatchful; break;
            }
            s.CropValueMult *= 1 + crop;
            float r = 1f + (float)ring;
            s.RingWaterMult *= r;
            s.RingGrowMult *= r;
            s.RingHarvestMult *= r;
            float p = 1f + (float)passive;
            s.IrrigationFactor *= p;
            s.SunFactor *= p;
            s.CrowSpawnChance *= (float)Math.Max(0, 1 - crows);
            if (golden > 0) s.GoldenCropChance += golden;
            s.AlmanacCostMult = Math.Max(0.05, s.AlmanacCostMult * (1 - discount));

            // New Game+ (GDD §8.3 v2.4): harder years each round.
            if (ngPlus > 0)
            {
                s.CrowSpawnChance *= (float)(1 + cfg.NgPlusCrows * ngPlus);
                s.YearLength *= Math.Max(0.7f, 1f - cfg.NgPlusYear * ngPlus);
            }

            switch (challenge)
            {
                case ChallengeKind.NoHelpers:
                    s.ApprenticeCount = 0;
                    s.TractorLevel = 0;
                    break;
                case ChallengeKind.ShortYears:
                    s.YearLength *= cfg.ChallengeShortYear;
                    s.FrostWarningSeconds = Math.Min(s.FrostWarningSeconds, s.YearLength * 0.2f);
                    break;
            }
        }
    }
}
