namespace TillWinter.Core
{
    /// <summary>
    /// A farm as one line of text: the save JSON in base64 behind a version tag and a checksum, so a player can carry
    /// a farm to another phone by copying a string. The checksum is the point — a half-copied code is refused instead
    /// of loading as a corrupt farm. Nothing here reads or writes files; the presentation layer does that.
    /// </summary>
    public static class SaveTransfer
    {
        /// <summary>Bumped only if the envelope changes; the save's own schema version travels inside the JSON.</summary>
        public const string Prefix = "TW1";

        public static string Encode(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return Prefix + "." + Checksum(bytes).ToString("x8") + "." + System.Convert.ToBase64String(bytes);
        }

        /// <summary>True only for a code that is whole: right envelope, valid base64, matching checksum.</summary>
        public static bool TryDecode(string code, out string json)
        {
            json = null;
            if (string.IsNullOrEmpty(code)) return false;
            var parts = Clean(code).Split('.');
            if (parts.Length != 3 || parts[0] != Prefix || parts[1].Length != 8) return false;
            byte[] bytes;
            try { bytes = System.Convert.FromBase64String(parts[2]); }
            catch (System.Exception) { return false; }
            if (bytes.Length == 0 || Checksum(bytes).ToString("x8") != parts[1]) return false;
            json = System.Text.Encoding.UTF8.GetString(bytes);
            return true;
        }

        /// <summary>A pasted code arrives with whatever line breaks and spaces the messenger app put in it.</summary>
        private static string Clean(string code)
        {
            var sb = new System.Text.StringBuilder(code.Length);
            foreach (var c in code)
                if (c > ' ') sb.Append(c);
            return sb.ToString();
        }

        /// <summary>FNV-1a: short, stable across platforms, and enough to catch a truncated or edited paste.</summary>
        private static uint Checksum(byte[] bytes)
        {
            uint hash = 2166136261;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 16777619;
            }
            return hash;
        }
    }
}
