using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core.Dig;

namespace TillWinter.Tests
{
    /// <summary>
    /// Core loop v3 prototype (GDD §2v3): the tile cycle, the timed strike, stamina as a budget, the swipe, crows,
    /// winter without penalty — and the acceptance gate: play must matter (§2v3.13).
    /// </summary>
    public class DigPrototypeTests
    {
        private static DigSim New(System.Action<DigConfig> tweak = null, int seed = 1)
        {
            var cfg = new DigConfig { ChestChance = 0, GoldChance = 0, CrowChancePerSecond = 0 };
            tweak?.Invoke(cfg);
            return new DigSim(cfg, seed);
        }

        /// <summary>Ticks until the pulse is on (or off) the beat, so a test controls the crit.</summary>
        private static void TickToBeat(DigSim sim, bool onBeat)
        {
            for (int i = 0; i < 400 && sim.OnBeat != onBeat; i++) sim.Tick(0.01f);
            Assert.AreEqual(onBeat, sim.OnBeat, "reached the wanted phase");
        }

        [Test]
        public void Tile_StartsHard_BreaksToGrowing_RipensToRipe_ReapsToHarderGround()
        {
            var sim = New(c => { c.BaseHp = 6; c.Damage = 3; c.StrikeCooldown = 0f; c.SpringSoftness = 1f; });
            var t = sim.Tiles[0];
            Assert.AreEqual(TileState.Hard, t.State);
            Assert.AreEqual(0, t.Layer);
            TickToBeat(sim, false);
            Assert.IsTrue(sim.Strike(t));
            Assert.AreEqual(3, t.Hp, 1e-6);
            Assert.AreEqual(TileState.Hard, t.State);
            Assert.IsTrue(sim.Strike(t));
            Assert.AreEqual(TileState.Growing, t.State, "two plain hits break clay");
            Assert.That(sim.Coins, Is.GreaterThan(0), "the break paid its hidden bonus");
            for (int i = 0; i < 100 && t.State == TileState.Growing; i++) sim.Tick(0.05f);
            Assert.AreEqual(TileState.Ripe, t.State, "the carrot grew on its own");
            double before = sim.Coins;
            sim.Reap(new[] { t });
            Assert.That(sim.Coins, Is.GreaterThan(before));
            Assert.AreEqual(TileState.Hard, t.State, "the next layer is waiting");
            Assert.AreEqual(1, t.Layer);
            Assert.That(t.MaxHp, Is.GreaterThan(6), "and it is tougher");
        }

        [Test]
        public void Strike_OnTheBeat_IsACrit()
        {
            var sim = New(c => { c.BaseHp = 100; c.Damage = 4; c.CritMult = 2.5; c.StrikeCooldown = 0f; });
            var t = sim.Tiles[0];
            TickToBeat(sim, true);
            sim.Strike(t);
            Assert.AreEqual(100 - 4 * 2.5 * sim.Config.SpringSoftness, t.Hp, 1e-6, "crit in spring, on soft ground");
            Assert.AreEqual(1, sim.Crits);
            TickToBeat(sim, false);
            sim.Strike(t);
            Assert.AreEqual(100 - 4 * 2.5 * sim.Config.SpringSoftness - 4 * sim.Config.SpringSoftness, t.Hp, 1e-6);
            Assert.AreEqual(1, sim.Crits);
        }

        [Test]
        public void Stamina_IsABudget_NotAClock()
        {
            var sim = New(c => { c.StaminaMax = 10; c.StrikeCost = 4; c.StaminaRegen = 1; c.StrikeCooldown = 0f; c.BaseHp = 1000; });
            var t = sim.Tiles[0];
            Assert.IsTrue(sim.Strike(t));
            Assert.IsTrue(sim.Strike(t));
            Assert.IsFalse(sim.Strike(t), "2 stamina left: no third strike");
            Assert.IsFalse(sim.Winter, "and the year did not end");
            sim.Tick(2f);
            Assert.IsTrue(sim.Strike(t), "regen refilled a strike");
            Assert.IsTrue(sim.Reap(new List<DigTile>()) == 0, "reaping is free");
        }

        [Test]
        public void Swipe_ReapsEveryRipeTile_WithACombo_AndIgnoresTheRest()
        {
            var sim = New(c => c.ComboPerCrop = 0.1);
            foreach (var t in sim.Tiles) { t.State = TileState.Ripe; }
            sim.Tiles[8].State = TileState.Hard;
            double one = sim.CropValue(sim.Tiles[0]);
            double coins = sim.Reap(sim.Tiles);
            Assert.AreEqual(8 * one * (1 + 0.1 * 7), coins, 1e-6, "eight crops, combo 1.7");
            Assert.AreEqual(TileState.Hard, sim.Tiles[8].State, "the hard tile was skipped");
            foreach (var t in sim.Tiles) Assert.AreEqual(TileState.Hard, t.State);
        }

        [Test]
        public void Crow_TappedInTime_ReapsTheCrop_Untouched_EatsIt()
        {
            var sim = New(c => { c.CrowChancePerSecond = 1000; c.CrowMinRipe = 0; c.CrowWait = 1f; });
            var t = sim.Tiles[0];
            t.State = TileState.Ripe;
            sim.Tick(0.05f);
            Assert.IsTrue(t.Crow, "a crow landed");
            double before = sim.Coins;
            Assert.IsTrue(sim.TapCrow(t));
            Assert.That(sim.Coins, Is.GreaterThan(before), "scared and reaped");
            Assert.AreEqual(1, t.Layer);

            var u = sim.Tiles[1];
            u.State = TileState.Ripe;
            for (int i = 0; i < 60 && !u.Crow; i++) sim.Tick(0.05f);
            Assert.IsTrue(u.Crow);
            before = sim.Coins;
            for (int i = 0; i < 40; i++) sim.Tick(0.05f);
            Assert.AreEqual(before, sim.Coins, "eaten: nothing paid");
            Assert.AreEqual(1, sim.CrowsEaten);
            Assert.AreEqual(TileState.Hard, u.State, "the ground still comes back");
        }

        [Test]
        public void Winter_EndsTheYear_KeepsEverything_ChargesNothing()
        {
            var sim = New(c => c.YearLength = 5f);
            sim.Tiles[0].State = TileState.Growing;
            sim.Tiles[0].Growth = 0.5f;
            sim.Tiles[1].Layer = 3;
            for (int i = 0; i < 200 && !sim.Winter; i++) sim.Tick(0.05f);
            Assert.IsTrue(sim.Winter);
            double coins = sim.Coins;
            Assert.IsFalse(sim.Strike(sim.Tiles[2]), "nothing happens in winter");
            sim.StartNextYear();
            Assert.AreEqual(2, sim.Year);
            Assert.AreEqual(coins, sim.Coins);
            Assert.AreEqual(3, sim.Tiles[1].Layer, "depth persists");
            Assert.AreEqual(sim.StaminaMax, sim.Stamina, "spring starts with a full depot");
        }

        [Test]
        public void Acceptance_SmartPlayOutEarnsRandomPlay_AndTimingSkillMatters()
        {
            const int years = 8;
            var dumb = new DigBot(new DigSim(new DigConfig(), 1), false, 0f, 1);
            var smart = new DigBot(new DigSim(new DigConfig(), 1), true, 0.8f, 1);
            var clumsy = new DigBot(new DigSim(new DigConfig(), 1), true, 0.3f, 1);
            dumb.RunYears(years);
            smart.RunYears(years);
            clumsy.RunYears(years);
            TestContext.WriteLine("dumb\n" + dumb.ToTable());
            TestContext.WriteLine("smart 0.8\n" + smart.ToTable());
            TestContext.WriteLine("smart 0.3\n" + clumsy.ToTable());
            Assert.That(smart.TotalCoins, Is.GreaterThanOrEqualTo(dumb.TotalCoins * 1.5), "play must matter: smart >= 1.5x dumb (GDD §2v3.13)");
            Assert.That(smart.TotalCoins, Is.GreaterThan(clumsy.TotalCoins * 1.15), "timing must matter: on-beat 0.8 beats 0.3 by 15%");
        }
    }
}
