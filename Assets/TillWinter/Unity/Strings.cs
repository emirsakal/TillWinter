using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The one string table (EN now, TR in S9 with the same shape). Loaded from the `en.json`
    /// TextAsset assigned on the Bootstrap; descriptions are templates filled from <see cref="NodeText"/>.
    /// Unknown keys return the key itself so a missing entry is visible, never a crash.
    /// </summary>
    public static class Strings
    {
        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>();
        public static bool Loaded { get; private set; }

        public static void Load(TextAsset json)
        {
            Table.Clear();
            Loaded = false;
            if (json == null || string.IsNullOrWhiteSpace(json.text))
            {
                Debug.LogWarning("[TillWinter] Strings: no table assigned; keys will show instead of text");
                return;
            }
            try
            {
                Parse(json.text);
                Loaded = Table.Count > 0;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TillWinter] Strings: could not parse table: " + e.Message);
            }
        }

        public static string Get(string key) => key != null && Table.TryGetValue(key, out var s) ? s : key ?? "";

        public static string Format(string key, IReadOnlyDictionary<string, string> values) => NodeText.Fill(Get(key), values);

        public static string Format(string key, params (string name, object value)[] args)
        {
            var d = new Dictionary<string, string>();
            foreach (var (name, value) in args) d[name] = value?.ToString() ?? "";
            return NodeText.Fill(Get(key), d);
        }

        public static string Name(SkillNode node) => Get(node.NameKey);
        public static string Branch(Core.Branch b) => Get("branch." + b);
        public static string Crop(CropDef crop) => Get(crop.Key);

        /// <summary>Description with {cur}/{next}/{level}/{max}/{cost} filled from the sim.</summary>
        public static string Description(FarmSim sim, SkillNode node) => NodeText.Fill(Get(node.DescKey), NodeText.Values(sim, node.Id));

        public static string[] Names(IReadOnlyList<SkillNode> nodes)
        {
            var r = new string[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) r[i] = Name(nodes[i]);
            return r;
        }

        /// <summary>Placeholder glyph until icons exist: first three letters of the id's last word.</summary>
        public static string Glyph(SkillNode node)
        {
            string id = node.Id.StartsWith("h_") ? node.Id.Substring(2) : node.Id;
            int cut = id.LastIndexOf('_');
            string word = cut >= 0 ? id.Substring(cut + 1) : id;
            return (word.Length > 3 ? word.Substring(0, 3) : word).ToUpperInvariant();
        }

        // Flat {"key":"value"} object parser (strings only), with \n and \" escapes.
        private static void Parse(string s)
        {
            int i = 0;
            Skip(s, ref i);
            if (s[i] != '{') throw new System.FormatException("expected {");
            i++;
            while (true)
            {
                Skip(s, ref i);
                if (s[i] == '}') return;
                string key = Str(s, ref i);
                Skip(s, ref i);
                if (s[i] != ':') throw new System.FormatException("expected : at " + i);
                i++;
                Skip(s, ref i);
                string value = Str(s, ref i);
                Table[key] = value;
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') return;
                throw new System.FormatException("expected , or } at " + i);
            }
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new System.FormatException("expected string at " + i);
            i++;
            var sb = new System.Text.StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    sb.Append(s[i] == 'n' ? '\n' : s[i] == 't' ? '\t' : s[i]);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
    }
}
