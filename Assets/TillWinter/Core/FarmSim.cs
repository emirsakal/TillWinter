using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>
    /// The whole game. Pure C#, deterministic for a fixed dt and seed. The Unity layer calls
    /// <see cref="Tick"/> every frame, forwards input via <see cref="Tick"/>/<see cref="TapAt"/>,
    /// and renders <see cref="State"/>.
    /// </summary>
    public sealed class FarmSim
    {
        public FarmConfig Config { get; }
        public FarmState State { get; }

        public event Action<HarvestEvent> Harvested;
        public event Action<CrowEvent> CrowLanded;
        public event Action<CrowEvent> CrowScared;
        public event Action<CrowEvent> CrowAte;
        public event Action<Season> SeasonChanged;
        public event Action WinterStarted;
        public event Action FrostWarningStarted;
        public event Action YearStarted;
        public event Action<UpgradeId> Purchased;
        public event Action FieldExpanded;

        private readonly Random _rng;
        private readonly List<Plot> _scratch = new List<Plot>();
        private float _crowSpawnTimer;
        private float? _ringRadiusOverride;

        public FarmSim(FarmConfig config, int seed)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            State = new FarmState();
            _rng = new Random(seed);
            BuildField(config.StartGridSize);
            RecomputeDerived();
            ResetApprenticePosition();
        }

        // ------------------------------------------------------------------ public API

        /// <param name="dt">Seconds. Already scaled by any debug time scale.</param>
        /// <param name="ring">Ring centre in plot space, or null when the finger is up.</param>
        public void Tick(float dt, RingInput? ring)
        {
            if (dt < 0f) dt = 0f;
            State.Ring = State.IsWinter ? null : ring;
            if (State.IsWinter) return;

            AdvanceYear(dt);
            if (State.IsWinter) return;

            UpdateGrowth(dt);
            UpdateApprentice(dt);
            UpdateCrows(dt);
        }

        /// <summary>Tap a plot. Scares a crow if one is there. Returns true if something happened.</summary>
        public bool TapAt(GridPos pos)
        {
            if (State.IsWinter || !State.InBounds(pos)) return false;
            var plot = State.GetPlot(pos);
            if (!plot.HasCrow) return false;
            RemoveCrowAt(pos);
            CrowScared?.Invoke(new CrowEvent(pos));
            return true;
        }

        public double GetCost(UpgradeId id)
        {
            var def = Config.GetUpgrade(id);
            if (def == null) return double.PositiveInfinity;
            return Math.Round(def.BaseCost * Math.Pow(Config.CostGrowth, State.GetLevel(id)));
        }

        public int GetMaxLevel(UpgradeId id)
        {
            if (id == UpgradeId.UpgradePlot)
                return State.GridSize * State.GridSize * Config.MaxTier;
            var def = Config.GetUpgrade(id);
            return def == null ? 0 : def.MaxLevel;
        }

        public bool IsMaxed(UpgradeId id) => State.GetLevel(id) >= GetMaxLevel(id);

        public bool CanBuy(UpgradeId id) =>
            State.IsWinter && !IsMaxed(id) && State.Coins >= GetCost(id);

        public bool TryBuy(UpgradeId id)
        {
            if (!CanBuy(id)) return false;
            State.Coins -= GetCost(id);
            State.LevelMap[id] = State.GetLevel(id) + 1;
            ApplyUpgrade(id);
            RecomputeDerived();
            Purchased?.Invoke(id);
            return true;
        }

        /// <summary>Leave the winter shop and start Spring of the next year.</summary>
        public void StartNextYear()
        {
            if (!State.IsWinter) return;
            State.Year++;
            State.YearTime = 0f;
            State.FrostWarning = false;
            State.Season = Season.Spring;
            _crowSpawnTimer = 0f;
            ClearCrows();
            foreach (var p in State.PlotArray) p.Growth = 0f;
            ResetApprenticePosition();
            SeasonChanged?.Invoke(Season.Spring);
            YearStarted?.Invoke();
        }

        // ------------------------------------------------------------------ debug hooks

        public void DebugAddCoins(double amount) => State.Coins += amount;

        public void DebugSkipToWinter()
        {
            if (!State.IsWinter) EnterWinter();
        }

        /// <summary>Force a crow onto a plot (ripening it if needed). Ignores year/scarecrow rules.</summary>
        public bool DebugSpawnCrow()
        {
            if (State.IsWinter) return false;
            _scratch.Clear();
            foreach (var p in State.PlotArray)
                if (!p.HasCrow && p.IsRipe && !State.IsUnderRing(p.Pos)) _scratch.Add(p);
            if (_scratch.Count == 0)
                foreach (var p in State.PlotArray)
                    if (!p.HasCrow) _scratch.Add(p);
            if (_scratch.Count == 0) return false;
            var plot = _scratch[_rng.Next(_scratch.Count)];
            plot.Growth = 1f;
            SpawnCrowAt(plot.Pos);
            return true;
        }

        /// <summary>null restores the upgrade-driven radius.</summary>
        public void DebugSetRingRadiusOverride(float? radius)
        {
            _ringRadiusOverride = radius;
            RecomputeDerived();
        }

        public float? DebugRingRadiusOverride => _ringRadiusOverride;

        // ------------------------------------------------------------------ year

        private void AdvanceYear(float dt)
        {
            State.YearTime += dt;

            if (!State.FrostWarning && State.YearTime >= State.YearLength - Config.FrostWarningSeconds)
            {
                State.FrostWarning = true;
                FrostWarningStarted?.Invoke();
            }

            if (State.YearTime >= State.YearLength)
            {
                EnterWinter();
                return;
            }

            var season = SeasonAt(State.YearTime, State.YearLength);
            if (season != State.Season)
            {
                State.Season = season;
                SeasonChanged?.Invoke(season);
            }
        }

        private static Season SeasonAt(float t, float length)
        {
            float f = t / length;
            if (f < 1f / 3f) return Season.Spring;
            if (f < 2f / 3f) return Season.Summer;
            return Season.Autumn;
        }

        private void EnterWinter()
        {
            State.Season = Season.Winter;
            State.YearTime = State.YearLength;
            State.FrostWarning = false;
            State.Ring = null;
            ClearCrows();
            foreach (var p in State.PlotArray) p.Growth = 0f;
            var a = State.Apprentice;
            a.HasTarget = false;
            a.IsHarvesting = false;
            a.HarvestProgress = 0f;
            SeasonChanged?.Invoke(Season.Winter);
            WinterStarted?.Invoke();
        }

        // ------------------------------------------------------------------ growth & harvest

        private void UpdateGrowth(float dt)
        {
            float soil = 1f + Config.SoilPerLevel * State.GetLevel(UpgradeId.Soil);
            float natural = Config.IrrigationPerLevel * State.GetLevel(UpgradeId.Irrigation);

            foreach (var plot in State.PlotArray)
            {
                bool under = State.IsUnderRing(plot.Pos);
                if (!plot.IsRipe)
                {
                    float factor = under ? 1f : natural;
                    if (factor > 0f)
                    {
                        float rate = soil * factor / Config.RipeTimes[(int)plot.Tier];
                        plot.Growth = Math.Min(1f, plot.Growth + rate * dt);
                    }
                }
                if (plot.IsRipe && under)
                    Harvest(plot, HarvestSource.Ring);
            }
        }

        private void Harvest(Plot plot, HarvestSource source)
        {
            double coins = Config.CropValues[(int)plot.Tier];
            State.Coins += coins;
            plot.Growth = 0f;
            if (plot.HasCrow)
            {
                RemoveCrowAt(plot.Pos);
                CrowScared?.Invoke(new CrowEvent(plot.Pos));
            }
            Harvested?.Invoke(new HarvestEvent(plot.Pos, plot.Tier, coins, source));
        }

        // ------------------------------------------------------------------ apprentice

        private void UpdateApprentice(float dt)
        {
            var a = State.Apprentice;
            if (!a.Owned) return;

            if (a.HasTarget && !State.GetPlot(a.Target).IsRipe)
            {
                // Someone else (the ring) got it first.
                a.HasTarget = false;
                a.IsHarvesting = false;
                a.HarvestProgress = 0f;
            }

            if (!a.HasTarget)
            {
                Plot best = null;
                float bestDist = float.MaxValue;
                foreach (var p in State.PlotArray)
                {
                    if (!p.IsRipe) continue;
                    float dx = p.Pos.X - a.X, dy = p.Pos.Y - a.Y;
                    float d = dx * dx + dy * dy;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = p;
                    }
                }
                if (best == null) return;
                a.Target = best.Pos;
                a.HasTarget = true;
            }

            if (a.IsHarvesting)
            {
                a.HarvestProgress += Config.ApprenticeHarvestTime <= 0f ? 1f : dt / Config.ApprenticeHarvestTime;
                if (a.HarvestProgress >= 1f)
                {
                    Harvest(State.GetPlot(a.Target), HarvestSource.Apprentice);
                    a.IsHarvesting = false;
                    a.HasTarget = false;
                    a.HarvestProgress = 0f;
                }
                return;
            }

            int level = State.GetLevel(UpgradeId.Apprentice);
            float speed = Config.ApprenticeBaseSpeed + Config.ApprenticeSpeedPerLevel * Math.Max(0, level - 1);
            float tx = a.Target.X, ty = a.Target.Y;
            float ddx = tx - a.X, ddy = ty - a.Y;
            float dist = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
            float step = speed * dt;
            if (dist <= step || dist < 1e-4f)
            {
                a.X = tx;
                a.Y = ty;
                a.IsHarvesting = true;
                a.HarvestProgress = 0f;
            }
            else
            {
                a.X += ddx / dist * step;
                a.Y += ddy / dist * step;
            }
        }

        private void ResetApprenticePosition()
        {
            var a = State.Apprentice;
            a.X = (State.GridSize - 1) * 0.5f;
            a.Y = -1.2f;
            a.HasTarget = false;
            a.IsHarvesting = false;
            a.HarvestProgress = 0f;
        }

        // ------------------------------------------------------------------ crows

        private void UpdateCrows(float dt)
        {
            var crows = State.CrowList;
            for (int i = crows.Count - 1; i >= 0; i--)
            {
                var crow = crows[i];
                crow.Timer += dt;
                if (crow.Timer >= crow.EatTime)
                {
                    var plot = State.GetPlot(crow.Pos);
                    plot.Growth = 0f;
                    plot.HasCrow = false;
                    crows.RemoveAt(i);
                    CrowAte?.Invoke(new CrowEvent(crow.Pos));
                }
            }

            if (State.Year < Config.CrowFirstYear) return;
            if (State.GetLevel(UpgradeId.Scarecrow) > 0) return;

            _crowSpawnTimer += dt;
            while (_crowSpawnTimer >= Config.CrowSpawnInterval)
            {
                _crowSpawnTimer -= Config.CrowSpawnInterval;
                TrySpawnCrow();
            }
        }

        private void TrySpawnCrow()
        {
            if (State.CrowList.Count >= Config.MaxCrows) return;
            _scratch.Clear();
            foreach (var p in State.PlotArray)
                if (p.IsRipe && !p.HasCrow && !State.IsUnderRing(p.Pos)) _scratch.Add(p);
            if (_scratch.Count == 0) return;
            if (_rng.NextDouble() >= Config.CrowSpawnChance) return;
            SpawnCrowAt(_scratch[_rng.Next(_scratch.Count)].Pos);
        }

        private void SpawnCrowAt(GridPos pos)
        {
            State.GetPlot(pos).HasCrow = true;
            State.CrowList.Add(new Crow { Pos = pos, Timer = 0f, EatTime = Config.CrowEatTime });
            CrowLanded?.Invoke(new CrowEvent(pos));
        }

        private void RemoveCrowAt(GridPos pos)
        {
            State.GetPlot(pos).HasCrow = false;
            var crows = State.CrowList;
            for (int i = crows.Count - 1; i >= 0; i--)
                if (crows[i].Pos == pos) crows.RemoveAt(i);
        }

        private void ClearCrows()
        {
            foreach (var p in State.PlotArray) p.HasCrow = false;
            State.CrowList.Clear();
        }

        // ------------------------------------------------------------------ field & shop

        private void BuildField(int size)
        {
            var old = State.PlotArray;
            int oldSize = State.GridSize;
            var plots = new Plot[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var pos = new GridPos(x, y);
                // Existing plots keep their index (field is anchored bottom-left); new ring starts at tier 0.
                if (old != null && x < oldSize && y < oldSize)
                {
                    var p = old[y * oldSize + x];
                    plots[y * size + x] = p;
                }
                else
                {
                    plots[y * size + x] = new Plot(pos, CropTier.Carrot);
                }
            }
            State.PlotArray = plots;
            State.GridSize = size;
        }

        private void ApplyUpgrade(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.ExpandField:
                    BuildField(Math.Min(Config.MaxGridSize, State.GridSize + 1));
                    FieldExpanded?.Invoke();
                    break;
                case UpgradeId.UpgradePlot:
                {
                    Plot lowest = null;
                    foreach (var p in State.PlotArray) // row-major: first lowest wins ties
                        if (lowest == null || p.Tier < lowest.Tier) lowest = p;
                    if (lowest != null && (int)lowest.Tier < Config.MaxTier)
                        lowest.Tier = lowest.Tier + 1;
                    break;
                }
                case UpgradeId.Apprentice:
                    if (!State.Apprentice.Owned)
                    {
                        State.Apprentice.Owned = true;
                        ResetApprenticePosition();
                    }
                    break;
            }
        }

        private void RecomputeDerived()
        {
            float radius = Config.BaseRingRadius + Config.RingRadiusPerLevel * State.GetLevel(UpgradeId.RingRadius);
            radius = Math.Min(Config.MaxRingRadius, radius);
            State.RingRadius = _ringRadiusOverride ?? radius;

            float length = Config.BaseYearLength + Config.CalendarPerLevel * State.GetLevel(UpgradeId.Calendar);
            State.YearLength = Math.Min(Config.MaxYearLength, length);
        }
    }
}
