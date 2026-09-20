using NUnit.Framework;
using TillWinter.Core;
using TillWinter.Unity;

namespace TillWinter.Tests.Unity
{
    /// <summary>
    /// The music bed and the ambience mix (GDD §12): which piece a farm state calls for, and how the looping layers
    /// answer the season, the weather and winter. Both are decisions, so both are tested without playing a sound.
    /// </summary>
    public class AudioBedTests
    {
        private static FarmSim Sim()
        {
            var cfg = new FarmConfig { CrowSpawnChance = 0f };
            return new FarmSim(cfg, 1);
        }

        [Test]
        public void EveryMusicId_HasARow()
        {
            foreach (MusicId id in System.Enum.GetValues(typeof(MusicId)))
            {
                Assert.IsTrue(MusicTable.Rows.ContainsKey(id), id + " row");
                Assert.IsFalse(string.IsNullOrEmpty(MusicTable.Rows[id].Clip), id + " clip name");
            }
            Assert.IsFalse(MusicTable.Rows[MusicId.Ending].Loop, "the ending plays once and hands the floor back");
        }

        [Test]
        public void TheSeasonPicksThePiece_AndWinterAndGoldenTakeOver()
        {
            var sim = Sim();
            Assert.AreEqual(MusicId.Spring, MusicPlayer.ForState(sim.State));
            sim.DebugSetSeason(Season.Summer);
            Assert.AreEqual(MusicId.Summer, MusicPlayer.ForState(sim.State));
            sim.DebugSetSeason(Season.Autumn);
            Assert.AreEqual(MusicId.Autumn, MusicPlayer.ForState(sim.State));
            sim.DebugSkipToWinter();
            Assert.AreEqual(MusicId.Winter, MusicPlayer.ForState(sim.State));
            Assert.AreEqual(MusicId.Title, MusicPlayer.ForState(null), "no farm, no season");
        }

        [Test]
        public void TheAmbienceMix_FollowsSeasonWeatherAndWinter()
        {
            var mix = new float[5];
            var sim = Sim();

            AmbiencePlayer.Mix(sim.State, mix);
            Assert.That(mix[(int)AmbienceLayer.Birds], Is.GreaterThan(0f), "spring has birds");
            Assert.AreEqual(0f, mix[(int)AmbienceLayer.WinterWind]);

            sim.DebugSetSeason(Season.Summer);
            sim.DebugStartWeather(Weather.HeatWave);
            AmbiencePlayer.Mix(sim.State, mix);
            Assert.That(mix[(int)AmbienceLayer.Insects], Is.GreaterThan(0.5f), "a heat wave is loud with insects");

            sim.DebugStartWeather(Weather.Storm);
            AmbiencePlayer.Mix(sim.State, mix);
            Assert.AreEqual(1f, mix[(int)AmbienceLayer.Rain], "rain takes over the mix");
            Assert.AreEqual(0f, mix[(int)AmbienceLayer.Birds], "birds sit a storm out");

            sim.DebugSkipToWinter();
            AmbiencePlayer.Mix(sim.State, mix);
            Assert.That(mix[(int)AmbienceLayer.WinterWind], Is.GreaterThan(0.5f));
            Assert.AreEqual(0f, mix[(int)AmbienceLayer.Rain], "winter is not the year's weather");
        }
    }
}
