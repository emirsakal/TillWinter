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
        /// <summary>Seconds a Wet plot has stood with nothing growing it in a drought (GDD §3.2 v1.9).</summary>
        public float DryTimer { get; internal set; }
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
            DryTimer = 0f;
        }
    }

    /// <summary>This year's goal (GDD §3.3 v1.9): what it asks, how far it has got, what it pays.</summary>
    public sealed class YearGoal
    {
        public GoalType Type { get; internal set; }
        /// <summary>The crop a HarvestCrop goal asks for.</summary>
        public int Tier { get; internal set; }
        public double Target { get; internal set; }
        public double Progress { get; internal set; }
        public bool Done { get; internal set; }
        public double Reward { get; internal set; }
        public bool Active => Type != GoalType.None;

        internal void Clear()
        {
            Type = GoalType.None;
            Tier = 0;
            Target = Progress = Reward = 0;
            Done = false;
        }
    }

    /// <summary>The barn (GDD §3.5 v2.2): what is stored, at what share, the winter's market price and the jars put up.</summary>
    public sealed class BarnState
    {
        /// <summary>Coins the stored crops were worth when harvested.</summary>
        public double Stock { get; internal set; }
        /// <summary>How many crops are stored (against the barn's capacity).</summary>
        public int Count { get; internal set; }
        /// <summary>Share of harvests that go to the barn: one of <see cref="FarmConfig.StoreShares"/>.</summary>
        public float StoreShare { get; internal set; }
        /// <summary>Running fraction: each harvest adds the share; a whole one sends that harvest to the barn.</summary>
        public float StoreAcc { get; internal set; }
        /// <summary>This winter's market price, a multiple of the stored value.</summary>
        public double MarketPrice { get; internal set; } = 1;
        /// <summary>Preserves put up this winter, paid out in spring.</summary>
        public double Jars { get; internal set; }
    }

    /// <summary>The one pest on the field, if any (GDD §5.5 v2.1).</summary>
    public sealed class PestState
    {
        public PestKind Kind { get; internal set; }
        public GridPos Pos { get; internal set; }
        /// <summary>Seconds it has been there.</summary>
        public float Timer { get; internal set; }
        /// <summary>0..1: how far the ring has driven a locust swarm off.</summary>
        public float Shoo { get; internal set; }
    }

    /// <summary>Lucky moments under way (GDD §5.6 v2.1).</summary>
    public sealed class LuckState
    {
        public GridPos CloverPos { get; internal set; }
        /// <summary>Seconds the clover has left; 0 = none.</summary>
        public float CloverLeft { get; internal set; }
        /// <summary>Seconds the shooting star is still in the sky; 0 = none.</summary>
        public float StarLeft { get; internal set; }
        /// <summary>Seconds of the star's double-pay rush left.</summary>
        public float RushLeft { get; internal set; }
    }

    /// <summary>The travelling trader (GDD §5.7 v2.1).</summary>
    public sealed class TraderState
    {
        public bool Active { get; internal set; }
        public float TimeLeft { get; internal set; }
        /// <summary>Year seconds it arrives this year, or -1.</summary>
        public float PlannedTime { get; internal set; } = -1f;
        public double SeedPrice { get; internal set; }
        public double RarePrice { get; internal set; }
        public bool SeedSold { get; internal set; }
        public bool RareSold { get; internal set; }
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
        /// <summary>Harvester or waterer (GDD §4.2 v2.0); the player switches it by tapping the apprentice.</summary>
        public ApprenticeRole Role { get; internal set; }
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
        /// <summary>Coins spent in the Almanac this generation, and whether its one free respec is used (GDD §6.3 v2.2).</summary>
        public double AlmanacSpent { get; internal set; }
        public bool RespecUsed { get; internal set; }
        /// <summary>M.7 (GDD §7.4–§7.6 v2.3): this generation's heir trait, the three heirs offered at the last rebirth,
        /// the chosen challenge, the achievements earned (bits, never reset) and the counters two of them need.</summary>
        public HeirTrait Trait { get; internal set; }
        public HeirTrait[] HeirOffer { get; } = new HeirTrait[3];
        public ChallengeKind Challenge { get; internal set; }
        public int Achievements { get; internal set; }
        public int GoalsMet { get; internal set; }
        public int PestsStopped { get; internal set; }
        /// <summary>M.8 (GDD §8.2 v2.4): the best year's stars this generation, and the harvest count when it began (for its album page).</summary>
        public int BestGradeThisGeneration { get; internal set; }
        public int HarvestsAtGenerationStart { get; internal set; }
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
        /// <summary>Sum of each harvest's freshness this year; with the crops lost to crows it makes the grade (GDD §3.4 v1.9).</summary>
        public double YearFreshSum { get; internal set; }
        public int CropsLostThisYear { get; internal set; }
        /// <summary>Stars (1–3) the last finished year earned, 0 before any, and the coins they paid.</summary>
        public int LastGrade { get; internal set; }
        public double LastGradeBonus { get; internal set; }
        /// <summary>What the last finished year earned; goals and their rewards scale from it.</summary>
        public double LastYearCoins { get; internal set; }
        public YearGoal Goal { get; } = new YearGoal();
        /// <summary>The weather now (GDD §5.4 v1.9) and seconds it has left.</summary>
        public Weather Weather { get; internal set; }
        public float WeatherLeft { get; internal set; }
        /// <summary>The spell this year still has coming, and when (year seconds).</summary>
        public Weather PlannedWeather { get; internal set; }
        public float PlannedWeatherTime { get; internal set; }
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
        /// <summary>
        /// Placed scarecrows (GDD §5.1 v2.0), one per `scarecrow` level, each on a plot corner: corner (cx, cy) is the
        /// point (cx - 0.5, cy - 0.5) in plot space, so 0..GridSize on both axes.
        /// </summary>
        internal readonly List<GridPos> ScarecrowList = new List<GridPos>();
        public IReadOnlyList<GridPos> Scarecrows => ScarecrowList;
        /// <summary>Seconds until the farm dog can chase again (GDD §4.3 v2.0).</summary>
        public float DogCooldown { get; internal set; }
        /// <summary>M.5 (GDD §5.5–§5.7 v2.1): the pest, lucky moments, the trader, and the hens' rest.</summary>
        public PestState Pest { get; } = new PestState();
        public LuckState Luck { get; } = new LuckState();
        public TraderState Trader { get; } = new TraderState();
        public float HenCooldown { get; internal set; }
        public BarnState Barn { get; } = new BarnState();
        /// <summary>The family album, one page per generation handed on (GDD §8.2 v2.4).</summary>
        internal readonly List<AlbumEntry> AlbumList = new List<AlbumEntry>();
        public IReadOnlyList<AlbumEntry> Album => AlbumList;
        /// <summary>The New Game+ round, 0 before the first (GDD §8.3 v2.4).</summary>
        public int NgPlus { get; internal set; }
        /// <summary>The daily farm's day (yyyymmdd), 0 for the family farm. The daily farm is never saved (GDD §8.4 v2.4).</summary>
        public int Daily { get; internal set; }
        public bool IsDaily => Daily != 0;
        /// <summary>M.9 (GDD §10.5/§10.7 v2.5): the first generation's checklist (bits) and the away plan.</summary>
        public int ChecklistBits { get; internal set; }
        public AwayPlan AwayPlan { get; internal set; }
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
