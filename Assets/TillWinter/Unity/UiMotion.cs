using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// One motion language for the UI. Every view used to pick its own duration and curve; these are the three
    /// speeds and the two easings the whole interface uses. Times are seconds of unscaled time (menus run paused).
    /// </summary>
    public static class UiMotion
    {
        public const float Fast = 0.12f;   // press feedback, toggles
        public const float Normal = 0.22f; // sheets, cards, panels
        public const float Slow = 0.4f;    // screen fades, reveals

        /// <summary>Decelerating: use for anything entering the screen.</summary>
        public static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

        /// <summary>Soft both ends: use for anything that also leaves.</summary>
        public static float EaseInOut(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        /// <summary>Frame-rate independent damping toward a target at one of the three speeds.</summary>
        public static float Damp(float current, float target, float seconds, float dt) =>
            Prims.Damp(current, target, 1f / Mathf.Max(0.0001f, seconds), dt);
    }
}
