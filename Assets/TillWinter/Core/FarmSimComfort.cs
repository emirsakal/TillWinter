using System;

namespace TillWinter.Core
{
    /// <summary>GDD §10.5 (v2.5): the first generation's checklist, in the order a new player meets them.</summary>
    public enum ChecklistStep
    {
        Water = 0,
        Grow = 1,
        Harvest5 = 2,
        Combo3 = 3,
        BuyNode = 4,
    }

    /// <summary>GDD §10.7 (v2.5): what the apprentices do while the game is closed.</summary>
    public enum AwayPlan
    {
        /// <summary>Each keeps the role the player gave it.</summary>
        AsTheyAre = 0,
        /// <summary>Everyone harvests.</summary>
        AllHarvest = 1,
        /// <summary>Every other apprentice waters, the rest harvest.</summary>
        Balanced = 2,
    }

    /// <summary>M.9, feel and accessibility (GDD §10.5–§10.7 v2.5): the early checklist and the away plan.</summary>
    public sealed partial class FarmSim
    {
        public const int ChecklistSteps = 5;

        /// <summary>A checklist step was done (it stays done).</summary>
        public event Action<ChecklistStep> ChecklistStepDone;

        /// <summary>The checklist shows during the family's first generation until every step is done.</summary>
        public bool ChecklistActive
        {
            get
            {
                var s = State;
                return !s.IsDaily && s.NgPlus == 0 && s.Generation.Generation == 1 && s.ChecklistBits != (1 << ChecklistSteps) - 1;
            }
        }

        public bool ChecklistDone(ChecklistStep step) => (State.ChecklistBits & (1 << (int)step)) != 0;

        private void UpdateChecklist()
        {
            if (!ChecklistActive) return;
            var g = State.Generation;
            bool wet = g.Harvests > 0, ripe = g.Harvests > 0;
            if (!wet || !ripe)
                foreach (var p in State.PlotArray)
                {
                    if (p.State != PlotState.Dry) wet = true;
                    if (p.IsRipe) ripe = true;
                }
            if (wet) MarkStep(ChecklistStep.Water);
            if (ripe) MarkStep(ChecklistStep.Grow);
            if (g.Harvests >= 5) MarkStep(ChecklistStep.Harvest5);
            if (g.BestCombo >= 3) MarkStep(ChecklistStep.Combo3);
            if (Almanac.Levels.Count > 0) MarkStep(ChecklistStep.BuyNode);
        }

        private void MarkStep(ChecklistStep step)
        {
            int bit = 1 << (int)step;
            if ((State.ChecklistBits & bit) != 0) return;
            State.ChecklistBits |= bit;
            ChecklistStepDone?.Invoke(step);
        }

        /// <summary>Opens the checklist again with nothing ticked (the UI tour).</summary>
        public void DebugResetChecklist() => State.ChecklistBits = 0;

        /// <summary>Chooses what the apprentices do while the game is closed.</summary>
        public void SetAwayPlan(AwayPlan plan) => State.AwayPlan = plan;

        private readonly System.Collections.Generic.List<ApprenticeRole> _rolesBeforeAway = new System.Collections.Generic.List<ApprenticeRole>();

        /// <summary>Before an offline stretch: the plan's roles; <see cref="RestoreRolesAfterAway"/> puts the player's back.</summary>
        private void ApplyAwayPlan()
        {
            _rolesBeforeAway.Clear();
            foreach (var a in State.ApprenticeList) _rolesBeforeAway.Add(a.Role);
            if (State.AwayPlan == AwayPlan.AsTheyAre) return;
            foreach (var a in State.ApprenticeList)
                a.Role = State.AwayPlan == AwayPlan.Balanced && a.Index % 2 == 1 ? ApprenticeRole.Waterer : ApprenticeRole.Harvester;
        }

        private void RestoreRolesAfterAway()
        {
            var list = State.ApprenticeList;
            for (int i = 0; i < list.Count && i < _rolesBeforeAway.Count; i++) list[i].Role = _rolesBeforeAway[i];
        }
    }
}
