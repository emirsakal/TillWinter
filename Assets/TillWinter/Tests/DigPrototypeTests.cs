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
            var sim = New(c => { c.BaseHp = 6; c.Damage = 3; c.StrikeCooldown = 0f; c.SpringSoftness = 1f; c.BaseCritChance = 0; });
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
            var sim = New(c => { c.BaseHp = 100; c.Damage = 4; c.CritMult = 2.5; c.StrikeCooldown = 0f; c.BaseCritChance = 0; });
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
        public void Stamina_IsAMultiplier_NotAGate_TiredSwingsLandWeak()
        {
            var sim = New(c => { c.StaminaMax = 10; c.StrikeCost = 4; c.StaminaRegen = 1; c.StrikeCooldown = 0f; c.BaseHp = 1000; c.Damage = 10; c.SpringSoftness = 1f; c.BaseCritChance = 1; c.TiredDamage = 0.4; });
            var t = sim.Tiles[0];
            TickToBeat(sim, false);
            Assert.IsTrue(sim.Strike(t));
            Assert.IsTrue(sim.Strike(t));
            Assert.AreEqual(2f, sim.Stamina, 1e-5f);
            Assert.AreEqual(1000 - 2 * 25, t.Hp, 1e-6, "rested swings, both crits by chance (100%)");
            Assert.IsTrue(sim.Tired);
            Assert.IsTrue(sim.Strike(t), "the finger never waits");
            Assert.AreEqual(1000 - 50 - 4, t.Hp, 1e-6, "a tired swing: 40% of the damage, no crit even at 100% chance");
            Assert.AreEqual(2f, sim.Stamina, 1e-5f, "and it costs nothing");
            Assert.AreEqual(1, sim.TiredStrikes);
            Assert.IsFalse(sim.Winter, "the year did not end");
            sim.Tick(2f);
            Assert.IsFalse(sim.Tired, "regen refilled a rested swing");
            Assert.IsTrue(sim.Reap(new List<DigTile>()) == 0, "reaping is free");
        }

        [Test]
        public void Reaping_GivesStaminaBack()
        {
            var sim = New(c => { c.ReapStamina = 8; c.StaminaMax = 100; c.StrikeCooldown = 0f; c.StrikeCost = 3; c.BaseHp = 1e9; });
            sim.Tiles[0].State = TileState.Ripe;
            sim.Tiles[1].State = TileState.Ripe;
            for (int i = 0; i < 20; i++) sim.Strike(sim.Tiles[2]); // spend 60
            float before = sim.Stamina;
            Assert.AreEqual(40f, before, 1e-4f);
            sim.Reap(new[] { sim.Tiles[0], sim.Tiles[1] });
            Assert.AreEqual(before + 16, sim.Stamina, 1e-4f, "two crops, sixteen stamina: the ground gives back");
        }

        [Test]
        public void CritChance_LandsCritsOffTheBeat_AndIsCappedByTheShop()
        {
            var sim = New(c => { c.BaseHp = 1e9; c.Damage = 4; c.StrikeCooldown = 0f; c.SpringSoftness = 1f; c.BaseCritChance = 0.5; c.StaminaMax = 10000; c.StaminaRegen = 0; c.StrikeCost = 1; });
            var t = sim.Tiles[0];
            int crits = 0;
            for (int i = 0; i < 400; i++)
            {
                TickToBeat(sim, false);
                int before = sim.Crits;
                sim.Strike(t);
                if (sim.Crits > before) crits++;
            }
            Assert.That(crits, Is.InRange(140, 260), "about half the off-beat swings crit by chance");
            var shop = New(c => { c.BaseCritChance = 0.48; c.MaxCritChance = 0.5; c.UpgradeCritChance = 0.03; });
            shop.Tiles[0].State = TileState.Ripe;
            for (int i = 0; i < 400 && !shop.Winter; i++) shop.Tick(0.5f);
            Assert.IsTrue(shop.Winter);
            typeof(DigSim).GetProperty("Coins").SetValue(shop, 1e9);
            Assert.IsTrue(shop.Buy(DigSim.Upgrade.CritChance));
            Assert.AreEqual(0.5, shop.CritChance, 1e-9, "capped");
            Assert.IsFalse(shop.Buy(DigSim.Upgrade.CritChance), "nothing left to buy");
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
        public void Winter_KeepsTheDepth_RefillsHp_ReapsTheRipe_LetsTheGrowingWait()
        {
            var sim = New(c => { c.YearLength = 5f; c.BaseHp = 10; c.ComboPerCrop = 0.5; c.Crops[0].Grow = 1000f; }); // a slow crop: it must still be growing at the frost
            sim.Tiles[0].State = TileState.Growing;
            sim.Tiles[0].Growth = 0.5f;
            sim.Tiles[1].Layer = 3;
            sim.Tiles[1].Hp = 4; // half-broken
            sim.Tiles[1].MaxHp = 12;
            sim.Tiles[2].State = TileState.Ripe;
            sim.Tiles[3].State = TileState.Ripe;
            double one = sim.CropValue(sim.Tiles[2]);
            double coins = sim.Coins;
            for (int i = 0; i < 200 && !sim.Winter; i++) sim.Tick(0.05f);
            Assert.IsTrue(sim.Winter);
            Assert.AreEqual(coins + 2 * one, sim.Coins, 1e-6, "the frost reaped both ripe crops, one at a time: no combo");
            Assert.AreEqual(2, sim.FrostReaped);
            Assert.AreEqual(TileState.Hard, sim.Tiles[2].State);
            Assert.AreEqual(1, sim.Tiles[2].Layer, "and the ground came back a layer deeper");
            Assert.AreEqual(12, sim.Tiles[1].Hp, 1e-6, "winter closes the half-made crack");
            Assert.AreEqual(TileState.Growing, sim.Tiles[0].State, "a growing crop waits");
            Assert.That(sim.Tiles[0].Growth, Is.InRange(0.5f, 0.6f), "it grew a little and stopped for the winter");
            Assert.IsFalse(sim.Strike(sim.Tiles[4]), "nothing happens in winter");
            Assert.AreEqual(0, sim.Reap(new[] { sim.Tiles[4] }), "nor is anything reaped by hand");
            coins = sim.Coins;
            sim.StartNextYear();
            Assert.AreEqual(2, sim.Year);
            Assert.AreEqual(coins, sim.Coins);
            Assert.AreEqual(3, sim.Tiles[1].Layer, "depth persists into the next year");
            Assert.AreEqual(sim.StaminaMax, sim.Stamina, "spring starts with a full depot");
        }

        [Test]
        public void Rebirth_ReturnsTheFieldToTheSurface()
        {
            var sim = New();
            sim.Tiles[0].Layer = 9;
            sim.Tiles[1].State = TileState.Growing;
            sim.Tiles[2].State = TileState.Ripe;
            sim.ResetField();
            foreach (var t in sim.Tiles)
            {
                Assert.AreEqual(0, t.Layer);
                Assert.AreEqual(TileState.Hard, t.State);
                Assert.AreEqual(t.MaxHp, t.Hp, 1e-9);
                Assert.AreEqual(GroundType.Clay, t.Ground);
            }
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
