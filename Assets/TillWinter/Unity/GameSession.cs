namespace TillWinter.Unity
{
    /// <summary>
    /// What the title screen tells the farm scene, and what views without a game reference need (GDD §8.3–§8.4 v2.4):
    /// the daily farm's day (0 = the family farm) and the New Game+ round.
    /// </summary>
    public static class GameSession
    {
        public static int Daily;
        public static int NgPlus;

        /// <summary>Today as yyyymmdd, on the device's own calendar.</summary>
        public static int Today()
        {
            var d = System.DateTime.Now;
            return d.Year * 10000 + d.Month * 100 + d.Day;
        }
    }
}
