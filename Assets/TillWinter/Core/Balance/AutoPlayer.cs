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
        public double Damage;
        public float FirstRipeTime;
        public int Strikes, Crits, Tired, Breaks, DeepestLayer;
        public int HarvestsHand, HarvestsApprentice, HarvestsTractor, HarvestsLateFrost, HarvestsFrost;
        public int TotalHarvests => HarvestsHand + HarvestsApprentice + HarvestsTractor + HarvestsLateFrost + HarvestsFrost;
        public double SimSecondsEnd;
    }

    /// <summary>The GDD §15 targets measured on one run. -1 = never happened.</summary>
    public sealed class BalanceSummary
    {
        public double Year1Coins;
        public int Year1RootNodesBought;
        public int FirstApprenticeYear = -1;
        public int FirstCanRetireYear = -1;
        public int SeedsAtFirstRetire = -1;
        public int GenerationsToMaxHeritage = -1;
        public double SimSecondsToEnding = -1;
        /// <summary>Share of harvests reaped by the player's own hand (the active layer's weight).</summary>
        public double HandShareYear1, HandShareAtFirstRetire, HandShareGen4 = -1;
        public double MaxNodeSpendShare;
        public string MaxNodeSpendId = "";
        public int MaxNodeSpendGeneration;
        /// <summary>The open-ended sink (upgrade_plot, no max level) is reported separately: it absorbs leftover coins by design.</summary>
        public double MaxSinkShare;
        public bool ReachedEnding;
    }

    /// <summary>
    /// Scripted player that runs <see cref="FarmSim"/> headlessly for balance tables (no Unity), GDD §2v3.13.
    /// Smart (the default): strikes the hard plot worth the most per hit, on the beat with probability
    /// <see cref="Skill"/>, spends tired swings only to finish a plot, waters the crop nearest ripe while the depot
    /// is more than half full, reaps everything ripe in one swipe. <see cref="Dumb"/>: a random plot at a random
    /// moment, never waters, reaps one at a time. Both shop the same way in winter: greedy weight/cost purchases;
    /// retire once this generation's seeds reach <see cref="RetireGrowth"/> × every seed earned before (at least
    /// <see cref="RetireAtSeeds"/>), then greedy Heritage. Taps crows, pests and clouds.
    /// </summary>
    public sealed class AutoPlayer
    {
        public float ReactionDelay = 0.3f;
        /// <summary>Share of strikes the smart player times onto the beat.</summary>
        public float Skill = 0.8f;
        /// <summary>The smart player swipes once this many crops stand ripe (sooner when one is about to draw a crow).</summary>
        public int SwipeAt = 2;
        /// <summary>The player who taps at random (the acceptance yardstick, GDD §2v3.13).</summary>
        public bool Dumb;
        /// <summary>Never retire below this many seeds.</summary>
        public int RetireAtSeeds = 8;
        /// <summary>Retire once this generation's seeds reach this multiple of every seed earned before it.</summary>
        public double RetireGrowth = 1.5;
        public float Dt = 0.05f;
        public int MaxTicks = 2_000_000;

        /// <summary>Static desirability per node id; missing = 1. Greedy score = weight / cost.</summary>
        public Dictionary<string, double> Weights = AlmanacAdvisor.CreateWeights();

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
        private readonly Dictionary<GridPos, float> _crowSeen = new Dictionary<GridPos, float>();
        private readonly List<GridPos> _crowKeys = new List<GridPos>();
        private readonly List<GridPos> _swipe = new List<GridPos>();
        private float _cloudSeen = -1f;
        private int _boughtThisWinter;
        private readonly Dictionary<string, double> _spend = new Dictionary<string, double>();
        private double _spendTotal;
        private bool _nextStrikeOffBeat;
        private int _strikesAtRow, _critsAtRow, _breaksAtRow, _tired;

        public AutoPlayer(FarmSim sim, int seed)
        {
            Sim = sim;
            _rng = new Rng(seed);
            sim.Harvested += OnHarvested;
            sim.Struck += e => { if (e.Tired && !e.Splash && e.ApprenticeIndex < 0) _tired++; };
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

        /// <summary>Runs until this many years have been played (winters shopped, no retiring): the acceptance table.</summary>
        public void RunYears(int years)
        {
            int ticks = 0;
            int startYears = Sim.State.Generation.YearsTotal;
            while (Sim.State.Generation.YearsTotal < startYears + years && ticks < MaxTicks)
            {
                if (Sim.State.Phase == Phase.Year) TickYear();
                else if (Sim.State.Phase == Phase.Winter)
                {
                    BuyGreedy(Sim.Nodes, true);
                    Sim.StartNextYear();
                    StartRow();
                }
                else DoHeritage();
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
                if (_crowSeen.Count > 0)
                {
                    _crowKeys.Clear();
                    _crowKeys.AddRange(_crowSeen.Keys);
                    foreach (var pos in _crowKeys)
                    {
                        _crowSeen[pos] += ReactionDelay;
                        if (_crowSeen[pos] >= ReactionDelay && s.InBounds(pos) && s.GetPlot(pos).HasCrow && (!Dumb || _rng.NextDouble() < 0.3)) Sim.TapAt(pos);
                        if (!s.InBounds(pos) || !s.GetPlot(pos).HasCrow) _crowSeen.Remove(pos);
                    }
                }
                var pest = s.Pest;
                if (!Dumb && (pest.Kind == PestKind.Mole || pest.Kind == PestKind.Rabbit))
                {
                    _pestSeen += ReactionDelay;
                    if (_pestSeen >= ReactionDelay) Sim.TapAt(pest.Pos);
                }
                else _pestSeen = 0f;
                if (!Dumb && s.Cloud.Active)
                {
                    _cloudSeen += ReactionDelay;
                    if (_cloudSeen >= ReactionDelay) Sim.TapCloud();
                }
            }
            if (Dumb) ActDumb(); else ActSmart();
            Sim.Tick(Dt);
            SimSeconds += Dt;
            if (s.Phase == Phase.Winter) FinishRow();
        }

        /// <summary>Random plot, random moment: strikes whatever is hard, reaps whatever is ripe, one at a time.</summary>
        private void ActDumb()
        {
            var s = Sim.State;
            if (!Sim.CanStrike) return;
            var p = s.Plots[_rng.Next(s.Plots.Count)];
            if (p.IsHard) Sim.Strike(p.Pos);
            else if (p.IsRipe) Sim.ReapOne(p.Pos);
        }

        private void ActSmart()
        {
            var s = Sim.State;
            // Reap in one swipe once a few crops stand ripe, once one has stood long enough to draw a crow, or when
            // there is nothing left to strike: a player does not lift the hoe for every single carrot, and the pickers
            // get their share of the field in between.
            _swipe.Clear();
            bool urgent = false, anyHard = false;
            foreach (var p in s.Plots)
            {
                if (p.IsRipe) { _swipe.Add(p.Pos); if (p.RipeAge >= 1.5f) urgent = true; }
                else if (p.IsHard) anyHard = true;
            }
            if (_swipe.Count > 0 && (_swipe.Count >= SwipeAt || urgent || !anyHard || s.Tired))
            {
                Sim.Reap(_swipe);
                return;
            }
            // The hard plot worth the most per hit (the type is visible, the bonus is not: use its expectation).
            Plot best = null;
            double bestScore = 0;
            var cfg = Sim.Config;
            foreach (var p in s.Plots)
            {
                if (!p.IsHard) continue;
                double bonus = cfg.BreakCoins * Math.Pow(cfg.BreakGrowth, p.Layer) * (p.Hardpan ? cfg.HardpanCoinsMult : 1);
                double crop = cfg.Crops[Math.Min(p.Tier, cfg.MaxTier)].Value * (1 + cfg.DepthValue * p.Layer);
                double score = (bonus + crop) / Math.Max(1, Sim.HitsLeft(p));
                if (s.Pest.Kind == PestKind.Locusts && Sim.InLocusts(p.Pos)) score *= 4;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            if (best != null && Sim.CanStrike)
            {
                // Tired swings are 40% and never crit: worth it only to finish a plot; otherwise let the depot refill.
                if (s.Tired && best.Hp > s.Stats.StrikeDamage * cfg.TiredDamage * 1.3) { WaterWhileWaiting(best); return; }
                if (_nextStrikeOffBeat || s.OnBeat)
                {
                    Sim.Strike(best.Pos);
                    _nextStrikeOffBeat = _rng.NextDouble() > Skill; // decided per strike: did I read the next pulse right?
                }
                return;
            }
            WaterWhileWaiting(best);
        }

        /// <summary>Nothing to strike, or no stamina to strike well: water the crop closest to ripe while the depot is more than half full.</summary>
        private void WaterWhileWaiting(Plot hard)
        {
            var s = Sim.State;
            Plot grow = null;
            foreach (var p in s.Plots)
            {
                if (!p.IsGrowing) continue;
                if (grow == null || p.Progress > grow.Progress) grow = p;
            }
            bool water = grow != null && (hard == null || s.Tired) && s.Stamina > s.Stats.StaminaMax * 0.5f;
            Sim.SetWatering(water ? grow.Pos : (GridPos?)null);
        }

        private float _pestSeen;
        private bool _winterBooked;

        private void DoWinter()
        {
            // Let the greenhouse accrue its full cap before leaving winter.
            if (Sim.State.Greenhouse.SecondsLeftThisWinter > 0f && Sim.State.Stats.GreenhouseLevel > 0)
            {
                Sim.Tick(1f);
                SimSeconds += 1f;
                return;
            }
            var s = Sim.State;
            if (!_winterBooked && Rows.Count > 0)
            {
                // The winter's greenhouse coins belong to the year just closed; otherwise no row of the table shows them.
                var last = Rows[Rows.Count - 1];
                last.CoinsThisYear += s.Greenhouse.CoinsThisWinter;
                last.TotalCoins = s.Generation.LifetimeCoinsTotal;
                _winterBooked = true;
            }
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
                    Summary.HandShareAtFirstRetire = Rows.Count > 0 ? Share(Rows[Rows.Count - 1]) : 0;
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
        /// (at least <see cref="RetireAtSeeds"/>), or cover the rest of the Heritage tree. A finished tree never retires
        /// again: the bot plays the Golden Year.
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
                if (n.Excludes == null) { total += Left(n); continue; }
                // Either/or pairs (GDD §7.3 v2.3): only one side will ever be bought, so count one side once.
                var other = Sim.Heritage.GetNode(n.Excludes);
                if (Sim.Heritage.GetLevel(n.Excludes) > 0) continue;          // the other path was taken
                if (Sim.Heritage.GetLevel(n.Id) > 0 || other == null) { total += Left(n); continue; }
                if (string.CompareOrdinal(n.Id, n.Excludes) < 0) total += Math.Min(Left(n), Left(other)); // neither yet: the cheaper, once
            }
            return total;

            double Left(SkillNode node)
            {
                double sum = 0;
                int level = Sim.Heritage.GetLevel(node.Id);
                int max = node.MaxLevel < 0 ? level : node.MaxLevel;
                for (int l = level; l < max; l++) sum += Math.Round(node.BaseCost * Math.Pow(node.CostGrowth, l));
                return sum;
            }
        }

        private void DoHeritage()
        {
            _boughtThisWinter += BuyGreedy(Sim.HeritageNodes, false);
            if (Sim.HeritageComplete && Summary.GenerationsToMaxHeritage < 0)
                Summary.GenerationsToMaxHeritage = Sim.State.Generation.Generation - 1;
            Sim.StartNewGeneration();
            StartRow();
        }

        /// <summary>
        /// Nodes whose effect only exists through a choice the bot never makes (a store share): buying them would only
        /// sink coins and make the balance table read poorer than a player plays.
        /// </summary>
        private static bool BotCanUse(SkillNode n) => n.Effect != EffectType.Barn;

        private int BuyGreedy(IReadOnlyList<SkillNode> nodes, bool coins)
        {
            int bought = 0;
            for (int guard = 0; guard < 400; guard++)
            {
                SkillNode best = null;
                double bestScore = 0;
                foreach (var n in nodes)
                {
                    if (!Sim.CanBuy(n.Id) || !BotCanUse(n)) continue;
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
            _winterBooked = false;
            _strikesAtRow = s.Generation.Strikes;
            _critsAtRow = s.Generation.Crits;
            _breaksAtRow = s.Generation.Breaks;
            _tired = 0;
            Sim.SetWatering(null);
        }

        private void FinishRow()
        {
            var s = Sim.State;
            _row.CoinsThisYear = s.Generation.LifetimeCoinsThisGeneration - _coinsAtYearStart;
            _row.TotalCoins = s.Generation.LifetimeCoinsTotal;
            _row.Seeds = s.Seeds;
            _row.Apprentices = s.Apprentices.Count;
            _row.FieldSize = s.GridSize;
            _row.Damage = s.Stats.StrikeDamage;
            _row.Strikes = s.Generation.Strikes - _strikesAtRow;
            _row.Crits = s.Generation.Crits - _critsAtRow;
            _row.Breaks = s.Generation.Breaks - _breaksAtRow;
            _row.Tired = _tired;
            _row.DeepestLayer = s.Generation.DeepestLayer;
            _row.SimSecondsEnd = SimSeconds;
            int top = 0;
            foreach (var p in s.Plots) top = Math.Max(top, p.Tier);
            _row.TopTier = top;
            Rows.Add(_row);
            if (_row.Generation == 1 && _row.Year == 1)
            {
                Summary.Year1Coins = _row.CoinsThisYear;
                Summary.HandShareYear1 = Share(_row);
            }
            if (_row.Generation == 4)
            {
                int hand = 0, all = 0;
                foreach (var r in Rows) if (r.Generation == 4) { hand += r.HarvestsHand; all += r.TotalHarvests; }
                Summary.HandShareGen4 = all > 0 ? (double)hand / all : 0;
            }
            _row = null;
        }

        private static double Share(YearRow r) => r.TotalHarvests > 0 ? (double)r.HarvestsHand / r.TotalHarvests : 0;

        private void OnHarvested(HarvestEvent e)
        {
            if (_row == null) return;
            switch (e.Source)
            {
                case HarvestSource.Hand: _row.HarvestsHand++; break;
                case HarvestSource.Apprentice: _row.HarvestsApprentice++; break;
                case HarvestSource.Tractor: _row.HarvestsTractor++; break;
                case HarvestSource.LateFrost: _row.HarvestsLateFrost++; break;
                case HarvestSource.Frost: _row.HarvestsFrost++; break;
            }
        }

        /// <summary>Coins over every row (the acceptance yardstick).</summary>
        public double TotalCoins
        {
            get { double sum = 0; foreach (var r in Rows) sum += r.CoinsThisYear; return sum; }
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("generation,year,coins_year,coins_total,seeds,nodes_bought,apprentices,field,damage,top_tier,first_ripe_s,strikes,crit_pct,tired_pct,breaks,deepest,harvests,share_hand,share_apprentice,share_tractor,share_frost,sim_seconds");
            foreach (var r in Rows)
            {
                int h = Math.Max(1, r.TotalHarvests);
                int st = Math.Max(1, r.Strikes);
                sb.AppendLine(string.Join(",", new[]
                {
                    r.Generation.ToString(), r.Year.ToString(), F(r.CoinsThisYear), F(r.TotalCoins), r.Seeds.ToString(), r.NodesBought.ToString(),
                    r.Apprentices.ToString(), r.FieldSize.ToString(), F(r.Damage), r.TopTier.ToString(), F(r.FirstRipeTime), r.Strikes.ToString(),
                    P(r.Crits, st), P(r.Tired, st), r.Breaks.ToString(), r.DeepestLayer.ToString(), r.TotalHarvests.ToString(),
                    P(r.HarvestsHand, h), P(r.HarvestsApprentice, h), P(r.HarvestsTractor, h), P(r.HarvestsLateFrost + r.HarvestsFrost, h), F(r.SimSecondsEnd),
                }));
            }
            return sb.ToString();
        }

        public string ToTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("gen year  coins/yr   total  seeds bought appr field  dmg tier strikes crit% tired% breaks deep   harv  hand% appr% trac% frost%");
            foreach (var r in Rows)
            {
                int h = Math.Max(1, r.TotalHarvests);
                int st = Math.Max(1, r.Strikes);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,3} {1,4} {2,9:0} {3,7:0} {4,6} {5,6} {6,4} {7,5} {8,4:0} {9,4} {10,7} {11,5:0}% {12,5:0}% {13,6} {14,4} {15,6} {16,5:0}% {17,5:0}% {18,5:0}% {19,5:0}%",
                    r.Generation, r.Year, r.CoinsThisYear, r.TotalCoins, r.Seeds, r.NodesBought, r.Apprentices, r.FieldSize + "x" + r.FieldSize, r.Damage, r.TopTier,
                    r.Strikes, 100.0 * r.Crits / st, 100.0 * r.Tired / st, r.Breaks, r.DeepestLayer, r.TotalHarvests,
                    100.0 * r.HarvestsHand / h, 100.0 * r.HarvestsApprentice / h, 100.0 * r.HarvestsTractor / h, 100.0 * (r.HarvestsLateFrost + r.HarvestsFrost) / h));
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
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "hand share              year 1 {0:0}%, first retire {1:0}%, gen 4 {2:0}%", 100 * m.HandShareYear1, 100 * m.HandShareAtFirstRetire, 100 * m.HandShareGen4));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "largest node share      {0:0}% of a generation's coins ({1}, gen {2}); open-ended upgrade_plot {3:0}%", 100 * m.MaxNodeSpendShare, m.MaxNodeSpendId, m.MaxNodeSpendGeneration, 100 * m.MaxSinkShare));
            return sb.ToString();
        }

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string P(int part, int total) => (100.0 * part / total).ToString("0", CultureInfo.InvariantCulture);
    }
}
