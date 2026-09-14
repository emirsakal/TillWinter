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

    public enum Branch
    {
        Hand,
        Soil,
        Field,
        Helpers,
        Calendar,
    }

    /// <summary>What an Almanac node does. See <see cref="AlmanacData.Implemented"/> for which ones the sim applies.</summary>
    public enum EffectType
    {
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
        HelperWater,
        YearLength,
        FrostWarning,
        LateFrost,
        Greenhouse,
        CrowBounty,
        SpringHeadStart,
    }

    public enum HarvestSource
    {
        Ring,
        Apprentice,
    }

    public readonly struct HarvestEvent
    {
        public readonly GridPos Pos;
        public readonly int Tier;
        public readonly double Coins;
        public readonly HarvestSource Source;
        /// <summary>Index into <see cref="FarmState.Apprentices"/>, or -1 for the ring.</summary>
        public readonly int ApprenticeIndex;

        public HarvestEvent(GridPos pos, int tier, double coins, HarvestSource source, int apprenticeIndex)
        {
            Pos = pos;
            Tier = tier;
            Coins = coins;
            Source = source;
            ApprenticeIndex = apprenticeIndex;
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

        public PurchaseEvent(string nodeId, int level)
        {
            NodeId = nodeId;
            Level = level;
        }
    }
}
