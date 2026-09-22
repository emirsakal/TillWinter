using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The store's own rating sheet, once, at the one moment a player has clearly decided to keep playing: the
    /// second time the farm is passed on. iOS has the API built in (SKStoreReviewController through
    /// UnityEngine.iOS.Device); Android's in-app review needs Google's Play Core package, so there the prompt is
    /// simply not made — the listing's rating button is the way. The flag lives in settings.json, not the save, so
    /// a reset farm does not ask again.
    /// </summary>
    public static class ReviewPrompt
    {
        public const int AskAtGeneration = 3;

        /// <summary>True when the prompt was made this call (for tests and the log).</summary>
        public static bool MaybeAsk(FarmState state)
        {
            if (state == null || state.IsDaily || state.Generation.Generation < AskAtGeneration) return false;
            var settings = SettingsStore.Current;
            if (settings.ReviewAsked) return false;
            settings.ReviewAsked = true;
            SettingsStore.Save();
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.RequestStoreReview();
#endif
            Debug.Log("[TillWinter] Review prompt at generation " + state.Generation.Generation);
            return true;
        }
    }
}
