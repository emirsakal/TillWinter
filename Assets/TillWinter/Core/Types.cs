using System;

namespace TillWinter.Core
{
    /// <summary>Integer plot coordinate. (0,0) is the bottom-left plot; X grows right, Y grows "up" (away from the camera).</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => "(" + X + "," + Y + ")";
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
    }

    /// <summary>Ring centre in plot space. Plot (x,y) has its centre at exactly (x,y).</summary>
    public readonly struct RingInput
    {
        public readonly float X;
        public readonly float Y;

        public RingInput(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>GDD §4.2 (v2.0): what an apprentice does.</summary>
    public enum ApprenticeRole
    {
        /// <summary>Walks to Ripe plots and harvests them.</summary>
        Harvester = 0,
        /// <summary>Walks to Dry plots and waters them.</summary>
        Waterer = 1,
    }

    /// <summary>GDD §3.3 (v1.9): the one goal a year sets.</summary>
    public enum GoalType
    {
        None = 0,
        /// <summary>Harvest a number of one crop.</summary>
        HarvestCrop = 1,
        /// <summary>Reach a combo length.</summary>
        Combo = 2,
        /// <summary>Earn more coins than last year.</summary>
        Coins = 3,
    }

    /// <summary>GDD §5.4 (v1.9): a weather spell, at most one a year.</summary>
    public enum Weather
    {
        Clear = 0,
        /// <summary>Rain waters every Dry plot, the sun is gone, the crows hide.</summary>
        Storm = 1,
        /// <summary>Strong sun, but unshaded Wet plots dry out twice as fast.</summary>
        HeatWave = 2,
        /// <summary>Cool mist: ripe crops keep, crows cannot find the field.</summary>
        Fog = 3,
    }

    /// <summary>GDD §2.4 (v1.8): what kind of ground a plot is. New plots from a field expansion may be special.</summary>
    public enum PlotKind
    {
        Normal = 0,
        /// <summary>Rich soil: its crop sells for more.</summary>
        Fertile = 1,
        /// <summary>Grows nothing until the ring has cleared the stones off it.</summary>
        Stony = 2,
    }

    /// <summary>GDD §2.2. Each state has its own 0..1 progress.</summary>
    public enum PlotState
    {
        Dry = 0,
        Wet = 1,
        Ripe = 2,
    }

    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3,
    }

    /// <summary>Top-level game phase. Year = the sim ticks; Winter = Almanac + Heritage open; Heritage = after a retire, before Spring.</summary>
    public enum Phase
    {
        Year = 0,
        Winter = 1,
        Heritage = 2,
    }

    public enum TreeKind
    {
        Almanac,
        Heritage,
    }

    public enum Branch
    {
        Hand,
        Soil,
        Field,
        Helpers,
        Calendar,
    }

    /// <summary>What a tree node does. Every member is applied by StatResolver or a FarmSim feature switch.</summary>
    public enum EffectType
    {
        // Almanac
        RingRadius,
        RingWaterSpeed,
        RingGrowSpeed,
        RingHarvestSpeed,
        RingBonusCoins,
        RingCombo,
        Irrigation,
        Sun,
        SoilQuality,
        CropValue,
        FertileStart,
        ExpandField,
        UpgradePlot,
        UnlockTier,
        BulkUpgrade,
        ApprenticeCount,
        ApprenticeSpeed,
        ApprenticeHarvestTime,
        ApprenticeYield,
        Tractor,
        Scarecrow,
        /// <summary>The dog chases off a crow that settles (GDD §4.3 v2.0).</summary>
        FarmDog,
        /// <summary>Bees speed the Sun on the columns by the sunflowers (GDD §4.3 v2.0).</summary>
        Beehive,
        HelperWater,
        YearLength,
        FrostWarning,
        LateFrost,
        Greenhouse,
        CrowBounty,
        SpringHeadStart,
        RingShape,
        TapHarvest,

        // Heritage (GDD §7): base modifiers applied before Almanac effects
        HeritageStartRadius,
        HeritageRingSpeeds,
        HeritageRingCoins,
        HeritageStartIrrigation,
        HeritageStartSun,
        HeritageGlobalGrowth,
        UnlockRainCloud,
        HeritageStartField,
        HeritageStartTomato,
        GoldenCropChance,
        FreeApprentice,
        HeritageApprenticeYield,
        ScarecrowImmunity,
        HeritageStartYearLength,
        GreenhouseX2,
        AlmanacDiscount,
    }

    /// <summary>
    /// The ring's footprint (GDD §2.1 v1.6). Round is the classic ring; the others are unlocked by `ring_shape`
    /// and chosen by the player: a wide rake for watering rows, a cross for reaching four ways at once.
    /// </summary>
    public enum RingShape
    {
        Round,
        Rake,
        Cross,
    }

    public enum HarvestSource
    {
        Ring,
        Apprentice,
        Tractor,
        LateFrost,
    }

    public readonly struct HarvestEvent
    {
        public readonly GridPos Pos;
        public readonly int Tier;
        public readonly double Coins;
        public readonly HarvestSource Source;
        /// <summary>Index into <see cref="FarmState.Apprentices"/>, or -1 for the ring.</summary>
        public readonly int ApprenticeIndex;
        public readonly bool WasGolden;

        public HarvestEvent(GridPos pos, int tier, double coins, HarvestSource source, int apprenticeIndex, bool wasGolden = false)
        {
            Pos = pos;
            Tier = tier;
            Coins = coins;
            Source = source;
            ApprenticeIndex = apprenticeIndex;
            WasGolden = wasGolden;
        }
    }

    public readonly struct CrowEvent
    {
        public readonly GridPos Pos;
        /// <summary>Coins dropped (only for a tap-scare; 0 otherwise).</summary>
        public readonly double Coins;

        public CrowEvent(GridPos pos, double coins = 0)
        {
            Pos = pos;
            Coins = coins;
        }
    }

    public readonly struct PurchaseEvent
    {
        public readonly string NodeId;
        public readonly int Level;
        public readonly TreeKind Tree;

        public PurchaseEvent(string nodeId, int level, TreeKind tree)
        {
            NodeId = nodeId;
            Level = level;
            Tree = tree;
        }
    }

    public readonly struct RetireEvent
    {
        public readonly int SeedsEarned;
        public readonly int Generation;

        public RetireEvent(int seedsEarned, int generation)
        {
            SeedsEarned = seedsEarned;
            Generation = generation;
        }
    }

    /// <summary>Result of <see cref="FarmSim.SimulateOffline"/>.</summary>
    public readonly struct OfflineReport
    {
        public readonly double SecondsSimulated;
        public readonly double CoinsEarned;
        public readonly int Harvests;
        public readonly int HarvestsApprentice;
        public readonly int HarvestsTractor;
        public readonly bool Capped;

        public OfflineReport(double secondsSimulated, double coinsEarned, int harvests, bool capped, int harvestsApprentice = 0, int harvestsTractor = 0)
        {
            SecondsSimulated = secondsSimulated;
            CoinsEarned = coinsEarned;
            Harvests = harvests;
            Capped = capped;
            HarvestsApprentice = harvestsApprentice;
            HarvestsTractor = harvestsTractor;
        }
    }

    /// <summary>One-shot onboarding hints (GDD §10.5). Each fires once, ever; the flag lives in Core and is saved.</summary>
    public enum Hint
    {
        FirstTouch = 0,
        Hold = 1,
        FirstRipeOutside = 2,
        FirstFrost = 3,
        FirstWinter = 4,
        FirstCrow = 5,
        FirstCanRetire = 6,
        FirstHeritage = 7,
    }
}
