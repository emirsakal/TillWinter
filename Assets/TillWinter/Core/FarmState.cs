using System.Collections.Generic;

namespace TillWinter.Core
{
    public sealed class Plot
    {
        public GridPos Pos { get; }
        public int Tier { get; internal set; }
        public PlotState State { get; internal set; }
        /// <summary>0..1 progress of the current state (Dry: watering, Wet: growing, Ripe: ring harvest).</summary>
        public float Progress { get; internal set; }
        public bool IsRipe => State == PlotState.Ripe;
        public bool HasCrow { get; internal set; }

        internal Plot(GridPos pos, int tier)
        {
            Pos = pos;
            Tier = tier;
            State = PlotState.Dry;
            Progress = 0f;
        }

        internal void Reset()
        {
            State = PlotState.Dry;
            Progress = 0f;
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
        public int Index { get; internal set; }
        /// <summary>Position in plot space.</summary>
        public float X { get; internal set; }
        public float Y { get; internal set; }
        /// <summary>Where it waits when nothing is Ripe.</summary>
        public float IdleX { get; internal set; }
        public float IdleY { get; internal set; }
        public bool HasTarget { get; internal set; }
        public GridPos Target { get; internal set; }
        public bool IsHarvesting { get; internal set; }
        public float HarvestProgress { get; internal set; }
        /// <summary>True while moving (toward a target or back to the idle spot).</summary>
        public bool IsWalking { get; internal set; }
    }

    /// <summary>Counters that survive a retire (GDD §7): generation, seeds, lifetime totals.</summary>
    public sealed class GenerationStats
    {
        public int Generation { get; internal set; } = 1;
        public double LifetimeCoinsThisGeneration { get; internal set; }
        public double LifetimeCoinsTotal { get; internal set; }
        public int YearsThisGeneration { get; internal set; }
        public int SeedsBanked { get; internal set; }
        public int SeedsEarnedTotal { get; internal set; }
        public int CrowsScared { get; internal set; }
        public int Harvests { get; internal set; }
    }

    /// <summary>Read-only view of the simulation for the presentation layer.</summary>
    public sealed class FarmState
    {
        public Phase Phase { get; internal set; } = Phase.Year;
        public double Coins { get; internal set; }
        public GenerationStats Generation { get; } = new GenerationStats();
        /// <summary>Heritage Seeds available to spend.</summary>
        public int Seeds => Generation.SeedsBanked;
        public int Year { get; internal set; } = 1;
        public Season Season { get; internal set; } = Season.Spring;
        /// <summary>Seconds elapsed in the current year (frozen outside <see cref="Phase.Year"/>).</summary>
        public float YearTime { get; internal set; }
        public float YearLength => Stats.YearLength;
        public bool IsWinter => Phase != Phase.Year;
        public bool FrostWarning { get; internal set; }
        public float SecondsUntilWinter => System.Math.Max(0f, YearLength - YearTime);

        public int GridSize { get; internal set; }
        internal Plot[] PlotArray;
        public IReadOnlyList<Plot> Plots => PlotArray;
        public Plot GetPlot(GridPos pos) => PlotArray[pos.Y * GridSize + pos.X];
        public Plot GetPlot(int x, int y) => PlotArray[y * GridSize + x];
        public bool InBounds(GridPos pos) => pos.X >= 0 && pos.Y >= 0 && pos.X < GridSize && pos.Y < GridSize;

        public RingInput? Ring { get; internal set; }
        /// <summary>Effective radius (debug override or <see cref="Stats"/>).</summary>
        public float RingRadius { get; internal set; }

        internal readonly List<Crow> CrowList = new List<Crow>();
        public IReadOnlyList<Crow> Crows => CrowList;

        internal readonly List<ApprenticeState> ApprenticeList = new List<ApprenticeState>();
        public IReadOnlyList<ApprenticeState> Apprentices => ApprenticeList;

        public SkillTree Almanac { get; internal set; }
        public SkillTree Heritage { get; internal set; }
        public IReadOnlyDictionary<string, int> AlmanacLevels => Almanac.Levels;
        public IReadOnlyDictionary<string, int> HeritageLevels => Heritage.Levels;
        /// <summary>Level of a node in whichever tree owns the id (0 if unknown).</summary>
        public int GetLevel(string nodeId) => Almanac.Contains(nodeId) ? Almanac.GetLevel(nodeId) : Heritage.GetLevel(nodeId);

        /// <summary>Derived numbers; recomputed after every purchase.</summary>
        public Stats Stats { get; internal set; } = new Stats();

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
