using System.Collections.Generic;

namespace TillWinter.Core
{
    public sealed class Plot
    {
        public GridPos Pos { get; }
        /// <summary>Highest crop this bed can grow; <c>upgrade_plot</c> raises it (GDD §2.4 v1.7).</summary>
        public int BedTier { get; internal set; }
        /// <summary>The crop picked from the seed bag, or -1 to follow the bed (GDD §2.3 v1.7).</summary>
        public int Choice { get; internal set; } = -1;
        /// <summary>The crop in the ground: the chosen one, never above what the bed can grow.</summary>
        public int Tier => Choice < 0 ? BedTier : System.Math.Min(Choice, BedTier);
        public PlotState State { get; internal set; }
        /// <summary>0..1 progress of the current state (Dry: watering, Wet: growing, Ripe: ring harvest).</summary>
        public float Progress { get; internal set; }
        public bool IsRipe => State == PlotState.Ripe;
        public bool HasCrow { get; internal set; }
        /// <summary>Golden crop: same timings, 10x value (GDD §5.3).</summary>
        public bool IsGolden { get; internal set; }
        /// <summary>Seconds this plot has stood Ripe; past the grace the crop is worth less (GDD §2.5 v1.6).</summary>
        public float RipeAge { get; internal set; }
        /// <summary>Fertile, stony or plain ground (GDD §2.4 v1.8). A stony plot's Progress is how far its clearing has got.</summary>
        public PlotKind Kind { get; internal set; }
        /// <summary>The crop this plot grew last year, or -1; a different crop this year is a rotation (GDD §2.3 v1.8).</summary>
        public int LastYearTier { get; internal set; } = -1;
        public bool IsStony => Kind == PlotKind.Stony;
        /// <summary>Growing something other than last year's crop.</summary>
        public bool IsRotated => LastYearTier >= 0 && Tier != LastYearTier;

        internal Plot(GridPos pos, int tier)
        {
            Pos = pos;
            BedTier = tier;
            State = PlotState.Dry;
            Progress = 0f;
        }

        internal void Reset()
        {
            State = PlotState.Dry;
            Progress = 0f;
            IsGolden = false;
            RipeAge = 0f;
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

    /// <summary>Rain cloud (GDD §5.2): drifts across the top of the field once per Summer when unlocked.</summary>
    public sealed class CloudState
    {
        public bool Active { get; internal set; }
        /// <summary>0..1 across the field width.</summary>
        public float X { get; internal set; }
        public float TimeLeft { get; internal set; }
        public bool SpawnedThisYear { get; internal set; }
        /// <summary>Year time at which this year's cloud appears (chosen at Spring).</summary>
        public float SpawnTime { get; internal set; }
    }

    /// <summary>Tractor (GDD §4): one unit sweeping the row with the most Ripe plots.</summary>
    public sealed class TractorState
    {
        public bool Owned { get; internal set; }
        public int Row { get; internal set; }
        /// <summary>Plot units along the row while sweeping.</summary>
        public float X { get; internal set; }
        public bool Sweeping { get; internal set; }
        public float TimeToNextSweep { get; internal set; }
        internal int Passed;
    }

    /// <summary>Greenhouse (GDD §4): coins per second during Winter, capped per winter.</summary>
    public sealed class GreenhouseState
    {
        public float SecondsLeftThisWinter { get; internal set; }
        public double CoinsThisWinter { get; internal set; }
        public double CoinsPerSecond { get; internal set; }
    }

    /// <summary>Which one-shot hints have been shown. Bit per <see cref="Hint"/>; never reset (not even by retire).</summary>
    public sealed class OnboardingFlags
    {
        public int Bits { get; internal set; }
        public bool Has(Hint h) => (Bits & (1 << (int)h)) != 0;
        internal void Set(Hint h) => Bits |= 1 << (int)h;
        public int Count
        {
            get { int c = 0, b = Bits; while (b != 0) { c += b & 1; b >>= 1; } return c; }
        }
    }

    /// <summary>Last pan/zoom of a tree canvas (cosmetic, saved so "first open centres on roots" survives a relaunch).</summary>
    public sealed class TreeViewMemory
    {
        public bool HasView { get; internal set; }
        public float PanX { get; internal set; }
        public float PanY { get; internal set; }
        public float Zoom { get; internal set; } = 1f;
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
        // v4: stats screen (never reset by a retire)
        public int HarvestsRing { get; internal set; }
        public int HarvestsApprentice { get; internal set; }
        public int HarvestsTractor { get; internal set; }
        public int HarvestsLateFrost { get; internal set; }
        public int GoldenHarvests { get; internal set; }
        public int BestCombo { get; internal set; }
        /// <summary>Real seconds the game was open and not paused (fed by the presentation layer through FarmSim.AddPlayTime).</summary>
        public double TimePlayedSeconds { get; internal set; }
        /// <summary>Years played across every generation.</summary>
        public int YearsTotal { get; internal set; }
    }

    /// <summary>Read-only view of the simulation for the presentation layer.</summary>
    public sealed class FarmState
    {
        public Phase Phase { get; internal set; } = Phase.Year;
        public double Coins { get; internal set; }
        public GenerationStats Generation { get; } = new GenerationStats();
        /// <summary>The Golden Year has been played (GDD §8); it happens once.</summary>
        public bool EndingSeen { get; internal set; }
        /// <summary>This year is the Golden Year: 6×6 golden wheat, no crows, no frost, a long year.</summary>
        public bool GoldenYearActive { get; internal set; }
        /// <summary>Heritage Seeds available to spend.</summary>
        public int Seeds => Generation.SeedsBanked;
        public int Year { get; internal set; } = 1;
        public Season Season { get; internal set; } = Season.Spring;
        /// <summary>Seconds elapsed in the current year (frozen outside <see cref="Phase.Year"/>).</summary>
        public float YearTime { get; internal set; }
        public float YearLength => Stats.YearLength;
        public bool IsWinter => Phase != Phase.Year;
        public bool FrostWarning { get; internal set; }
        /// <summary>Coins earned since Spring; the Winter screen shows the year's take. Reset every year.</summary>
        public double CoinsThisYear { get; internal set; }
        /// <summary>Harvests since Spring (all sources). Reset every year.</summary>
        public int HarvestsThisYear { get; internal set; }
        public float SecondsUntilWinter => System.Math.Max(0f, YearLength - YearTime);

        public int GridSize { get; internal set; }
        internal Plot[] PlotArray;
        public IReadOnlyList<Plot> Plots => PlotArray;
        public Plot GetPlot(GridPos pos) => PlotArray[pos.Y * GridSize + pos.X];
        public Plot GetPlot(int x, int y) => PlotArray[y * GridSize + x];
        public bool InBounds(GridPos pos) => pos.X >= 0 && pos.Y >= 0 && pos.X < GridSize && pos.Y < GridSize;

        public RingInput? Ring { get; internal set; }
        /// <summary>The ring's footprint; Rake and Cross need `ring_shape` (GDD §2.1 v1.6).</summary>
        public RingShape RingShape { get; internal set; } = RingShape.Round;
        /// <summary>0..1: how much the ring is "flowing" (moving), which speeds it up.</summary>
        public float Flow { get; internal set; }
        /// <summary>Seconds until the next tap harvest is allowed (`tap_harvest`).</summary>
        public float TapCooldown { get; internal set; }
        /// <summary>Effective radius (debug override or <see cref="Stats"/>).</summary>
        public float RingRadius { get; internal set; }

        internal readonly List<Crow> CrowList = new List<Crow>();
        public IReadOnlyList<Crow> Crows => CrowList;

        internal readonly List<ApprenticeState> ApprenticeList = new List<ApprenticeState>();
        public IReadOnlyList<ApprenticeState> Apprentices => ApprenticeList;

        public CloudState Cloud { get; } = new CloudState();
        public TractorState Tractor { get; } = new TractorState();
        public GreenhouseState Greenhouse { get; } = new GreenhouseState();
        /// <summary>Consecutive ring harvests within the combo window (ring_combo).</summary>
        public int Combo { get; internal set; }
        /// <summary>Seconds since the last ring harvest (combo window is 1 s).</summary>
        public float ComboTimer { get; internal set; }

        public OnboardingFlags Onboarding { get; } = new OnboardingFlags();
        public TreeViewMemory AlmanacView { get; } = new TreeViewMemory();
        public TreeViewMemory HeritageView { get; } = new TreeViewMemory();

        public SkillTree Almanac { get; internal set; }
        public SkillTree Heritage { get; internal set; }
        public IReadOnlyDictionary<string, int> AlmanacLevels => Almanac.Levels;
        public IReadOnlyDictionary<string, int> HeritageLevels => Heritage.Levels;
        /// <summary>Level of a node in whichever tree owns the id (0 if unknown).</summary>
        public int GetLevel(string nodeId) => Almanac.Contains(nodeId) ? Almanac.GetLevel(nodeId) : Heritage.GetLevel(nodeId);

        /// <summary>Derived numbers; recomputed after every purchase.</summary>
        public Stats Stats { get; internal set; } = new Stats();

        /// <summary>Shape metrics, set from <see cref="FarmConfig"/> when the sim is built.</summary>
        internal float RakeLength = 1.7f, RakeWidth = 0.5f, CrossLength = 1.5f, CrossWidth = 0.42f;

        /// <summary>Is the centre of this plot inside the ring right now (whatever shape it has)?</summary>
        public bool IsUnderRing(GridPos pos)
        {
            if (Ring == null) return false;
            var r = Ring.Value;
            float dx = pos.X - r.X;
            float dy = pos.Y - r.Y;
            float radius = RingRadius;
            switch (RingShape)
            {
                case RingShape.Rake:
                    return Inside(dx, dy, radius * RakeLength, radius * RakeWidth);
                case RingShape.Cross:
                    return Inside(dx, dy, radius * CrossLength, radius * CrossWidth)
                        || Inside(dx, dy, radius * CrossWidth, radius * CrossLength);
                default:
                    return dx * dx + dy * dy <= radius * radius;
            }
        }

        private static bool Inside(float dx, float dy, float halfX, float halfY)
        {
            float nx = dx / halfX, ny = dy / halfY;
            return nx * nx + ny * ny <= 1f;
        }
    }
}
