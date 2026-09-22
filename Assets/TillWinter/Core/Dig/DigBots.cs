using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TillWinter.Core.Dig
{
    /// <summary>
    /// Two players for the v3 prototype (GDD §2v3.13). The dumb one taps at random; the smart one picks the tile
    /// worth the most per stamina, waits for the beat, waters when it has stamina to spare and reaps in one swipe.
    /// Both shop the same way in winter, so the tables measure play. <see cref="Skill"/> is the share of strikes the
    /// smart bot times right; the rest land whenever the cooldown allows, like a player who misread the pulse.
    /// </summary>
    public sealed class DigBot
    {
        public readonly DigSim Sim;
        public readonly bool Smart;
        public readonly float Skill;
        public float Dt = 0.05f;
        private readonly Rng _rng;
        private bool _nextStrikeOffBeat;
        private readonly List<DigTile> _swipe = new List<DigTile>();
        private readonly List<YearRow> _rows = new List<YearRow>();

        public struct YearRow
        {
            public int Year;
            public double Coins, FromBreaks, FromCrops;
            public int Strikes, Crits, Breaks, Reaped, Swipes, CrowsEaten, MaxLayer;
            public float StaminaEmpty;
            public double Damage;
            public float CritWindow, StaminaMax;
            public int Tier;
        }

        public IReadOnlyList<YearRow> Rows => _rows;

        public DigBot(DigSim sim, bool smart, float skill, int seed)
        {
            Sim = sim;
            Smart = smart;
            Skill = skill;
            _rng = new Rng(seed * 7919 + 13);
        }

        public void RunYears(int years)
        {
            for (int y = 0; y < years; y++)
            {
                int guard = 0;
                while (!Sim.Winter && guard++ < 1_000_000)
                {
                    Act();
                    Sim.Tick(Dt);
                }
                _rows.Add(Snapshot());
                Sim.ShopGreedy();
                Sim.StartNextYear();
            }
        }

        // Running totals at the last snapshot, so each row is one year's worth.
        private double _cumBreaks, _cumCrops;
        private int _cumStrikes, _cumCrits, _cumBreakCount, _cumReaped, _cumSwipes, _cumCrowsEaten;
        private float _cumEmpty;

        private YearRow Snapshot()
        {
            int maxLayer = 0;
            foreach (var t in Sim.Tiles) if (t.Layer > maxLayer) maxLayer = t.Layer;
            var row = new YearRow
            {
                Year = Sim.Year,
                Coins = Sim.CoinsThisYear,
                FromBreaks = Sim.CoinsFromBreaks - _cumBreaks,
                FromCrops = Sim.CoinsFromCrops - _cumCrops,
                Strikes = Sim.Strikes - _cumStrikes,
                Crits = Sim.Crits - _cumCrits,
                Breaks = Sim.Breaks - _cumBreakCount,
                Reaped = Sim.Reaped - _cumReaped,
                Swipes = Sim.Swipes - _cumSwipes,
                CrowsEaten = Sim.CrowsEaten - _cumCrowsEaten,
                MaxLayer = maxLayer,
                StaminaEmpty = Sim.SecondsStaminaEmpty - _cumEmpty,
                Damage = Sim.Damage,
                CritWindow = Sim.CritWindow,
                StaminaMax = Sim.StaminaMax,
                Tier = Sim.MaxTier,
            };
            _cumBreaks = Sim.CoinsFromBreaks; _cumCrops = Sim.CoinsFromCrops;
            _cumStrikes = Sim.Strikes; _cumCrits = Sim.Crits; _cumBreakCount = Sim.Breaks; _cumReaped = Sim.Reaped;
            _cumSwipes = Sim.Swipes; _cumCrowsEaten = Sim.CrowsEaten; _cumEmpty = Sim.SecondsStaminaEmpty;
            return row;
        }

        private void Act()
        {
            var sim = Sim;
            // Crows first: a tap is free and saves the crop (both bots see a crow; the dumb one only by luck).
            foreach (var t in sim.Tiles)
                if (t.Crow && (Smart || _rng.NextDouble() < 0.02))
                {
                    sim.TapCrow(t);
                    return;
                }

            if (!Smart)
            {
                if (!sim.CanStrike) return;
                var t = sim.Tiles[_rng.Next(sim.Tiles.Length)];
                if (t.State == TileState.Hard) sim.Strike(t);
                else if (t.State == TileState.Ripe) sim.Reap(new[] { t });
                return;
            }

            // Reap everything ripe in one swipe.
            _swipe.Clear();
            foreach (var t in sim.Tiles) if (t.State == TileState.Ripe) _swipe.Add(t);
            if (_swipe.Count > 0)
            {
                sim.Reap(_swipe);
                return;
            }

            // Pick the hard tile worth the most per hit (the type is visible, the bonus is not: use its expectation).
            DigTile best = null;
            double bestScore = 0;
            foreach (var t in sim.Tiles)
            {
                if (t.State != TileState.Hard) continue;
                double expected = ExpectedBreak(t) + sim.Config.Crops[sim.MaxTier].Value * (1 + sim.Config.DepthValue * t.Layer);
                double score = expected / Math.Max(1, sim.HitsLeft(t));
                if (score > bestScore) { bestScore = score; best = t; }
            }

            if (best != null && sim.CanStrike)
            {
                if (_nextStrikeOffBeat || sim.OnBeat)
                {
                    sim.Strike(best);
                    _nextStrikeOffBeat = _rng.NextDouble() > Skill; // decided per strike: did I read the next pulse right?
                }
                return;
            }

            // Nothing to strike (or no stamina): water the crop closest to ripe while the depot is more than half full.
            DigTile grow = null;
            foreach (var t in sim.Tiles)
            {
                if (t.State != TileState.Growing) continue;
                if (grow == null || t.Growth > grow.Growth) grow = t;
            }
            foreach (var t in sim.Tiles) if (t.Watering && t != grow) sim.SetWatering(t, false);
            if (grow != null) sim.SetWatering(grow, best == null && sim.Stamina > sim.StaminaMax * 0.5f);
        }

        private double ExpectedBreak(DigTile t)
        {
            var c = Sim.Config;
            double bonus = c.BreakCoins * Math.Pow(c.BreakGrowth, t.Layer);
            if (t.Golden) return bonus * c.GoldCoinsMult;
            if (t.Chest) return bonus * c.ChestMult;
            return bonus;
        }

        // ------------------------------------------------------------------ tables

        public double TotalCoins { get { double s = 0; foreach (var r in _rows) s += r.Coins; return s; } }
        public int TotalStrikes { get { int s = 0; foreach (var r in _rows) s += r.Strikes; return s; } }
        public int TotalCrits { get { int s = 0; foreach (var r in _rows) s += r.Crits; return s; } }

        public string ToTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("year   coins  breaks%  strikes crit%  breaks reaped swipes crows maxL  dmg  crit  stam tier  empty s");
            foreach (var r in _rows)
            {
                double breakShare = r.Coins > 0 ? (r.FromBreaks / r.Coins) : 0;
                double critRate = r.Strikes > 0 ? (double)r.Crits / r.Strikes : 0;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,4} {1,7:0} {2,7:0%} {3,8} {4,5:0%} {5,7} {6,6} {7,6} {8,5} {9,4} {10,4:0} {11,5:0.00} {12,5:0} {13,4} {14,7:0.0}",
                    r.Year, r.Coins, breakShare, r.Strikes, critRate, r.Breaks, r.Reaped, r.Swipes, r.CrowsEaten, r.MaxLayer,
                    r.Damage, r.CritWindow, r.StaminaMax, r.Tier, r.StaminaEmpty));
            }
            return sb.ToString();
        }
    }
}
