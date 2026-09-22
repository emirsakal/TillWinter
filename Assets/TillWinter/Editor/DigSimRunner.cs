using System.IO;
using System.Text;
using TillWinter.Core.Dig;
using UnityEditor;
using UnityEngine;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Core loop v3 prototype tables (GDD §2v3.13): dumb bot, smart bot, clumsy smart bot, for N years, written to
    /// TestResults/dig.txt by dig-sim.bat. Same shape as BalanceSim so the numbers are read the same way.
    /// </summary>
    public static class DigSimRunner
    {
        public static void Run()
        {
            int seed = 1, years = 8;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-seed") int.TryParse(args[i + 1], out seed);
                if (args[i] == "-years") int.TryParse(args[i + 1], out years);
            }
            string table = RunToFile(seed, years);
            Debug.Log("[Dig]\n" + table);
            EditorApplication.Exit(0);
        }

        [MenuItem("Till Winter/Core v3 dig sim (seed 1, 8 years)")]
        public static void RunFromMenu() => Debug.Log("[Dig]\n" + RunToFile(1, 8));

        public static string RunToFile(int seed, int years)
        {
            var sb = new StringBuilder();
            sb.AppendLine("seed " + seed + ", " + years + " years, dt 0.05");
            foreach (var (name, smart, skill) in new[] { ("dumb (random tile, random timing)", false, 0f), ("smart, on-beat 0.8", true, 0.8f), ("smart, on-beat 0.3", true, 0.3f) })
            {
                var bot = new DigBot(new DigSim(new DigConfig(), seed), smart, skill, seed);
                bot.RunYears(years);
                sb.AppendLine();
                sb.AppendLine("== " + name + "   total " + bot.TotalCoins.ToString("0") + " coins, crit rate "
                    + (bot.TotalStrikes > 0 ? (100.0 * bot.TotalCrits / bot.TotalStrikes).ToString("0") : "0") + "%");
                sb.Append(bot.ToTable());
            }
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestResults");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "dig.txt"), sb.ToString());
            return sb.ToString();
        }
    }
}
