using System.IO;
using TillWinter.Core;
using TillWinter.Core.Balance;
using UnityEditor;
using UnityEngine;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Headless balance run (balance-sim.bat): the AutoPlayer plays to the Golden Year ending (or N generations)
    /// and writes TestResults/balance.csv + balance.txt (year table and the S9 target summary).
    /// </summary>
    public static class BalanceSim
    {
        public static void Run()
        {
            int seed = 1, generations = 0;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-seed") int.TryParse(args[i + 1], out seed);
                if (args[i] == "-generations") int.TryParse(args[i + 1], out generations);
            }
            string table = RunToFiles(seed, generations);
            Debug.Log("[Balance]\n" + table);
            EditorApplication.Exit(0);
        }

        [MenuItem("Till Winter/Balance sim (to the ending, seed 1)")]
        public static void RunFromMenu()
        {
            Debug.Log("[Balance]\n" + RunToFiles(1, 0));
        }

        /// <summary>Runs the AutoPlayer (generations &lt;= 0: to the ending) and writes the CSV/table; returns the table text.</summary>
        public static string RunToFiles(int seed, int generations)
        {
            var sim = new FarmSim(new FarmConfig(), seed);
            var player = new AutoPlayer(sim, seed) { MaxTicks = 20_000_000 };
            if (generations > 0) player.Run(generations);
            else player.RunToEnding();
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestResults");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "balance.csv"), player.ToCsv());
            string span = generations > 0 ? generations + " generations" : "to the ending";
            string table = "seed " + seed + ", " + span + ", dt " + player.Dt + "\n" + player.ToTable() + "\n" + player.SummaryText();
            File.WriteAllText(Path.Combine(dir, "balance.txt"), table);
            return table;
        }
    }
}
