using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>
    /// GDD §6.3 (v2.2): the Almanac's suggestion. One weight per node (how much it tends to help, by the balance
    /// measurements); the suggestion is the affordable node with the best weight per coin. The AutoPlayer buys by the
    /// same table, so the marker points where the measured play goes.
    /// </summary>
    public static class AlmanacAdvisor
    {
        /// <summary>A fresh copy of the weights (missing id = 1).</summary>
        public static Dictionary<string, double> CreateWeights() => new Dictionary<string, double>
        {
            ["irrigation"] = 3, ["sun"] = 3, ["ring_radius"] = 2.5, ["apprentice_count"] = 2.5, ["expand_field"] = 2,
            ["unlock_tomato"] = 2, ["upgrade_plot"] = 1.5, ["soil_quality"] = 1.5, ["ring_water_speed"] = 1.2, ["ring_grow_speed"] = 1.2,
            ["year_length"] = 1.5, ["scarecrow"] = 0.8, ["farm_dog"] = 0.6, ["beehive"] = 0.6, ["hens"] = 0.5, ["barn"] = 0.3, ["tractor"] = 1.2, ["greenhouse"] = 0.8, ["crow_bounty"] = 0.4,
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

        private static Dictionary<string, double> _weights;

        /// <summary>The node to suggest this winter, or null when nothing is affordable (a marker on a node you cannot buy helps nobody).</summary>
        public static string Suggest(FarmSim sim)
        {
            if (sim == null || sim.State.Phase != Phase.Winter) return null;
            if (_weights == null) _weights = CreateWeights();
            string best = null;
            double bestScore = 0;
            foreach (var n in sim.Nodes)
            {
                if (!sim.CanBuy(n.Id)) continue;
                double w = _weights.TryGetValue(n.Id, out var v) ? v : 1;
                double score = w / Math.Max(1, sim.CostOf(n.Id));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = n.Id;
                }
            }
            return best;
        }
    }
}
