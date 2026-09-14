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

    public enum CropTier
    {
        Carrot = 0,
        Tomato = 1,
        Corn = 2,
    }

    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3,
    }

    public enum UpgradeId
    {
        ExpandField,
        UpgradePlot,
        Soil,
        Irrigation,
        RingRadius,
        Calendar,
        Apprentice,
        Scarecrow,
    }

    public enum HarvestSource
    {
        Ring,
        Apprentice,
    }

    public readonly struct HarvestEvent
    {
        public readonly GridPos Pos;
        public readonly CropTier Tier;
        public readonly double Coins;
        public readonly HarvestSource Source;

        public HarvestEvent(GridPos pos, CropTier tier, double coins, HarvestSource source)
        {
            Pos = pos;
            Tier = tier;
            Coins = coins;
            Source = source;
        }
    }

    public readonly struct CrowEvent
    {
        public readonly GridPos Pos;

        public CrowEvent(GridPos pos)
        {
            Pos = pos;
        }
    }
}
