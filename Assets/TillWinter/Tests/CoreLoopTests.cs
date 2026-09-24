using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// Core loop v3 (GDD §2v3): strike hard ground on the beat, let the crop grow, water it by holding, reap it with a
    /// swipe, meet the next layer. Rule tests on <see cref="TestConfig.Classic"/> (carrot 1 coin, no specials, no depth
    /// bonus, no season softness) so a tuning pass cannot move them.
    /// </summary>
    public class CoreLoopTests
    {
        private const float Dt = 0.01f;

        /// <summary>A sim whose hoe never crits by chance: timing is the only crit.</summary>
        internal static FarmSim NewSim(Action<FarmConfig> tweak = null, int seed = 1)
        {
            var cfg = TestConfig.Classic();
            cfg.BaseCritChance = 0;
            tweak?.Invoke(cfg);
            return new FarmSim(cfg, seed);
        }

        internal static void Run(FarmSim sim, float seconds, float dt = Dt)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt);
        }

        /// <summary>Ticks until the field's beat is inside the crit window (or not), at most one pulse.</summary>
        internal static void WaitForBeat(FarmSim sim, bool onBeat)
        {
            for (int i = 0; i < 200 && sim.State.OnBeat != onBeat; i++) sim.Tick(Dt);
            Assert.AreEqual(onBeat, sim.State.OnBeat, "beat state reachable");
        }

        private static readonly GridPos Centre = new GridPos(1, 1);

        // ---------------------------------------------------------------- the tile cycle (GDD §2v3.2)

        [Test]
        public void AFreshField_IsHardClay_AtLayer0_WithFullHp_AndNothingGrowing()
        {
            var sim = NewSim();
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(PlotState.Hard, p.State);
                Assert.AreEqual(0, p.Layer);
                Assert.AreEqual(GroundType.Clay, p.Ground);
                Assert.AreEqual(sim.Config.BaseHp, p.MaxHp, 1e-9);
                Assert.AreEqual(p.MaxHp, p.Hp, 1e-9);
                Assert.AreEqual(0f, p.Cracks, 1e-6f);
                Assert.IsFalse(p.Hardpan);
                Assert.IsFalse(p.Chest);
            }
            Assert.AreEqual(sim.Config.BaseStaminaMax, sim.State.Stamina, 1e-6f);
        }

        [Test]
        public void Strike_TakesDamageOffTheGround_CostsStamina_AndBreaksIt_ThenTheSeedDrops()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(Centre);
            var struck = new List<StrikeEvent>();
            var broke = new List<BreakEvent>();
            sim.Struck += struck.Add;
            sim.PlotBroken += broke.Add;
            WaitForBeat(sim, false);
            float stamina = sim.State.Stamina;
            Assert.IsTrue(sim.Strike(Centre));
            Assert.AreEqual(sim.Config.BaseHp - sim.Config.BaseStrikeDamage, plot.Hp, 1e-9, "3 damage off 10 HP");
            Assert.AreEqual(stamina - sim.Config.StrikeCost, sim.State.Stamina, 1e-6f);
            Assert.AreEqual(1, struck.Count);
            Assert.IsFalse(struck[0].Crit);
            Assert.IsFalse(struck[0].Tired);
            Assert.AreEqual(Centre, struck[0].Pos);
            Assert.AreEqual(0.3f, plot.Cracks, 1e-5f);
            Assert.AreEqual(1, sim.State.Generation.Strikes);

            // 10 HP at 3 a hit (no crit) is four strikes; the cooldown between them.
            for (int i = 0; i < 3; i++)
            {
                Run(sim, sim.Config.BaseStrikeCooldown + Dt);
                WaitForBeat(sim, false);
                Assert.IsTrue(sim.Strike(Centre), "strike " + (i + 2));
            }
            Assert.AreEqual(PlotState.Growing, plot.State, "the seed dropped");
            Assert.AreEqual(0f, plot.Progress);
            Assert.AreEqual(1, broke.Count);
            Assert.AreEqual(0, broke[0].Layer);
            Assert.AreEqual(sim.Config.BreakCoins, broke[0].Coins, 1e-9, "the layer's break bonus");
            Assert.AreEqual(sim.Config.BreakCoins, sim.State.Coins, 1e-9);
            Assert.AreEqual(1, sim.State.Generation.Breaks);
            Assert.AreEqual(0, plot.CropLayer);
        }

        [Test]
        public void Strike_RefusesGrowingAndRipeGround_AndWithinTheCooldown()
        {
            var sim = NewSim();
            Assert.IsTrue(sim.Strike(Centre));
            Assert.IsFalse(sim.Strike(Centre), "cooldown");
            Assert.IsFalse(sim.CanStrike);
            Run(sim, sim.Config.BaseStrikeCooldown + Dt);
            Assert.IsTrue(sim.CanStrike);
            Assert.IsTrue(sim.DebugBreak(Centre));
            Assert.IsFalse(sim.Strike(Centre), "growing ground takes no strike");
            sim.DebugForceRipeAll();
            Assert.IsFalse(sim.Strike(Centre), "ripe ground takes no strike");
            Assert.IsFalse(sim.Strike(new GridPos(9, 9)), "off the field");
        }

        [Test]
        public void ABrokenPlot_GrowsInItsCropsTime_ThenRipens()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(Centre);
            int ripened = 0;
            sim.PlotRipened += _ => ripened++;
            sim.DebugBreak(Centre);
            float grow = sim.Config.Crops[0].Grow; // carrot 2.5 s
            Run(sim, grow * 0.5f);
            Assert.That(plot.Progress, Is.EqualTo(0.5f).Within(0.02f));
            Assert.AreEqual(PlotState.Growing, plot.State);
            Run(sim, grow * 0.5f + 0.05f);
            Assert.AreEqual(PlotState.Ripe, plot.State);
            Assert.AreEqual(1, ripened);
            Assert.AreEqual(0f, plot.RipeAge, 0.1f);
        }

        [Test]
        public void Reap_PaysTheCrop_RefillsStamina_AndTheGroundReturnsALayerDeeper_TougherAndRicher()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(Centre);
            sim.DebugBreak(Centre);
            Run(sim, 3f);
            Assert.AreEqual(PlotState.Ripe, plot.State);
            sim.DebugSetStamina(50f);
            var harvests = new List<HarvestEvent>();
            sim.Harvested += harvests.Add;
            double coins = sim.State.Coins;
            Assert.AreEqual(1, sim.Reap(new[] { Centre }));
            Assert.AreEqual(coins + 1, sim.State.Coins, 1e-9, "a carrot is one coin");
            Assert.AreEqual(50f + sim.Config.ReapStamina, sim.State.Stamina, 1e-6f, "the ground gives back");
            Assert.AreEqual(1, harvests.Count);
            Assert.AreEqual(HarvestSource.Hand, harvests[0].Source);
            Assert.AreEqual(1, harvests[0].Combo);
            Assert.AreEqual(PlotState.Hard, plot.State);
            Assert.AreEqual(1, plot.Layer);
            Assert.AreEqual(sim.Config.BaseHp * sim.Config.HpGrowth, plot.MaxHp, 1e-9, "tougher");
            Assert.AreEqual(plot.MaxHp, plot.Hp, 1e-9);
            Assert.AreEqual(1, sim.State.Generation.HarvestsHand);
            sim.DebugBreak(Centre);
            Assert.AreEqual(sim.Config.BreakCoins + 1 + sim.Config.BreakCoins * sim.Config.BreakGrowth, sim.State.Coins, 1e-9, "richer");
        }

        [Test]
        public void ThePlotNeverFinishes_LayersRunOn_AndTheGroundTypeFollowsTheDepth()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(Centre);
            for (int layer = 0; layer < 10; layer++)
            {
                Assert.AreEqual(layer, plot.Layer);
                var expected = layer < 2 ? GroundType.Clay : layer < 4 ? GroundType.Stone : layer < 6 ? GroundType.Roots : layer < 8 ? GroundType.Gravel : GroundType.Rock;
                Assert.AreEqual(expected, plot.Ground, "layer " + layer);
                Assert.AreEqual(sim.Config.BaseHp * Math.Pow(sim.Config.HpGrowth, layer), plot.MaxHp, 1e-6);
                sim.DebugBreak(Centre);
                sim.DebugForceRipeAll();
                sim.ReapOne(Centre);
            }
            Assert.AreEqual(9, sim.State.Generation.DeepestLayer);
        }

        [Test]
        public void Bedrock_TheDeepestLayerComesBackAsItself_SoTheFieldNeverOutgrowsTheHoe()
        {
            var sim = NewSim(c => c.MaxLayer = 3);
            var plot = sim.State.GetPlot(Centre);
            sim.DebugSetLayer(Centre, 3);
            Assert.AreEqual(3, plot.Layer);
            double hp = plot.MaxHp;
            sim.DebugBreak(Centre);
            sim.DebugForceRipe(Centre);
            sim.ReapOne(Centre);
            Assert.AreEqual(3, plot.Layer, "a reap past bedrock brings the same layer back");
            Assert.AreEqual(hp, plot.MaxHp, 1e-9);
            Assert.AreEqual(3, sim.State.Generation.DeepestLayer);
            sim.DebugSetLayer(Centre, 99);
            Assert.AreEqual(3, plot.Layer, "the debug hook is capped too");
        }

        // ---------------------------------------------------------------- the beat and the crit (GDD §2v3.4)

        [Test]
        public void TheBeat_IsTheFieldsPulse_AndTheWindowIsCentredOnIt()
        {
            var sim = NewSim();
            float period = sim.Config.PulseSeconds;
            Run(sim, period * 0.5f);
            Assert.IsTrue(sim.State.OnBeat, "the beat itself");
            Assert.That(sim.State.Pulse, Is.EqualTo(0.5f).Within(0.03f));
            Run(sim, period * 0.5f);
            Assert.IsFalse(sim.State.OnBeat, "between beats");
            Run(sim, period * 0.5f - sim.Config.BaseCritWindow * period * 0.5f - 0.02f);
            Assert.IsFalse(sim.State.OnBeat, "just before the window");
            Run(sim, 0.03f);
            Assert.IsTrue(sim.State.OnBeat, "inside the window");
        }

        [Test]
        public void OnTheBeat_TheStrikeCritsForCertain_AtTwoAndAHalfTimesTheDamage()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(Centre);
            StrikeEvent last = default;
            sim.Struck += e => last = e;
            WaitForBeat(sim, true);
            Assert.IsTrue(sim.Strike(Centre));
            Assert.IsTrue(last.Crit);
            Assert.AreEqual(sim.Config.BaseStrikeDamage * sim.Config.CritMult, last.Damage, 1e-9);
            Assert.AreEqual(sim.Config.BaseHp - 7.5, plot.Hp, 1e-9);
            Assert.AreEqual(1, sim.State.Generation.Crits);
        }

        [Test]
        public void OffTheBeat_TheCritIsAChance_RaisedByLuckyHoe_AndCapped()
        {
            var sure = NewSim(c => { c.BaseCritChance = 1; c.MaxCritChance = 1; });
            StrikeEvent last = default;
            sure.Struck += e => last = e;
            WaitForBeat(sure, false);
            Assert.IsTrue(sure.Strike(Centre));
            Assert.IsTrue(last.Crit, "chance 1 crits off the beat");

            var never = NewSim();
            never.Struck += e => last = e;
            WaitForBeat(never, false);
            never.Strike(Centre);
            Assert.IsFalse(last.Crit, "chance 0 never crits off the beat");

            var cfg = TestConfig.Classic();
            var node = AlmanacData.Get("lucky_hoe");
            var one = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["lucky_hoe"] = 1 });
            Assert.AreEqual(cfg.BaseCritChance + node.ValuePerLevel, one.CritChance, 1e-9);
            var max = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["lucky_hoe"] = 99 });
            Assert.AreEqual(cfg.MaxCritChance, max.CritChance, 1e-9, "capped");
        }

        [Test]
        public void SteadyHand_WidensTheWindow_UpToItsCap()
        {
            var cfg = TestConfig.Classic();
            var node = AlmanacData.Get("steady_hand");
            var one = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["steady_hand"] = 1 });
            Assert.AreEqual(cfg.BaseCritWindow + node.ValuePerLevel, one.CritWindow, 1e-6);
            var max = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["steady_hand"] = 99 });
            Assert.AreEqual(cfg.MaxCritWindow, max.CritWindow, 1e-6);
        }

        // ---------------------------------------------------------------- stamina and the tired swing (GDD §2v3.5)

        [Test]
        public void WithNoStamina_TheSwingIsTired_FortyPercent_NoCrit_NoCost()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(Centre);
            StrikeEvent last = default;
            sim.Struck += e => last = e;
            sim.DebugSetStamina(1f);
            Assert.IsTrue(sim.State.Tired);
            WaitForBeat(sim, true); // even on the beat
            Assert.IsTrue(sim.Strike(Centre));
            Assert.IsTrue(last.Tired);
            Assert.IsFalse(last.Crit);
            Assert.AreEqual(sim.Config.BaseStrikeDamage * sim.Config.TiredDamage, last.Damage, 1e-9);
            Assert.AreEqual(sim.Config.BaseHp - 1.2, plot.Hp, 1e-9);
            Assert.That(sim.State.Stamina, Is.GreaterThanOrEqualTo(1f), "a tired swing costs nothing");
        }

        [Test]
        public void Stamina_RegeneratesPerSecond_AndNeverPastTheDepot()
        {
            var sim = NewSim();
            sim.DebugSetStamina(10f);
            Run(sim, 5f);
            Assert.That(sim.State.Stamina, Is.EqualTo(10f + 5f * sim.Config.BaseStaminaRegen).Within(0.05f));
            Run(sim, 60f);
            Assert.AreEqual(sim.Config.BaseStaminaMax, sim.State.Stamina, 1e-4f);
            var depot = StatResolver.Resolve(TestConfig.Classic(), new Dictionary<string, int> { ["stamina_depot"] = 2, ["stamina_regen"] = 2 });
            Assert.AreEqual(100 + 30, depot.StaminaMax, 1e-6);
            Assert.AreEqual(2 + 0.5, depot.StaminaRegen, 1e-6);
        }

        // ---------------------------------------------------------------- watering (GDD §2v3.6)

        [Test]
        public void HoldingOnAGrowingCrop_WatersIt_ThreeTimesFaster_ForStaminaPerSecond()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(Centre);
            sim.DebugBreak(Centre);
            sim.SetWatering(Centre);
            Run(sim, 0.5f);
            Assert.IsTrue(plot.Watering);
            float expected = 0.5f * sim.Config.WaterBoost / sim.Config.Crops[0].Grow;
            Assert.That(plot.Progress, Is.EqualTo(expected).Within(0.02f));
            float spent = sim.Config.BaseStaminaMax - sim.State.Stamina;
            Assert.That(spent, Is.EqualTo(0.5f * (sim.Config.WaterCostPerSecond - sim.Config.BaseStaminaRegen)).Within(0.1f));
            sim.SetWatering(null);
            Run(sim, Dt);
            Assert.IsFalse(plot.Watering);
        }

        [Test]
        public void Watering_StopsWhenTheDepotIsEmpty_AndDoesNothingOnHardOrRipeGround()
        {
            var sim = NewSim(c => c.BaseStaminaRegen = 0f);
            var plot = sim.State.GetPlot(Centre);
            sim.DebugBreak(Centre);
            sim.DebugSetStamina(0f);
            sim.SetWatering(Centre);
            Run(sim, 0.2f);
            Assert.IsFalse(plot.Watering, "no stamina, no boost");
            Assert.That(plot.Progress, Is.EqualTo(0.2f / sim.Config.Crops[0].Grow).Within(0.02f), "grows at the plain rate");

            var hard = NewSim();
            hard.SetWatering(Centre);
            Run(hard, 0.5f);
            Assert.AreEqual(hard.Config.BaseStaminaMax, hard.State.Stamina, 1e-4f, "hard ground drinks nothing");
        }

        // ---------------------------------------------------------------- reaping (GDD §2v3.7)

        [Test]
        public void ASwipe_ReapsEveryRipePlotOnItsPath_IgnoresTheRest_AndPaysTheCombo()
        {
            var sim = NewSim();
            var events = new List<HarvestEvent>();
            sim.Harvested += events.Add;
            int reapedN = 0; double reapedCoins = 0;
            sim.Reaped += (n, c) => { reapedN = n; reapedCoins = c; };
            sim.DebugBreak(new GridPos(0, 0));
            sim.DebugBreak(new GridPos(1, 0));
            sim.DebugBreak(new GridPos(2, 0));
            sim.DebugBreak(new GridPos(0, 1));
            Run(sim, 3f); // four ripe carrots
            double coins = sim.State.Coins;
            // The path crosses three ripe plots, a hard one and one ripe plot twice.
            int n = sim.Reap(new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0), new GridPos(2, 1), new GridPos(0, 0) });
            Assert.AreEqual(3, n);
            Assert.AreEqual(3, sim.State.Combo);
            Assert.AreEqual(3, sim.State.Generation.BestCombo);
            double combo = 1 + sim.Config.ComboPerCrop * 2;
            Assert.AreEqual(coins + 3 * combo, sim.State.Coins, 1e-9, "x1.2 on each of three");
            Assert.AreEqual(3, reapedN);
            Assert.AreEqual(3 * combo, reapedCoins, 1e-9);
            Assert.AreEqual(3, events.Count);
            foreach (var e in events) Assert.AreEqual(3, e.Combo);
            Assert.AreEqual(PlotState.Ripe, sim.State.GetPlot(0, 1).State, "off the path");
            Assert.AreEqual(0, sim.Reap(new[] { new GridPos(2, 2) }), "nothing ripe on the path");
            Run(sim, sim.Config.ComboWindowSeconds + 0.1f);
            Assert.AreEqual(0, sim.State.Combo, "the combo count fades");
        }

        [Test]
        public void ReapCombo_RaisesTheBonusPerCrop_AndAutumnStretchesIt()
        {
            var cfg = TestConfig.Classic();
            var node = AlmanacData.Get("reap_combo");
            var s = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["reap_combo"] = 2 });
            Assert.AreEqual(cfg.ComboPerCrop + 2 * node.ValuePerLevel, s.ComboPerCrop, 1e-9);

            var sim = NewSim(c => c.AutumnCombo = 2f);
            sim.DebugSetSeason(Season.Autumn);
            Assert.AreEqual(Season.Autumn, sim.State.Season);
            sim.DebugForceRipeAll();
            double coins = sim.State.Coins;
            Assert.AreEqual(9, sim.ReapAll());
            Assert.AreEqual(coins + 9 * (1 + 0.1 * 2 * 8), sim.State.Coins, 1e-9);
        }

        [Test]
        public void ASwipeMilestone_PaysABonus_OnceAtThatLength()
        {
            var sim = NewSim(c => { c.ComboMilestones = new[] { 3 }; c.ComboMilestoneBonus = new[] { 5.0 }; });
            int fired = 0;
            sim.ComboMilestone += (n, b) => { fired++; Assert.AreEqual(3, n); Assert.AreEqual(5, b, 1e-9); };
            sim.DebugForceRipeAll();
            double coins = sim.State.Coins;
            sim.Reap(new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0) });
            Assert.AreEqual(1, fired);
            Assert.AreEqual(coins + 3 * 1.2 + 5, sim.State.Coins, 1e-9);
            sim.Reap(new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(0, 2) });
            Assert.AreEqual(1, fired, "four is not the milestone");
        }

        [Test]
        public void ATap_ReapsOneRipePlot_AndDoesNothingOnHardGround()
        {
            var sim = NewSim();
            Assert.IsFalse(sim.TapAt(Centre), "hard ground is the strike's");
            Assert.IsTrue(sim.Press(Centre), "but a press strikes it");
            sim.DebugBreak(Centre);
            Run(sim, 3f);
            Assert.IsTrue(sim.TapAt(Centre));
            Assert.AreEqual(1, sim.State.Combo);
            Assert.AreEqual(PlotState.Hard, sim.State.GetPlot(Centre).State);
        }

        // ---------------------------------------------------------------- crows (GDD §2v3.8)

        [Test]
        public void ACrow_LandsOnACropThatHasStoodRipe_WaitsFourSeconds_ThenEatsIt_AndTheGroundReturnsDeeper()
        {
            var sim = NewSim(c => { c.CrowFirstYear = 1; c.CrowSpawnChance = 1f; c.CrowSpawnInterval = 0.5f; });
            var plot = sim.State.GetPlot(Centre);
            int landed = 0, ate = 0;
            sim.CrowLanded += _ => landed++;
            sim.CrowAte += _ => ate++;
            sim.DebugBreak(Centre);
            Run(sim, 2.6f); // ripe at 2.5 s
            Assert.AreEqual(PlotState.Ripe, plot.State);
            Run(sim, sim.Config.CrowMinRipe - 0.2f);
            Assert.AreEqual(0, landed, "not before the crop has stood a while");
            Run(sim, 0.8f);
            Assert.AreEqual(1, landed);
            Assert.IsTrue(plot.HasCrow);
            Run(sim, sim.Config.CrowEatTime + 0.1f);
            Assert.AreEqual(1, ate);
            Assert.AreEqual(PlotState.Hard, plot.State);
            Assert.AreEqual(1, plot.Layer, "the ground came back a layer deeper all the same");
            Assert.AreEqual(1, sim.State.CropsLostThisYear);
            Assert.AreEqual(0, sim.State.Crows.Count);
        }

        [Test]
        public void TappingACrow_ScaresIt_PaysTheBounty_AndReapsTheCropUnderIt()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(Centre);
            sim.DebugBreak(Centre);
            Run(sim, 3f);
            Assert.IsTrue(sim.DebugSpawnCrow());
            var crowAt = sim.State.Crows[0].Pos;
            var crowPlot = sim.State.GetPlot(crowAt);
            Assert.IsTrue(crowPlot.IsRipe);
            double coins = sim.State.Coins;
            int scared = 0;
            sim.CrowScared += e => { scared++; Assert.AreEqual(sim.Config.CrowScareValueMultiplier * 1, e.Coins, 1e-9); };
            Assert.IsTrue(sim.TapAt(crowAt));
            Assert.AreEqual(1, scared);
            Assert.AreEqual(coins + 2 + 1, sim.State.Coins, 1e-9, "the bounty and the crop");
            Assert.AreEqual(PlotState.Hard, crowPlot.State);
            Assert.AreEqual(1, sim.State.Generation.CrowsScared);
            Assert.AreEqual(1, sim.State.Generation.HarvestsHand);
        }

        // ---------------------------------------------------------------- seasons (GDD §2v3.3)

        [Test]
        public void SpringSoftensTheGround_SummerHardensItAndSlowsGrowth()
        {
            var sim = NewSim(c => { c.SpringSoftness = 2f; c.SummerHardness = 0.5f; c.SummerGrowth = 0.5f; });
            var plot = sim.State.GetPlot(Centre);
            WaitForBeat(sim, false);
            sim.Strike(Centre);
            Assert.AreEqual(10 - 6, plot.Hp, 1e-9, "spring: x2");
            sim.DebugSetSeason(Season.Summer);
            Assert.AreEqual(Season.Summer, sim.State.Season);
            sim.DebugClearCooldown();
            WaitForBeat(sim, false);
            sim.Strike(Centre);
            Assert.AreEqual(4 - 1.5, plot.Hp, 1e-9, "summer: x0.5");
            sim.DebugBreak(Centre);
            Run(sim, 1f);
            Assert.That(plot.Progress, Is.EqualTo(0.5f / sim.Config.Crops[0].Grow).Within(0.02f), "summer growth x0.5");
        }

        // ---------------------------------------------------------------- winter and rebirth (GDD §2v3.2 v3.2)

        [Test]
        public void Winter_KeepsTheDepth_ClosesTheCracks_ReapsWhatIsRipe_AndAGrowingCropWaits()
        {
            var sim = NewSim(c => c.Crops[0].Grow = 1000f);
            var hard = sim.State.GetPlot(0, 0);
            var ripe = sim.State.GetPlot(1, 0);
            var growing = sim.State.GetPlot(2, 0);
            WaitForBeat(sim, false);
            sim.Strike(new GridPos(0, 0));
            Assert.AreEqual(7, hard.Hp, 1e-9);
            sim.DebugSetLayer(new GridPos(1, 0), 4);
            sim.DebugBreak(new GridPos(1, 0));
            sim.DebugForceRipe(new GridPos(1, 0));
            sim.DebugBreak(new GridPos(2, 0));
            Run(sim, 1f);
            Assert.IsTrue(growing.IsGrowing);
            float progress = growing.Progress;
            var harvests = new List<HarvestEvent>();
            sim.Harvested += harvests.Add;
            double coins = sim.State.Coins;
            sim.DebugSkipToWinter();
            Assert.AreEqual(Phase.Winter, sim.State.Phase);
            Assert.AreEqual(hard.MaxHp, hard.Hp, 1e-9, "winter closes the cracks");
            Assert.AreEqual(0, hard.Layer, "the layer stays");
            Assert.AreEqual(PlotState.Hard, ripe.State, "the frost reaped it");
            Assert.AreEqual(5, ripe.Layer, "L4 became L5");
            Assert.AreEqual(PlotState.Growing, growing.State, "a growing crop waits");
            Assert.AreEqual(progress, growing.Progress, 1e-6f);
            // The ripe plot is reaped by the frost: no combo, no stamina refund, the frost source.
            Assert.AreEqual(1, harvests.Count);
            Assert.AreEqual(HarvestSource.Frost, harvests[0].Source);
            Assert.AreEqual(1, harvests[0].Combo);
            Assert.AreEqual(coins + 1, sim.State.Coins, 1e-9, "one carrot at one coin");
            Assert.AreEqual(0, sim.State.Combo);

            sim.StartNextYear();
            Assert.AreEqual(0, hard.Layer);
            Assert.AreEqual(5, ripe.Layer);
            Assert.AreEqual(PlotState.Growing, growing.State);
            Assert.AreEqual(sim.Config.BaseStaminaMax, sim.State.Stamina, 1e-6f, "spring: a full depot");
        }

        [Test]
        public void WinterHpRefill_CanBePartial()
        {
            var sim = NewSim(c => c.WinterHpRefill = 0.5f);
            var plot = sim.State.GetPlot(Centre);
            WaitForBeat(sim, false);
            for (int i = 0; i < 3; i++) { sim.DebugClearCooldown(); WaitForBeat(sim, false); sim.Strike(Centre); }
            Assert.AreEqual(1, plot.Hp, 1e-9);
            sim.DebugSkipToWinter();
            Assert.AreEqual(5, plot.Hp, 1e-9, "half the cracks close");
            var deep = NewSim(c => c.WinterHpRefill = 0.5f);
            WaitForBeat(deep, false);
            deep.Strike(Centre);
            deep.DebugSkipToWinter();
            Assert.AreEqual(7, deep.State.GetPlot(Centre).Hp, 1e-9, "never lowered");
        }

        [Test]
        public void ARebirth_ReturnsTheFieldToTheSurface()
        {
            var sim = NewSim();
            sim.DebugSetLayer(Centre, 7);
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(6000);
            Assert.IsTrue(sim.Retire());
            sim.StartNewGeneration();
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(0, p.Layer);
                Assert.AreEqual(PlotState.Hard, p.State);
                Assert.AreEqual(p.MaxHp, p.Hp, 1e-9);
            }
        }

        [Test]
        public void EarlyThaw_AndHeadStart_OpenSpringWithCracksAlreadyMade()
        {
            var thaw = NewSim();
            thaw.DebugSetLevel("early_thaw", 1);
            thaw.DebugSkipToWinter();
            thaw.StartNextYear();
            foreach (var p in thaw.State.Plots) Assert.AreEqual(p.MaxHp * (1 - thaw.Config.EarlyThawShare), p.Hp, 1e-9);

            var head = NewSim(c => c.Crops[0].Grow = 1000f);
            head.DebugSetLevel("spring_head_start", 1);
            head.DebugBreak(Centre);
            head.DebugSkipToWinter();
            head.StartNextYear();
            Assert.AreEqual(head.Config.SpringHeadStartProgress, head.State.GetPlot(Centre).Progress, 1e-6f);
            Assert.AreEqual(head.State.GetPlot(0, 0).MaxHp * head.Config.SpringHeadStartProgress, head.State.GetPlot(0, 0).Hp, 1e-9);
        }

        // ---------------------------------------------------------------- the ground's specials and the hoe's nodes

        [Test]
        public void Hardpan_HasThreeTimesTheHp_AndPaysEightTimes_AChestPaysItsMultiple()
        {
            var gold = NewSim(c => c.HardpanChance = 1);
            var plot = gold.State.GetPlot(Centre);
            Assert.IsTrue(plot.Hardpan);
            Assert.IsFalse(plot.Chest);
            Assert.AreEqual(gold.Config.BaseHp * gold.Config.HardpanHpMult, plot.MaxHp, 1e-9);
            BreakEvent last = default;
            gold.PlotBroken += e => last = e;
            gold.DebugBreak(Centre);
            Assert.IsTrue(last.Hardpan);
            Assert.AreEqual(gold.Config.BreakCoins * gold.Config.HardpanCoinsMult, last.Coins, 1e-9);

            var chest = NewSim(c => c.ChestChance = 1);
            var cp = chest.State.GetPlot(Centre);
            Assert.IsTrue(cp.Chest);
            Assert.AreEqual(chest.Config.BaseHp, cp.MaxHp, 1e-9, "a chest does not harden the ground");
            chest.PlotBroken += e => last = e;
            chest.DebugBreak(Centre);
            Assert.IsTrue(last.Chest);
            Assert.AreEqual(chest.Config.BreakCoins * chest.Config.ChestMult, last.Coins, 1e-9);
        }

        [Test]
        public void DepthValue_PaysMoreForACropDugFromDeeper()
        {
            var sim = NewSim(c => c.DepthValue = 0.5);
            sim.DebugSetLayer(Centre, 4);
            sim.DebugBreak(Centre);
            sim.DebugForceRipeAll();
            double coins = sim.State.Coins;
            sim.ReapOne(Centre);
            Assert.AreEqual(coins + 1 * (1 + 0.5 * 4), sim.State.Coins, 1e-9);
        }

        [Test]
        public void HoeDamage_BreakBonus_StrikeSpeed_AndSplash_ApplyAsTheirNodesSay()
        {
            var cfg = TestConfig.Classic();
            var s = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["hoe_damage"] = 2, ["break_bonus"] = 2, ["strike_speed"] = 2, ["splash"] = 1 });
            Assert.AreEqual(cfg.BaseStrikeDamage + 2, s.StrikeDamage, 1e-9);
            Assert.AreEqual(1.2, s.BreakBonusMult, 1e-9);
            Assert.AreEqual(cfg.BaseStrikeCooldown - 0.08f, s.StrikeCooldown, 1e-6f);
            Assert.AreEqual(0.25f, s.SplashShare, 1e-6f);
            var fast = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["strike_speed"] = 99 });
            Assert.AreEqual(cfg.MinStrikeCooldown, fast.StrikeCooldown, 1e-6f, "never below the floor");

            var sim = NewSim();
            sim.DebugSetLevel("splash", 1);
            var struck = new List<StrikeEvent>();
            sim.Struck += struck.Add;
            WaitForBeat(sim, false);
            sim.Strike(Centre);
            Assert.AreEqual(5, struck.Count, "the plot and its four side neighbours");
            Assert.AreEqual(10 - 3, sim.State.GetPlot(Centre).Hp, 1e-9);
            Assert.AreEqual(10 - 0.75, sim.State.GetPlot(0, 1).Hp, 1e-9);
            Assert.AreEqual(10, sim.State.GetPlot(0, 0).Hp, 1e-9, "diagonals are untouched");
            sim.DebugSetLevel("break_bonus", 4); // its max
            sim.DebugBreak(Centre);
            Assert.AreEqual(sim.Config.BreakCoins * 1.4, sim.State.Coins, 1e-9);
        }

        [Test]
        public void SoftGround_LowersTheHp_AndKeepsTheCracksAlreadyMade()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(Centre);
            WaitForBeat(sim, false);
            sim.Strike(Centre); // 7/10
            sim.DebugSetLevel("soft_ground", 5);
            var node = AlmanacData.Get("soft_ground");
            double max = sim.Config.BaseHp * (1 - node.ValuePerLevel * 5);
            Assert.AreEqual(max, plot.MaxHp, 1e-5);
            Assert.AreEqual(max * 0.7, plot.Hp, 1e-5, "the same share of cracks");
            sim.DebugBreak(Centre);
            sim.DebugForceRipeAll();
            sim.ReapOne(Centre);
            Assert.AreEqual(sim.Config.BaseHp * sim.Config.HpGrowth * (1 - node.ValuePerLevel * 5), plot.MaxHp, 1e-5, "the next layer is softer too");
        }

        [Test]
        public void GrowthNodes_SpeedEveryCrop_AndTheBeehiveOnlyItsColumns()
        {
            var cfg = TestConfig.Classic();
            var s = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["growth"] = 2, ["soil_quality"] = 2 });
            Assert.AreEqual(1.3f, s.GrowthMult, 1e-6f);
            Assert.AreEqual(1.5f, s.SoilMultiplier, 1e-6f);

            var sim = NewSim();
            sim.DebugSetLevel("growth", 2);
            sim.DebugSetLevel("beehive", 1);
            sim.DebugBreak(new GridPos(0, 0));
            sim.DebugBreak(new GridPos(2, 0));
            Run(sim, 1f);
            float plain = 1f * 1.3f / sim.Config.Crops[0].Grow;
            Assert.That(sim.State.GetPlot(0, 0).Progress, Is.EqualTo(plain).Within(0.02f));
            Assert.That(sim.State.GetPlot(2, 0).Progress, Is.EqualTo(plain * sim.Config.BeeGrowth).Within(0.02f), "by the sunflowers");
        }

        // ---------------------------------------------------------------- helpers (GDD §2v3.9)

        [Test]
        public void ADigger_StrikesHardGround_Slowly_AndNeverCrits()
        {
            var sim = NewSim();
            sim.DebugSetLevel("apprentice_count", 1);
            Assert.IsTrue(sim.SetApprenticeRole(0, ApprenticeRole.Digger));
            var struck = new List<StrikeEvent>();
            sim.Struck += struck.Add;
            Run(sim, 4f);
            Assert.That(struck.Count, Is.GreaterThan(0));
            foreach (var e in struck)
            {
                Assert.AreEqual(0, e.ApprenticeIndex);
                Assert.IsFalse(e.Crit);
                Assert.AreEqual(sim.Config.BaseStrikeDamage * sim.Config.ApprenticeDigShare, e.Damage, 1e-9);
            }
            Assert.AreEqual(sim.Config.BaseStaminaMax, sim.State.Stamina, 1e-4f, "the digger has its own arms");
            var dig = StatResolver.Resolve(TestConfig.Classic(), new Dictionary<string, int> { ["apprentice_dig"] = 2 });
            Assert.AreEqual(sim.Config.ApprenticeDigShare + 0.5, dig.ApprenticeDigShare, 1e-9);
        }

        [Test]
        public void AWaterer_GivesGrowingCropsABurst_AndAPickerReapsRipeOnes()
        {
            var sim = NewSim(c => c.Crops[0].Grow = 1000f);
            sim.DebugSetLevel("apprentice_count", 1);
            Assert.IsTrue(sim.SetApprenticeRole(0, ApprenticeRole.Waterer));
            sim.DebugBreak(Centre);
            int watered = 0;
            sim.PlotWatered += _ => watered++;
            Run(sim, 4f);
            Assert.That(watered, Is.GreaterThan(0));
            Assert.That(sim.State.GetPlot(Centre).Progress, Is.GreaterThanOrEqualTo(sim.Config.ApprenticeWaterBoost));

            var picker = NewSim();
            picker.DebugSetLevel("apprentice_count", 1);
            Assert.AreEqual(ApprenticeRole.Picker, picker.State.Apprentices[0].Role);
            picker.DebugForceRipeAll();
            var harvests = new List<HarvestEvent>();
            picker.Harvested += harvests.Add;
            Run(picker, 4f);
            Assert.That(harvests.Count, Is.GreaterThan(0));
            Assert.AreEqual(HarvestSource.Apprentice, harvests[0].Source);
            Assert.AreEqual(1 * picker.Config.ApprenticeYieldByLevel[0], harvests[0].Coins, 1e-9);
        }

        // ---------------------------------------------------------------- offline (GDD §2v3.10)

        [Test]
        public void Offline_GrowsTheCrops_RefillsTheDepot_AndStrikesNothing()
        {
            var sim = NewSim();
            sim.DebugBreak(Centre);
            sim.DebugSetStamina(10f);
            WaitForBeat(sim, false);
            sim.Strike(new GridPos(0, 0));
            double hp = sim.State.GetPlot(0, 0).Hp;
            var r = sim.SimulateOffline(600);
            Assert.That(r.SecondsSimulated, Is.EqualTo(600).Within(1));
            Assert.AreEqual(PlotState.Ripe, sim.State.GetPlot(Centre).State, "it grew");
            Assert.AreEqual(hp, sim.State.GetPlot(0, 0).Hp, 1e-9, "no strikes while away");
            Assert.AreEqual(sim.Config.BaseStaminaMax, sim.State.Stamina, 1e-4f, "the depot refilled");
            Assert.AreEqual(0, r.CoinsEarned, "nothing reaps by itself");
        }

        // ---------------------------------------------------------------- determinism

        [Test]
        public void Determinism_SameSeedSameInputs_SameResult()
        {
            double Play(int seed)
            {
                var sim = new FarmSim(new FarmConfig(), seed);
                var path = new List<GridPos>();
                for (int i = 0; i < 6000; i++)
                {
                    var p = sim.State.Plots[i % 9];
                    if (p.IsHard) sim.Strike(p.Pos);
                    if (i % 50 == 0)
                    {
                        path.Clear();
                        foreach (var q in sim.State.Plots) path.Add(q.Pos);
                        sim.Reap(path);
                    }
                    sim.SetWatering(i % 7 == 0 ? (GridPos?)null : sim.State.Plots[(i / 3) % 9].Pos);
                    sim.Tick(0.02f);
                    if (sim.State.Phase == Phase.Winter) sim.StartNextYear();
                }
                return sim.State.Coins * 1000 + sim.State.Generation.Breaks;
            }
            Assert.AreEqual(Play(11), Play(11));
            Assert.AreNotEqual(Play(11), Play(12));
        }
    }
}
