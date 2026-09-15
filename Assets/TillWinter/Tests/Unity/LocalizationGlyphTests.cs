using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Tests;
using TillWinter.Unity;

namespace TillWinter.Tests.Unity
{
    /// <summary>Every character used by a string table renders with the game font (no tofu boxes in Turkish).</summary>
    public class LocalizationGlyphTests
    {
        [TestCase("en")]
        [TestCase("tr")]
        public void EveryCharacter_ExistsInTheFontAtlas(string lang)
        {
            var font = UiKit.Font;
            Assert.IsNotNull(font, "NunitoSDF missing from Resources");
            var chars = new HashSet<char>();
            foreach (var kv in StringsTests.LoadTable(lang))
                foreach (char c in kv.Value)
                    if (!char.IsWhiteSpace(c)) chars.Add(c);
            var text = new string(new List<char>(chars).ToArray());
            bool ok = font.HasCharacters(text, out uint[] missing, false, false);
            var names = new System.Text.StringBuilder();
            if (missing != null) foreach (uint u in missing) names.Append((char)u).Append(" U+").Append(u.ToString("X4")).Append(' ');
            Assert.IsTrue(ok, lang + ": missing glyphs in NunitoSDF: " + names);
        }
    }
}
