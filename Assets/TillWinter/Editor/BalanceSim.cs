using System.IO;
using TillWinter.Core;
using TillWinter.Core.Balance;
using UnityEditor;
using UnityEngine;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Headless balance run (balance-sim.bat): plays N generations with the AutoPlayer and writes
    /// TestResults/balance.csv + balance.txt. Also reachable from the menu for a quick look.
    /// </summary>
    public static class BalanceSim
    {
        public static void Run()
        {
            int seed = 7, generations = 3;
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

        [MenuItem("Till Winter/Balance sim (3 generations, seed 7)")]
        public static void RunFromMenu()
        {
            Debug.Log("[Balance]\n" + RunToFiles(7, 3));
        }

        /// <summary>Runs the AutoPlayer and writes the CSV/table; returns the table text.</summary>
        public static string RunToFiles(int seed, int generations)
        {
            var sim = new FarmSim(new FarmConfig(), seed);
            var player = new AutoPlayer(sim, seed);
            player.Run(generations);
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestResults");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "balance.csv"), player.ToCsv());
            string table = "seed " + seed + ", " + generations + " generations, dt " + player.Dt + "\n" + player.ToTable();
            File.WriteAllText(Path.Combine(dir, "balance.txt"), table);
            return table;
        }
    }
}
