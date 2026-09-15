using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// One type scale for every screen (canvas units at the 1080x2340 reference). Sizes were scattered as literals
    /// per call site, so screens disagreed; every <see cref="UiKit.Label"/> now goes through <see cref="Size"/>,
    /// which also applies <see cref="Scale"/> (the hook for a large-text setting).
    /// </summary>
    public static class UiType
    {
        public const int Display = 140; // the game's name on the title scene
        public const int Hero = 84;     // generation card title, ending title
        public const int Big = 64;      // big numbers (away coins, seeds counting up)
        public const int Title = 56;    // screen and sheet titles
        public const int Heading = 44;  // primary buttons, node names, section heads
        public const int Body = 34;     // normal text and secondary buttons
        public const int Label = 28;    // row labels, chips, small buttons
        public const int Caption = 24;  // hints, costs, footnotes

        /// <summary>Global text multiplier (accessibility). 1 = the scale above.</summary>
        public static float Scale { get; set; } = 1f;

        public static int Size(int size) => Mathf.Max(8, Mathf.RoundToInt(size * Scale));
    }
}
