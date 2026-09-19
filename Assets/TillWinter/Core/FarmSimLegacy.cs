using System;

namespace TillWinter.Core
{
    /// <summary>
    /// M.7, progression and rebirth (GDD §7.4–§7.6 v2.3): the heirs offered at each rebirth, challenge generations,
    /// and achievements that leave heirlooms.
    /// </summary>
    public sealed partial class FarmSim
    {
        /// <summary>An achievement was earned; its heirloom now applies (GDD §7.6 v2.3).</summary>
        public event Action<AchievementId> AchievementUnlocked;

        /// <summary>Picks the heir for the coming generation from the three offered (Heritage phase only).</summary>
        public bool ChooseHeir(int index)
        {
            var g = State.Generation;
            if (State.Phase != Phase.Heritage || index < 0 || index >= g.HeirOffer.Length) return false;
            var trait = g.HeirOffer[index];
            if (trait == HeirTrait.None) return false;
            g.Trait = trait;
            ResolveStats();
            return true;
        }

        /// <summary>Sets the coming generation's challenge (Heritage phase only). It pays more seeds when that generation retires.</summary>
        public bool SetChallenge(ChallengeKind challenge)
        {
            if (State.Phase != Phase.Heritage) return false;
            State.Generation.Challenge = challenge;
            ResolveStats();
            return true;
        }

        /// <summary>Seeds at retirement, with a challenge's bonus.</summary>
        private double ChallengeSeedMult => State.Generation.Challenge != ChallengeKind.None ? Config.ChallengeSeedBonus : 1;

        /// <summary>At a rebirth: three different heirs drawn with the sim RNG; the first is chosen until the player picks.</summary>
        private void OfferHeirs()
        {
            var g = State.Generation;
            _scratchTraits.Clear();
            for (int i = 1; i <= Legacy.TraitCount; i++) _scratchTraits.Add((HeirTrait)i);
            for (int i = 0; i < g.HeirOffer.Length; i++)
            {
                int k = Math.Min(_scratchTraits.Count - 1, (int)(_rng.NextDouble() * _scratchTraits.Count));
                g.HeirOffer[i] = _scratchTraits[k];
                _scratchTraits.RemoveAt(k);
            }
            g.Trait = g.HeirOffer[0];
            g.Challenge = ChallengeKind.None;
        }

        private readonly System.Collections.Generic.List<HeirTrait> _scratchTraits = new System.Collections.Generic.List<HeirTrait>();

        // ------------------------------------------------------------------ achievements

        private bool Unlock(AchievementId id)
        {
            var g = State.Generation;
            int bit = 1 << (int)id;
            if ((g.Achievements & bit) != 0) return false;
            g.Achievements |= bit;
            ResolveStats();
            AchievementUnlocked?.Invoke(id);
            return true;
        }

        /// <summary>Cheap enough for every tick: a handful of comparisons, and nothing once everything is earned.</summary>
        private void CheckAchievements()
        {
            var g = State.Generation;
            if (g.Achievements == (1 << Legacy.AchievementCount) - 1) return;
            if (g.Harvests >= 1) Unlock(AchievementId.FirstHarvest);
            if (g.BestCombo >= 25) Unlock(AchievementId.Combo25);
            if (g.CrowsScared >= 50) Unlock(AchievementId.Crows50);
            if (g.GoldenHarvests >= 10) Unlock(AchievementId.Golden10);
            if (State.LastGrade >= 3) Unlock(AchievementId.ThreeStars);
            if (g.Harvests >= 1000) Unlock(AchievementId.Harvests1000);
            if (g.Generation >= 2) Unlock(AchievementId.SecondGeneration);
            if (State.Stats.MaxTierUnlocked >= Config.MaxTier) Unlock(AchievementId.GoldenWheat);
            if (g.GoalsMet >= 5) Unlock(AchievementId.Goals5);
            if (g.PestsStopped >= 10) Unlock(AchievementId.Pests10);
        }

        /// <summary>Earns an achievement now (tests and the UI tour).</summary>
        public void DebugUnlockAchievement(AchievementId id) => Unlock(id);

        /// <summary>Marks the ending as seen, as the Golden Year does (tests and the UI tour).</summary>
        public void DebugMarkEndingSeen() => State.EndingSeen = true;
    }
}
