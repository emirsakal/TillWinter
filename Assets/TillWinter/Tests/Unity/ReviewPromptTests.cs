using NUnit.Framework;
using TillWinter.Core;
using TillWinter.Unity;

namespace TillWinter.Tests.Unity
{
    /// <summary>The rating sheet: once, from the third generation on, never on the daily farm, remembered in settings.</summary>
    public class ReviewPromptTests
    {
        [Test]
        public void Asks_OnceFromTheThirdGeneration_AndRemembersInSettings()
        {
            SettingsStore.Override(new SettingsData());
            var sim = new FarmSim(new FarmConfig(), 1);
            Assert.IsFalse(ReviewPrompt.MaybeAsk(sim.State), "generation 1: too early");
            sim.DebugSetGeneration(ReviewPrompt.AskAtGeneration - 1);
            Assert.IsFalse(ReviewPrompt.MaybeAsk(sim.State), "one short");
            sim.DebugSetGeneration(ReviewPrompt.AskAtGeneration);
            Assert.IsTrue(ReviewPrompt.MaybeAsk(sim.State));
            Assert.IsTrue(SettingsStore.Current.ReviewAsked);
            Assert.IsFalse(ReviewPrompt.MaybeAsk(sim.State), "never twice");
            SettingsStore.Override(new SettingsData()); // leave the editor's settings alone
        }
    }
}
