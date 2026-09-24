using System;

namespace TillWinter.Core
{
    /// <summary>
    /// M.5, events and threats (GDD §5.5–§5.7 v2.1, re-themed v3): pests (a mole, a rabbit, a locust swarm) and the
    /// hens that eat them, lucky moments (a four-leaf clover, a golden egg, a shooting star) and the travelling trader.
    /// None of it runs while the game is closed: offline advances crops, apprentices and the tractor only.
    /// </summary>
    public sealed partial class FarmSim
    {
        /// <summary>A pest arrived (kind, plot).</summary>
        public event Action<PestKind, GridPos> PestArrived;
        /// <summary>A pest was driven off, tapped away or eaten by the hens; the coins a bonked mole dropped.</summary>
        public event Action<PestKind, GridPos, double> PestScared;
        /// <summary>A pest did its damage before anyone stopped it.</summary>
        public event Action<PestKind, GridPos> PestStruck;
        /// <summary>The hens ate a pest (GDD §4.3/§5.5 v2.1).</summary>
        public event Action<GridPos> HensAte;
        /// <summary>A lucky moment appeared (a clover on a plot, a shooting star in the sky).</summary>
        public event Action<LuckyKind> LuckyAppeared;
        /// <summary>A lucky moment was taken: its kind and the coins it paid (0 for the star, which starts a rush instead).</summary>
        public event Action<LuckyKind, double> LuckyFound;
        public event Action TraderArrived;
        public event Action TraderLeft;
        /// <summary>A trader offer was bought: which one and what it cost.</summary>
        public event Action<TraderOffer, double> TraderSold;

        private float _pestCheckTimer;
        private float _luckyCheckTimer;

        // ------------------------------------------------------------------ pests

        /// <summary>From <see cref="FarmConfig.PestFirstYear"/>, a check every few seconds may bring one pest.</summary>
        private void UpdatePests(float dt)
        {
            var pest = State.Pest;
            if (State.HenCooldown > 0f) State.HenCooldown = Math.Max(0f, State.HenCooldown - dt);
            if (pest.Kind == PestKind.None)
            {
                if (State.Year < Config.PestFirstYear || State.GoldenYearActive) return;
                _pestCheckTimer += dt;
                if (_pestCheckTimer < Config.PestCheckSeconds) return;
                _pestCheckTimer -= Config.PestCheckSeconds;
                if (_rng.NextDouble() < Config.PestChance) SpawnPest();
                return;
            }

            pest.Timer += dt;
            var plot = State.GetPlot(pest.Pos);
            // The hens get to it first if they are about.
            if (State.Stats.Hens && State.HenCooldown <= 0f && pest.Timer >= Config.HenReactSeconds)
            {
                State.HenCooldown = Config.HenCooldownSeconds;
                var at = pest.Pos;
                ClearPest(pest, 0);
                HensAte?.Invoke(at);
                if (_rng.NextDouble() < Config.GoldenEggChance)
                {
                    double egg = Crop(plot).Value * State.Stats.CropValueMult * Config.GoldenEggValue;
                    AddCoins(egg);
                    LuckyFound?.Invoke(LuckyKind.GoldenEgg, egg);
                }
                return;
            }
            switch (pest.Kind)
            {
                case PestKind.Mole:
                    if (pest.Timer < Config.MoleDigSeconds) return;
                    PestStrikes(pest, plot);
                    return;
                case PestKind.Rabbit:
                    if (pest.Timer < Config.RabbitEatSeconds) return;
                    PestStrikes(pest, plot);
                    return;
                case PestKind.Locusts:
                    // Driven off by striking inside the swarm (ShooLocustsAt), else it strips the patch.
                    if (pest.Shoo >= 0.999f) { ClearPest(pest, 0); return; }
                    if (pest.Timer < Config.LocustSeconds) return;
                    PestStrikes(pest, plot);
                    return;
            }
        }

        /// <summary>A strike inside the swarm drives it a step off (GDD §5.5 v2.1, re-themed v3).</summary>
        private void ShooLocustsAt(GridPos pos)
        {
            var pest = State.Pest;
            if (pest.Kind != PestKind.Locusts || !InLocusts(pos)) return;
            pest.Shoo += 1f / Math.Max(1, Config.LocustShooStrikes);
            if (pest.Shoo >= 0.999f) ClearPest(pest, 0); // three thirds must make one, whatever the float says
        }

        /// <summary>True for a plot inside a locust swarm: nothing grows there while it stays.</summary>
        public bool InLocusts(GridPos pos)
        {
            var pest = State.Pest;
            if (pest.Kind != PestKind.Locusts) return false;
            int r = Config.LocustRadius;
            return Math.Abs(pos.X - pest.Pos.X) <= r && Math.Abs(pos.Y - pest.Pos.Y) <= r;
        }

        private void SpawnPest()
        {
            var pest = State.Pest;
            // Which kinds can come now: a rabbit needs a carrot to eat, a mole a crop to dig up.
            _scratch.Clear();
            foreach (var p in State.PlotArray)
                if (p.Tier == 0 && !p.IsHard) _scratch.Add(p);
            bool rabbit = _scratch.Count > 0;
            int kinds = rabbit ? 3 : 2;
            int pick = Math.Min(kinds - 1, (int)(_rng.NextDouble() * kinds));
            var kind = pick == 0 ? PestKind.Mole : pick == 1 ? PestKind.Locusts : PestKind.Rabbit;
            if (kind != PestKind.Rabbit)
            {
                _scratch.Clear();
                foreach (var p in State.PlotArray) if (!p.HasCrow && (kind == PestKind.Locusts || !p.IsHard)) _scratch.Add(p);
                if (_scratch.Count == 0) foreach (var p in State.PlotArray) if (!p.HasCrow) _scratch.Add(p);
            }
            if (_scratch.Count == 0) return;
            var target = _scratch[Math.Min(_scratch.Count - 1, (int)(_rng.NextDouble() * _scratch.Count))];
            pest.Kind = kind;
            pest.Pos = target.Pos;
            pest.Timer = 0f;
            pest.Shoo = 0f;
            PestArrived?.Invoke(kind, target.Pos);
        }

        /// <summary>The pest does its damage: a mole digs the crop up, a rabbit eats the carrot, locusts strip their patch.</summary>
        private void PestStrikes(PestState pest, Plot plot)
        {
            var kind = pest.Kind;
            var at = pest.Pos;
            switch (kind)
            {
                case PestKind.Mole:
                    LoseCrop(plot);
                    break;
                case PestKind.Rabbit:
                    if (plot.Tier == 0 && !plot.IsHard) LoseCrop(plot);
                    break;
                case PestKind.Locusts:
                {
                    int r = Config.LocustRadius;
                    for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var p = new GridPos(at.X + dx, at.Y + dy);
                        if (!State.InBounds(p)) continue;
                        var q = State.GetPlot(p);
                        if (q.IsRipe) LoseCrop(q);
                        else if (q.IsGrowing) q.Progress = 0f;
                    }
                    break;
                }
            }
            pest.Kind = PestKind.None;
            pest.Timer = 0f;
            pest.Shoo = 0f;
            PestStruck?.Invoke(kind, at);
        }

        private void ClearPest(PestState pest, double coins)
        {
            State.Generation.PestsStopped++;
            var kind = pest.Kind;
            var at = pest.Pos;
            pest.Kind = PestKind.None;
            pest.Timer = 0f;
            pest.Shoo = 0f;
            PestScared?.Invoke(kind, at, coins);
        }

        /// <summary>A tap on a mole bonks it (it drops a crop's worth); a tap on a rabbit shoos it. Locusts ignore taps.</summary>
        private bool TapPest(GridPos pos)
        {
            var pest = State.Pest;
            if (pest.Kind == PestKind.None || pest.Kind == PestKind.Locusts || pest.Pos != pos) return false;
            double coins = 0;
            if (pest.Kind == PestKind.Mole)
            {
                coins = Crop(State.GetPlot(pos)).Value * State.Stats.CropValueMult * Config.MoleBounty;
                AddCoins(coins);
            }
            ClearPest(pest, coins);
            return true;
        }

        /// <summary>Puts a pest on a plot now (tests and the UI tour).</summary>
        public void DebugSpawnPest(PestKind kind, GridPos pos)
        {
            if (State.Phase != Phase.Year || !State.InBounds(pos)) return;
            var pest = State.Pest;
            pest.Kind = kind;
            pest.Pos = pos;
            pest.Timer = 0f;
            pest.Shoo = 0f;
            PestArrived?.Invoke(kind, pos);
        }

        // ------------------------------------------------------------------ lucky moments

        private void UpdateLuck(float dt)
        {
            var luck = State.Luck;
            if (luck.RushLeft > 0f) luck.RushLeft = Math.Max(0f, luck.RushLeft - dt);
            if (luck.StarLeft > 0f) luck.StarLeft = Math.Max(0f, luck.StarLeft - dt);

            if (luck.CloverLeft > 0f)
            {
                luck.CloverLeft = Math.Max(0f, luck.CloverLeft - dt);
                return;
            }
            if (State.Year < Config.LuckyFirstYear || State.GoldenYearActive) return;
            _luckyCheckTimer += dt;
            if (_luckyCheckTimer < Config.LuckyCheckSeconds) return;
            _luckyCheckTimer -= Config.LuckyCheckSeconds;
            if (_rng.NextDouble() >= Config.CloverChance) return;
            if (State.PlotArray.Length == 0) return;
            luck.CloverPos = State.PlotArray[Math.Min(State.PlotArray.Length - 1, (int)(_rng.NextDouble() * State.PlotArray.Length))].Pos;
            luck.CloverLeft = Config.CloverSeconds;
            LuckyAppeared?.Invoke(LuckyKind.Clover);
        }

        /// <summary>A strike, tap or reap on the clover's plot takes it (GDD §5.6 v2.1, re-themed v3).</summary>
        private bool TakeCloverAt(GridPos pos)
        {
            var luck = State.Luck;
            if (luck.CloverLeft <= 0f || luck.CloverPos != pos) return false;
            double coins = Crop(State.GetPlot(pos)).Value * State.Stats.CropValueMult * Config.CloverValue;
            luck.CloverLeft = 0f;
            AddCoins(coins);
            LuckyFound?.Invoke(LuckyKind.Clover, coins);
            return true;
        }

        /// <summary>At the frost warning a shooting star may cross the evening sky (GDD §5.6 v2.1).</summary>
        private void MaybeShootingStar()
        {
            if (State.Year < Config.LuckyFirstYear || State.GoldenYearActive) return;
            if (_rng.NextDouble() >= Config.ShootingStarChance) return;
            State.Luck.StarLeft = Config.ShootingStarSeconds;
            LuckyAppeared?.Invoke(LuckyKind.ShootingStar);
        }

        /// <summary>Catching the shooting star: every harvest pays double for a few seconds.</summary>
        public bool TapStar()
        {
            var luck = State.Luck;
            if (State.Phase != Phase.Year || luck.StarLeft <= 0f) return false;
            luck.StarLeft = 0f;
            luck.RushLeft = Config.StarRushSeconds;
            LuckyFound?.Invoke(LuckyKind.ShootingStar, 0);
            return true;
        }

        /// <summary>Harvests during a star rush pay this much more (never while the game is closed).</summary>
        private double LuckMultiplier => !_offline && State.Luck.RushLeft > 0f ? Config.StarRushValue : 1;

        /// <summary>Puts a clover on a plot, or a star in the sky (tests and the UI tour).</summary>
        public void DebugSpawnLucky(LuckyKind kind, GridPos pos)
        {
            if (State.Phase != Phase.Year) return;
            var luck = State.Luck;
            if (kind == LuckyKind.Clover && State.InBounds(pos))
            {
                luck.CloverPos = pos;
                luck.CloverLeft = Config.CloverSeconds;
            }
            else if (kind == LuckyKind.ShootingStar) luck.StarLeft = Config.ShootingStarSeconds;
            else return;
            LuckyAppeared?.Invoke(kind);
        }

        // ------------------------------------------------------------------ travelling trader

        /// <summary>From <see cref="FarmConfig.TraderFirstYear"/>, some summers bring the trader for a little while.</summary>
        private void PlanTrader()
        {
            var t = State.Trader;
            t.Active = false;
            t.TimeLeft = 0f;
            t.PlannedTime = -1f;
            t.SeedSold = t.RareSold = false;
            if (State.Year < Config.TraderFirstYear || State.GoldenYearActive) return;
            if (_rng.NextDouble() >= Config.TraderChance) return;
            float length = State.Stats.YearLength;
            float from = length / 3f, to = Math.Max(from, 2f * length / 3f - Config.TraderSeconds);
            t.PlannedTime = from + (float)_rng.NextDouble() * (to - from);
        }

        private void UpdateTrader(float dt)
        {
            var t = State.Trader;
            if (t.Active)
            {
                t.TimeLeft -= dt;
                if (t.TimeLeft > 0f) return;
                t.Active = false;
                t.TimeLeft = 0f;
                TraderLeft?.Invoke();
                return;
            }
            if (t.PlannedTime < 0f || State.YearTime < t.PlannedTime) return;
            t.PlannedTime = -1f;
            t.Active = true;
            t.TimeLeft = Config.TraderSeconds;
            double last = Math.Max(State.LastYearCoins, State.CoinsThisYear);
            t.SeedPrice = Math.Ceiling(Math.Max(Config.TraderSeedMinPrice, last * Config.TraderSeedPriceShare));
            t.RarePrice = Math.Ceiling(Math.Max(Config.TraderRareMinPrice, last * Config.TraderRarePriceShare));
            TraderArrived?.Invoke();
        }

        /// <summary>
        /// Buys one of the trader's two offers, once each a visit: a Heritage Seed for coins, or rare seed that turns
        /// <see cref="FarmConfig.TraderRarePlots"/> growing crops golden now (a hard plot's next seed).
        /// </summary>
        public bool TraderBuy(TraderOffer offer)
        {
            var t = State.Trader;
            if (State.Phase != Phase.Year || !t.Active) return false;
            double price = offer == TraderOffer.Seed ? t.SeedPrice : t.RarePrice;
            if ((offer == TraderOffer.Seed ? t.SeedSold : t.RareSold) || State.Coins < price) return false;
            if (offer == TraderOffer.RareSeed)
            {
                _scratch.Clear();
                foreach (var p in State.PlotArray) if (!p.IsGolden) _scratch.Add(p);
                if (_scratch.Count == 0) return false;
                for (int i = 0; i < Config.TraderRarePlots && _scratch.Count > 0; i++)
                {
                    int k = Math.Min(_scratch.Count - 1, (int)(_rng.NextDouble() * _scratch.Count));
                    _scratch[k].IsGolden = true;
                    _scratch.RemoveAt(k);
                }
                t.RareSold = true;
            }
            else
            {
                State.Generation.SeedsBanked++;
                State.Generation.SeedsEarnedTotal++;
                t.SeedSold = true;
            }
            State.Coins -= price;
            TraderSold?.Invoke(offer, price);
            Unlock(AchievementId.TraderDeal);
            return true;
        }

        /// <summary>Brings the trader now (tests and the UI tour).</summary>
        public void DebugBringTrader()
        {
            if (State.Phase != Phase.Year) return;
            var t = State.Trader;
            t.PlannedTime = 0f;
            t.Active = false;
            t.SeedSold = t.RareSold = false;
            UpdateTrader(0f);
        }
    }
}
