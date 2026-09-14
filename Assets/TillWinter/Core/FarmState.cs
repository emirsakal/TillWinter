using System.Collections.Generic;

namespace TillWinter.Core
{
    public sealed class Plot
    {
        public GridPos Pos { get; internal set; }
        public CropTier Tier { get; internal set; }
        /// <summary>0..1; clamped to 1 while the plot waits ripe outside the ring.</summary>
        public float Growth { get; internal set; }
        public bool IsRipe => Growth >= 1f;
        public bool HasCrow { get; internal set; }

        internal Plot(GridPos pos, CropTier tier)
        {
            Pos = pos;
            Tier = tier;
            Growth = 0f;
        }
    }

    public sealed class Crow
    {
        public GridPos Pos { get; internal set; }
        /// <summary>Seconds the crow has been on the plot.</summary>
        public float Timer { get; internal set; }
        public float EatTime { get; internal set; }
        public float Progress => EatTime <= 0f ? 1f : Timer / EatTime;
    }

    public sealed class ApprenticeState
    {
        public bool Owned { get; internal set; }
        /// <summary>Position in plot space.</summary>
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public bool HasTarget { get; internal set; }
        public GridPos Target { get; internal set; }
        public bool IsHarvesting { get; internal set; }
        public float HarvestProgress { get; internal set; }
        public bool IsWalking => Owned && HasTarget && !IsHarvesting;
    }

    /// <summary>Read-only view of the simulation for the presentation layer.</summary>
    public sealed class FarmState
    {
        public double Coins { get; internal set; }
        public int Year { get; internal set; } = 1;
        public Season Season { get; internal set; } = Season.Spring;
        /// <summary>Seconds elapsed in the current year (frozen in Winter).</summary>
        public float YearTime { get; internal set; }
        public float YearLength { get; internal set; }
        public bool IsWinter => Season == Season.Winter;
        public bool FrostWarning { get; internal set; }
        public float SecondsUntilWinter => System.Math.Max(0f, YearLength - YearTime);

        public int GridSize { get; internal set; }
        internal Plot[] PlotArray;
        public IReadOnlyList<Plot> Plots => PlotArray;
        public Plot GetPlot(GridPos pos) => PlotArray[pos.Y * GridSize + pos.X];
        public Plot GetPlot(int x, int y) => PlotArray[y * GridSize + x];
        public bool InBounds(GridPos pos) => pos.X >= 0 && pos.Y >= 0 && pos.X < GridSize && pos.Y < GridSize;

        public RingInput? Ring { get; internal set; }
        public float RingRadius { get; internal set; }

        internal readonly List<Crow> CrowList = new List<Crow>();
        public IReadOnlyList<Crow> Crows => CrowList;

        public ApprenticeState Apprentice { get; } = new ApprenticeState();

        internal readonly Dictionary<UpgradeId, int> LevelMap = new Dictionary<UpgradeId, int>();
        public int GetLevel(UpgradeId id) => LevelMap.TryGetValue(id, out var l) ? l : 0;

        /// <summary>Is the centre of this plot inside the ring right now?</summary>
        public bool IsUnderRing(GridPos pos)
        {
            if (Ring == null) return false;
            var r = Ring.Value;
            float dx = pos.X - r.X;
            float dy = pos.Y - r.Y;
            return dx * dx + dy * dy <= RingRadius * RingRadius;
        }
    }
}
