using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// The recurring ideas of the game as icons (Kenney Game Icons, the same atlas the skill tree uses), so lists of
    /// numbers read as rows instead of a wall of text. Names are semantic; the atlas key is an implementation detail.
    /// </summary>
    public static class UiIcons
    {
        public const string Generation = "singleplayer";
        public const string Year = "scrollHorizontal";
        public const string Coin = "star";
        public const string Harvest = "basket";
        public const string Ring = "target";
        public const string Apprentice = "multiplayer";
        public const string Tractor = "gear";
        public const string Crow = "warning";
        public const string Golden = "trophy";
        public const string Combo = "leaderboardsSimple";
        public const string Time = "fastForward";

        /// <summary>An icon image sized to <paramref name="size"/>, anchored left-middle at <paramref name="x"/>.</summary>
        public static Image Row(Transform parent, string icon, Color color, float x, float size)
        {
            // Coins are a gold disc everywhere else in the game (the HUD counter, the coin flight); the atlas has no
            // coin, and a star here made the same idea read as two different things.
            var img = icon == Coin
                ? UiKit.CircleImage(parent, "Coin", color, Vector2.zero, size)
                : NodeIcons.Image(parent, icon, color);
            UiKit.Box(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(size, size));
            return img;
        }
    }
}
