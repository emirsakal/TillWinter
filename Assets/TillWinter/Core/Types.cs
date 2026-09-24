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

    /// <summary>GDD §5.5 (v2.1): the pests that come from year 3.</summary>
    public enum PestKind
    {
        None = 0,
        /// <summary>Digs up the crop on its plot unless tapped (bonking it drops a crop's worth).</summary>
        Mole = 1,
        /// <summary>Eats a growing or ripe carrot unless tapped.</summary>
        Rabbit = 2,
        /// <summary>A swarm over a 3x3 patch: nothing grows there; striking inside it drives it off, or it strips the patch.</summary>
        Locusts = 3,
    }

    /// <summary>GDD §5.6 (v2.1): rare lucky moments.</summary>
    public enum LuckyKind
    {
        /// <summary>A four-leaf clover on a plot: strike, tap or reap that plot for a handful of crop values.</summary>
        Clover = 0,
        /// <summary>The hens' golden egg, left after they eat a pest.</summary>
        GoldenEgg = 1,
        /// <summary>A shooting star at the frost warning: tap it and harvests pay double for a few seconds.</summary>
        ShootingStar = 2,
    }

    /// <summary>GDD §5.7 (v2.1): what the travelling trader sells.</summary>
    public enum TraderOffer
    {
        /// <summary>One Heritage Seed for coins.</summary>
        Seed = 0,
        /// <summary>Rare seed: a few plots turn golden now.</summary>
        RareSeed = 1,
    }

    /// <summary>GDD §2v3.9: what an apprentice does. The player switches it by tapping the apprentice.</summary>
    public enum ApprenticeRole
    {
        /// <summary>Walks to Ripe plots and reaps them.</summary>
        Picker = 0,
        /// <summary>Walks to Growing plots and waters them (a burst of growth).</summary>
        Waterer = 1,
        /// <summary>Walks to Hard plots and strikes them, slowly, never a crit.</summary>
        Digger = 2,
    }

    /// <summary>GDD §3.3 (v1.9): the one goal a year sets.</summary>
    public enum GoalType
    {
        None = 0,
        /// <summary>Reap a number of one crop.</summary>
        HarvestCrop = 1,
        /// <summary>Reap this many crops in one swipe.</summary>
        Combo = 2,
        /// <summary>Earn more coins than last year.</summary>
        Coins = 3,
    }

    /// <summary>GDD §5.4 (v1.9, re-themed v3): a weather spell, at most one a year.</summary>
    public enum Weather
    {
        Clear = 0,
        /// <summary>Rain grows every crop faster and softens the ground; the crows hide.</summary>
        Storm = 1,
        /// <summary>Strong sun: crops grow slower and the ground bakes hard.</summary>
        HeatWave = 2,
        /// <summary>Cool mist: ripe crops keep, crows cannot find the field.</summary>
        Fog = 3,
    }

    /// <summary>GDD §2.4 (v1.8): what kind of ground a plot is. New plots from a field expansion may be fertile.</summary>
    public enum PlotKind
    {
        Normal = 0,
        /// <summary>Rich soil: its crop sells for more.</summary>
        Fertile = 1,
    }

    /// <summary>GDD §2v3.3: the layer's material, by depth. Visible on the plot; the reward inside is not.</summary>
    public enum GroundType
    {
        Clay = 0,
        Stone = 1,
        Roots = 2,
        Gravel = 3,
        Rock = 4,
    }

    /// <summary>GDD §2v3.2. Hard ground with HP, then a growing crop (0..1 Progress), then a ripe one waiting for a swipe.</summary>
    public enum PlotState
    {
        Hard = 0,
        Growing = 1,
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

    /// <summary>The five branches of both trees. Hand is the hoe branch since v3 (the string tables name it).</summary>
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
        // Almanac — the hoe (GDD §2v3.4–5)
        StrikeDamage,
        CritWindow,
        CritChance,
        StrikeSpeed,
        Splash,
        StaminaMax,
        StaminaRegen,
        BreakBonus,
        ReapCombo,
        // Almanac — soil
        Growth,
        Softness,
        SoilQuality,
        CropValue,
        EarlyThaw,
        // Almanac — field
        ExpandField,
        UpgradePlot,
        UnlockTier,
        BulkUpgrade,
        // Almanac — helpers
        ApprenticeCount,
        ApprenticeSpeed,
        ApprenticeWorkTime,
        ApprenticeYield,
        ApprenticeDig,
        Tractor,
        Scarecrow,
        /// <summary>The dog chases off a crow that settles (GDD §4.3 v2.0).</summary>
        FarmDog,
        /// <summary>Bees speed growth on the columns by the sunflowers (GDD §4.3 v2.0).</summary>
        Beehive,
        /// <summary>Hens eat pests, and now and then lay a golden egg (GDD §4.3/§5.5 v2.1).</summary>
        Hens,
        /// <summary>A barn that keeps part of the harvest for the winter market (GDD §3.5 v2.2).</summary>
        Barn,
        // Almanac — calendar
        YearLength,
        FrostWarning,
        LateFrost,
        Greenhouse,
        CrowBounty,
        SpringHeadStart,

        // Heritage (GDD §7): base modifiers applied before Almanac effects
        HeritageStartDamage,
        HeritageStrikeSpeed,
        HeritageBreakCoins,
        HeritageStartGrowth,
        HeritageStartSoft,
        HeritageGlobalGrowth,
        UnlockRainCloud,
        HeritageStartField,
        HeritageStartTomato,
        GoldenCropChance,
        FreeApprentice,
        HeritageApprenticeYield,
        ScarecrowImmunity,
        HeritageStartYearLength,
        /// <summary>Strike speed and the break bonus together: the active-play path (GDD §7.3 v2.8, re-themed v3).</summary>
        HeritageHoeMaster,
        /// <summary>Years longer, and the year-length ceiling raised by the same amount (GDD §7.3 v2.8).</summary>
        LongSummer,
        GreenhouseX2,
        AlmanacDiscount,
    }

    public enum HarvestSource
    {
        /// <summary>The player's swipe or tap.</summary>
        Hand,
        Apprentice,
        Tractor,
        LateFrost,
        /// <summary>The frost reaps what stands ripe at the end of the year (GDD §2v3.2 v3.2).</summary>
        Frost,
    }

    public readonly struct HarvestEvent
    {
        public readonly GridPos Pos;
        public readonly int Tier;
        public readonly double Coins;
        public readonly HarvestSource Source;
        /// <summary>Index into <see cref="FarmState.Apprentices"/>, or -1 for the hand.</summary>
        public readonly int ApprenticeIndex;
        public readonly bool WasGolden;
        /// <summary>Crops in the swipe this one was part of (1 for a tap or a helper).</summary>
        public readonly int Combo;

        public HarvestEvent(GridPos pos, int tier, double coins, HarvestSource source, int apprenticeIndex, bool wasGolden = false, int combo = 1)
        {
            Pos = pos;
            Tier = tier;
            Coins = coins;
            Source = source;
            ApprenticeIndex = apprenticeIndex;
            WasGolden = wasGolden;
            Combo = combo;
        }
    }

    /// <summary>One strike landed (GDD §2v3.4): where, how hard, and whether it was a crit or a tired swing.</summary>
    public readonly struct StrikeEvent
    {
        public readonly GridPos Pos;
        public readonly double Damage;
        public readonly bool Crit;
        public readonly bool Tired;
        /// <summary>True for the splash on a neighbour, not the strike itself.</summary>
        public readonly bool Splash;
        /// <summary>Who struck: -1 for the player's hoe, else the apprentice index.</summary>
        public readonly int ApprenticeIndex;

        public StrikeEvent(GridPos pos, double damage, bool crit, bool tired, bool splash, int apprenticeIndex)
        {
            Pos = pos;
            Damage = damage;
            Crit = crit;
            Tired = tired;
            Splash = splash;
            ApprenticeIndex = apprenticeIndex;
        }
    }

    /// <summary>Hard ground broke (GDD §2v3.3): the layer, the coins it hid, and whether it was a chest or golden hardpan.</summary>
    public readonly struct BreakEvent
    {
        public readonly GridPos Pos;
        public readonly int Layer;
        public readonly double Coins;
        public readonly bool Chest;
        public readonly bool Hardpan;

        public BreakEvent(GridPos pos, int layer, double coins, bool chest, bool hardpan)
        {
            Pos = pos;
            Layer = layer;
            Coins = coins;
            Chest = chest;
            Hardpan = hardpan;
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
        /// <summary>Heirlooms (achievements) earned while away (v2.8): they applied from then on, and the card says so.</summary>
        public readonly int HeirloomsFound;

        public OfflineReport(double secondsSimulated, double coinsEarned, int harvests, bool capped, int harvestsApprentice = 0, int harvestsTractor = 0, int heirloomsFound = 0)
        {
            HeirloomsFound = heirloomsFound;
            SecondsSimulated = secondsSimulated;
            CoinsEarned = coinsEarned;
            Harvests = harvests;
            Capped = capped;
            HarvestsApprentice = harvestsApprentice;
            HarvestsTractor = harvestsTractor;
        }
    }

    /// <summary>One-shot onboarding hints (GDD §10.5, v3 set). Each fires once, ever; the flag lives in Core and is saved.</summary>
    public enum Hint
    {
        /// <summary>Tap hard ground to strike it.</summary>
        FirstTouch = 0,
        /// <summary>Hold a growing crop to water it.</summary>
        Hold = 1,
        /// <summary>Swipe over ripe crops to reap them.</summary>
        FirstRipe = 2,
        FirstFrost = 3,
        FirstWinter = 4,
        FirstCrow = 5,
        FirstCanRetire = 6,
        FirstHeritage = 7,
        /// <summary>Strike on the beat for a certain crit.</summary>
        Beat = 8,
    }
}
