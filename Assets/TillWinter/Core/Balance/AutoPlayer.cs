using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TillWinter.Core.Balance
{
    /// <summary>One row of the balance table: one year of one generation.</summary>
    public sealed class YearRow
    {
        public int Generation, Year;
        public double CoinsThisYear, TotalCoins;
        public int Seeds, NodesBought, Apprentices, FieldSize, TopTier;
        public float RingRadius, FirstRipeTime;
        public int HarvestsRing, HarvestsApprentice, HarvestsTractor, HarvestsLateFrost;
        public int TotalHarvests => HarvestsRing + HarvestsApprentice + HarvestsTractor + HarvestsLateFrost;
        public double SimSecondsEnd;
    }

    /// <summary>The GDD §15 targets measured on one run (Session 9). -1 = never happened.</summary>
    public sealed class BalanceSummary
    {
        public double Year1Coins;
        public int Year1RootNodesBought;
        public int FirstApprenticeYear = -1;
        public int FirstCanRetireYear = -1;
        public int SeedsAtFirstRetire = -1;
        public int GenerationsToMaxHeritage = -1;
        public double SimSecondsToEnding = -1;
        public double RingShareYear1, RingShareAtFirstRetire, RingShareGen4 = -1;
        public double MaxNodeSpendShare;
        public string MaxNodeSpendId = "";
        public int MaxNodeSpendGeneration;
        /// <summary>The open-ended sink (upgrade_plot, no max level) is reported separately: it absorbs leftover coins by design.</summary>
        public double MaxSinkShare;
        public bool ReachedEnding;
    }

    /// <summary>
    /// Scripted player that runs <see cref="FarmSim"/> headlessly for balance tables (no Unity).
    /// Ring: highest-urgency plot with a reaction delay and a max ring speed. Winter: greedy
    /// weight/cost purchases; retires once this generation's seeds reach <see cref="RetireGrowth"/> × every seed
    /// earned before (at least <see cref="RetireAtSeeds"/>), then greedy Heritage. Taps crows and clouds.
    /// </summary>
    public sealed class AutoPlayer
    {
        public float ReactionDelay = 0.3f;
        public float MaxRingSpeed = 6f;
        /// <summary>Never retire below this many seeds.</summary>
        public int RetireAtSeeds = 8;
        /// <summary>Retire once this generation's seeds reach this multiple of every seed earned before it.</summary>
        public double RetireGrowth = 1.5;
        public float Dt = 0.05f;
        public int MaxTicks = 2_000_000;

        /// <summary>Static desirability per node id; missing = 1. Greedy score = weight / cost.</summary>
        public Dictionary<string, double> Weights = new Dictionary<string, double>
        {
            ["irrigation"] = 3, ["sun"] = 3, ["ring_radius"] = 2.5, ["apprentice_count"] = 2.5, ["expand_field"] = 2,
            ["unlock_tomato"] = 2, ["upgrade_plot"] = 1.5, ["soil_quality"] = 1.5, ["ring_water_speed"] = 1.2, ["ring_grow_speed"] = 1.2,
            ["year_length"] = 1.5, ["scarecrow"] = 0.8, ["farm_dog"] = 0.6, ["beehive"] = 0.6, ["tractor"] = 1.2, ["greenhouse"] = 0.8, ["crow_bounty"] = 0.4,
            ["ring_combo"] = 0.5, ["ring_shape"] = 0.6, ["tap_harvest"] = 0.6, ["late_frost"] = 0.6, ["frost_warning"] = 0.3, ["helper_water"] = 0.7, ["spring_head_start"] = 0.7,
            ["h_start_field"] = 3, ["h_free_apprentice"] = 3, ["h_start_irrigation"] = 2.5, ["h_start_sun"] = 2.5, ["h_start_radius"] = 2,
            ["h_global_growth"] = 1.5, ["h_almanac_discount"] = 1.2, ["h_ring_speeds"] = 1.2, ["h_unlock_rain_cloud"] = 1, ["h_golden_crop"] = 1,
            // S9: every node has a weight; crop unlocks are weighted by the value jump they bring.
            ["unlock_corn"] = 5, ["unlock_pumpkin"] = 6, ["unlock_grapes"] = 7, ["unlock_golden_wheat"] = 8, ["bulk_upgrade"] = 2,
            ["crop_value"] = 1.5, ["fertile_start"] = 0.5, ["ring_harvest_speed"] = 1, ["ring_bonus_coins"] = 1,
            ["apprentice_speed"] = 1.5, ["apprentice_harvest_time"] = 1.5, ["apprentice_yield"] = 2,
            ["h_ring_coins"] = 1, ["h_start_tomato"] = 2, ["h_apprentice_yield"] = 1.5, ["h_scarecrow_immunity"] = 0.8,
            ["h_start_year_length"] = 1.5, ["h_greenhouse_x2"] = 0.8,
        };

        public FarmSim Sim { get; }
        public List<YearRow> Rows { get; } = new List<YearRow>();
        public BalanceSummary Summary { get; } = new BalanceSummary();
        /// <summary>Sim seconds played (years plus greenhouse winters).</summary>
        public double SimSeconds { get; private set; }

        private readonly Rng _rng;
        private YearRow _row;
        private double _coinsAtYearStart;
        private float _yearClock;
        private float _sinceDecision;
        private GridPos? _target;
        private float _ringX, _ringY;
        private readonly Dictionary<GridPos, float> _crowSeen = new Dictionary<GridPos, float>();
        private readonly List<GridPos> _crowKeys = new List<GridPos>();
        private float _cloudSeen = -1f;
        private int _boughtThisWinter;
        private readonly Dictionary<string, double> _spend = new Dictionary<string, double>();
        private double _spendTotal;

        public AutoPlayer(FarmSim sim, int seed)
        {
            Sim = sim;
            _rng = new Rng(seed);
            _ringX = (sim.State.GridSize - 1) * 0.5f;
            _ringY = _ringX;
            sim.Harvested += OnHarvested;
            sim.CrowLanded += e => _crowSeen[e.Pos] = 0f;
            sim.RainCloudAppeared += () => _cloudSeen = 0f;
            sim.PlotRipened += _ => { if (_row != null && _row.FirstRipeTime < 0f) _row.FirstRipeTime = _yearClock; };
            StartRow();
        }

        /// <summary>Runs until <paramref name="generations"/> retirements have happened (or the tick budget runs out).</summary>
        public void Run(int generations)
        {
            int ticks = 0;
            int startGen = Sim.State.Generation.Generation;
            while (Sim.State.Generation.Generation < startGen + generations && ticks < MaxTicks)
            {
                Step();
                ticks++;
            }
        }

        /// <summary>Runs until the Golden Year has been played (GDD §8) or the tick budget runs out.</summary>
        public void RunToEnding()
        {
            int ticks = 0;
            while (!Sim.State.EndingSeen && ticks < MaxTicks)
            {
                Step();
                ticks++;
            }
            Summary.ReachedEnding = Sim.State.EndingSeen;
            if (Summary.ReachedEnding) Summary.SimSecondsToEnding = SimSeconds;
            FinishGenerationSpend();
        }

        /// <summary>One tick of play (Year) or one winter/heritage decision.</summary>
        public void Step()
        {
            var s = Sim.State;
            switch (s.Phase)
            {
                case Phase.Year: TickYear(); break;
                case Phase.Winter: DoWinter(); break;
                case Phase.Heritage: DoHeritage(); break;
            }
        }

        private void TickYear()
        {
            var s = Sim.State;
            _yearClock += Dt;
            _sinceDecision += Dt;
            if (_sinceDecision >= ReactionDelay)
            {
                _sinceDecision = 0f;
                _target = PickTarget();
                if (_crowSeen.Count > 0)
                {
                    _crowKeys.Clear();
                    _crowKeys.AddRange(_crowSeen.Keys);
                    foreach (var pos in _crowKeys)
                    {
                        _crowSeen[pos] += ReactionDelay;
                        if (_crowSeen[pos] >= ReactionDelay && s.InBounds(pos) && s.GetPlot(pos).HasCrow) Sim.TapAt(pos);
                        if (!s.InBounds(pos) || !s.GetPlot(pos).HasCrow) _crowSeen.Remove(pos);
                    }
                }
                if (s.Cloud.Active)
                {
                    _cloudSeen += ReactionDelay;
                    if (_cloudSeen >= ReactionDelay) Sim.TapCloud();
                }
            }
            RingInput? ring = null;
            if (_target.HasValue)
            {
                float tx = _target.Value.X, ty = _target.Value.Y;
                float dx = tx - _ringX, dy = ty - _ringY;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = MaxRingSpeed * Dt;
                if (dist <= step) { _ringX = tx; _ringY = ty; }
                else { _ringX += dx / dist * step; _ringY += dy / dist * step; }
                ring = new RingInput(_ringX, _ringY);
            }
            Sim.Tick(Dt, ring);
            SimSeconds += Dt;
            if (s.Phase == Phase.Winter) FinishRow();
        }

        /// <summary>Urgency: Ripe first, then highest Wet progress, then Dry (nearest wins ties).</summary>
        private GridPos? PickTarget()
        {
            var s = Sim.State;
            Plot best = null;
            double bestScore = double.NegativeInfinity;
            foreach (var p in s.Plots)
            {
                double score = p.State == PlotState.Ripe ? 3 : p.State == PlotState.Wet ? 1 + p.Progress : 0;
                float dx = p.Pos.X - _ringX, dy = p.Pos.Y - _ringY;
                score -= 0.01 * Math.Sqrt(dx * dx + dy * dy);
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best?.Pos;
        }

        private void DoWinter()
        {
            // Let the greenhouse accrue its full cap before leaving winter.
            if (Sim.State.Greenhouse.SecondsLeftThisWinter > 0f && Sim.State.Stats.GreenhouseLevel > 0)
            {
                Sim.Tick(1f, null);
                SimSeconds += 1f;
                return;
            }
            var s = Sim.State;
            bool firstGen = s.Generation.Generation == 1;
            if (firstGen && Summary.FirstCanRetireYear < 0 && Sim.CanRetire) Summary.FirstCanRetireYear = s.Year;
            int bought = BuyGreedy(Sim.Nodes, true);
            if (firstGen && s.Year == 1) Summary.Year1RootNodesBought = bought;
            _boughtThisWinter += bought;
            if (ShouldRetire())
            {
                if (Summary.SeedsAtFirstRetire < 0)
                {
                    Summary.SeedsAtFirstRetire = Sim.SeedsIfRetiredNow;
                    Summary.RingShareAtFirstRetire = Rows.Count > 0 ? Share(Rows[Rows.Count - 1]) : 0;
                }
                FinishGenerationSpend();
                Sim.Retire();
                return;
            }
            _boughtThisWinter += BuyGreedy(Sim.HeritageNodes, false);
            Sim.StartNextYear();
            StartRow();
        }

        /// <summary>
        /// Retire when allowed and this generation's seeds reach <see cref="RetireGrowth"/> × all seeds earned so far
        /// (at least <see cref="RetireAtSeeds"/>) — the usual "prestige when it multiplies your progress" instinct —
        /// or cover the rest of the Heritage tree. A finished tree never retires again: the bot plays the Golden Year.
        /// </summary>
        private bool ShouldRetire()
        {
            if (!Sim.CanRetire || Sim.HeritageComplete) return false;
            int seeds = Sim.SeedsIfRetiredNow;
            if (seeds < RetireAtSeeds) return false;
            return seeds >= RetireGrowth * Sim.State.Generation.SeedsEarnedTotal || seeds >= RemainingHeritageCost();
        }

        private double RemainingHeritageCost()
        {
            double total = -Sim.State.Seeds;
            foreach (var n in Sim.HeritageNodes)
            {
                int level = Sim.Heritage.GetLevel(n.Id);
                int max = n.MaxLevel < 0 ? level : n.MaxLevel;
                for (int l = level; l < max; l++) total += Math.Round(n.BaseCost * Math.Pow(n.CostGrowth, l));
            }
            return total;
        }

        private void DoHeritage()
        {
            _boughtThisWinter += BuyGreedy(Sim.HeritageNodes, false);
            if (Sim.HeritageComplete && Summary.GenerationsToMaxHeritage < 0)
                Summary.GenerationsToMaxHeritage = Sim.State.Generation.Generation - 1;
            Sim.StartNewGeneration();
            StartRow();
        }

        private int BuyGreedy(IReadOnlyList<SkillNode> nodes, bool coins)
        {
            int bought = 0;
            for (int guard = 0; guard < 400; guard++)
            {
                SkillNode best = null;
                double bestScore = 0;
                foreach (var n in nodes)
                {
                    if (!Sim.CanBuy(n.Id)) continue;
                    double w = Weights.TryGetValue(n.Id, out var v) ? v : 1;
                    double score = w / Math.Max(1, Sim.CostOf(n.Id));
                    if (score > bestScore) { bestScore = score; best = n; }
                }
                if (best == null) break;
                double cost = Sim.CostOf(best.Id);
                int yearNow = Sim.State.Year;
                if (!Sim.TryBuy(best.Id)) break;
                bought++;
                if (!coins) continue;
                _spend.TryGetValue(best.Id, out var sofar);
                _spend[best.Id] = sofar + cost;
                _spendTotal += cost;
                if (best.Id == "apprentice_count" && Summary.FirstApprenticeYear < 0 && Sim.State.Generation.Generation == 1)
                    Summary.FirstApprenticeYear = yearNow;
            }
            return bought;
        }

        private void FinishGenerationSpend()
        {
            // Share of the generation's coins: coins spent on one node / coins earned this generation.
            double earned = Sim.State.Generation.LifetimeCoinsThisGeneration;
            if (_spendTotal > 0 && earned > 0)
            {
                foreach (var kv in _spend)
                {
                    double share = kv.Value / earned;
                    var node = AlmanacData.Get(kv.Key);
                    if (node != null && node.MaxLevel < 0)
                    {
                        Summary.MaxSinkShare = Math.Max(Summary.MaxSinkShare, share);
                        continue;
                    }
                    if (share > Summary.MaxNodeSpendShare)
                    {
                        Summary.MaxNodeSpendShare = share;
                        Summary.MaxNodeSpendId = kv.Key;
                        Summary.MaxNodeSpendGeneration = Sim.State.Generation.Generation;
                    }
                }
            }
            _spend.Clear();
            _spendTotal = 0;
        }

        private void StartRow()
        {
            var s = Sim.State;
            _coinsAtYearStart = s.Generation.LifetimeCoinsThisGeneration;
            _yearClock = 0f;
            _row = new YearRow { Generation = s.Generation.Generation, Year = s.Year, FirstRipeTime = -1f };
            _row.NodesBought = _boughtThisWinter;
            _boughtThisWinter = 0;
        }

        private void FinishRow()
        {
            var s = Sim.State;
            _row.CoinsThisYear = s.Generation.LifetimeCoinsThisGeneration - _coinsAtYearStart;
            _row.TotalCoins = s.Generation.LifetimeCoinsTotal;
            _row.Seeds = s.Seeds;
            _row.Apprentices = s.Apprentices.Count;
            _row.FieldSize = s.GridSize;
            _row.RingRadius = s.RingRadius;
            _row.SimSecondsEnd = SimSeconds;
            int top = 0;
            foreach (var p in s.Plots) top = Math.Max(top, p.Tier);
            _row.TopTier = top;
            Rows.Add(_row);
            if (_row.Generation == 1 && _row.Year == 1)
            {
                Summary.Year1Coins = _row.CoinsThisYear;
                Summary.RingShareYear1 = Share(_row);
            }
            if (_row.Generation == 4)
            {
                int ring = 0, all = 0;
                foreach (var r in Rows) if (r.Generation == 4) { ring += r.HarvestsRing; all += r.TotalHarvests; }
                Summary.RingShareGen4 = all > 0 ? (double)ring / all : 0;
            }
            _row = null;
        }

        private static double Share(YearRow r) => r.TotalHarvests > 0 ? (double)r.HarvestsRing / r.TotalHarvests : 0;

        private void OnHarvested(HarvestEvent e)
        {
            if (_row == null) return;
            switch (e.Source)
            {
                case HarvestSource.Ring: _row.HarvestsRing++; break;
                case HarvestSource.Apprentice: _row.HarvestsApprentice++; break;
                case HarvestSource.Tractor: _row.HarvestsTractor++; break;
                case HarvestSource.LateFrost: _row.HarvestsLateFrost++; break;
            }
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("generation,year,coins_year,coins_total,seeds,nodes_bought,apprentices,field,ring_radius,top_tier,first_ripe_s,harvests,share_ring,share_apprentice,share_tractor,share_late_frost,sim_seconds");
            foreach (var r in Rows)
            {
                int h = Math.Max(1, r.TotalHarvests);
                sb.AppendLine(string.Join(",", new[]
                {
                    r.Generation.ToString(), r.Year.ToString(), F(r.CoinsThisYear), F(r.TotalCoins), r.Seeds.ToString(), r.NodesBought.ToString(),
                    r.Apprentices.ToString(), r.FieldSize.ToString(), F(r.RingRadius), r.TopTier.ToString(), F(r.FirstRipeTime), r.TotalHarvests.ToString(),
                    P(r.HarvestsRing, h), P(r.HarvestsApprentice, h), P(r.HarvestsTractor, h), P(r.HarvestsLateFrost, h), F(r.SimSecondsEnd),
                }));
            }
            return sb.ToString();
        }

        public string ToTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("gen year  coins/yr   total  seeds bought appr field ring  tier ripe@   harv  ring% appr% trac% frost%");
            foreach (var r in Rows)
            {
                int h = Math.Max(1, r.TotalHarvests);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,3} {1,4} {2,9:0} {3,7:0} {4,6} {5,6} {6,4} {7,5} {8,5:0.00} {9,4} {10,6:0.0} {11,6} {12,5:0}% {13,5:0}% {14,5:0}% {15,5:0}%",
                    r.Generation, r.Year, r.CoinsThisYear, r.TotalCoins, r.Seeds, r.NodesBought, r.Apprentices, r.FieldSize + "x" + r.FieldSize, r.RingRadius, r.TopTier,
                    r.FirstRipeTime, r.TotalHarvests, 100.0 * r.HarvestsRing / h, 100.0 * r.HarvestsApprentice / h, 100.0 * r.HarvestsTractor / h, 100.0 * r.HarvestsLateFrost / h));
            }
            return sb.ToString();
        }

        /// <summary>The §15 targets for this run, one per line.</summary>
        public string SummaryText()
        {
            var m = Summary;
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "year 1 coins            {0:0} (root nodes bought: {1})", m.Year1Coins, m.Year1RootNodesBought));
            sb.AppendLine("first apprentice        year " + m.FirstApprenticeYear);
            sb.AppendLine("first CanRetire         year " + m.FirstCanRetireYear);
            sb.AppendLine("seeds at first retire   " + m.SeedsAtFirstRetire);
            sb.AppendLine("generations to max H.   " + m.GenerationsToMaxHeritage);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "sim time to ending      {0:0.00} h", m.SimSecondsToEnding / 3600.0));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "ring share              year 1 {0:0}%, first retire {1:0}%, gen 4 {2:0}%", 100 * m.RingShareYear1, 100 * m.RingShareAtFirstRetire, 100 * m.RingShareGen4));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "largest node share      {0:0}% of a generation's coins ({1}, gen {2}); open-ended upgrade_plot {3:0}%", 100 * m.MaxNodeSpendShare, m.MaxNodeSpendId, m.MaxNodeSpendGeneration, 100 * m.MaxSinkShare));
            return sb.ToString();
        }

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string P(int part, int total) => (100.0 * part / total).ToString("0", CultureInfo.InvariantCulture);
    }
}
