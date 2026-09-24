using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace TillWinter.Tests
{
    /// <summary>
    /// Minimal JSON reader/writer for tests (Core has no JSON dependency; Unity uses JsonUtility at runtime).
    /// Supports objects, arrays, numbers, strings, bools, null; maps onto public fields by name.
    /// </summary>
    public static class MiniJson
    {
        public static T To<T>(string json) where T : new() => (T)Map(Parse(json), typeof(T));

        /// <summary>Top-level object as a dictionary (values: string, double, bool, List, Dictionary, null).</summary>
        public static Dictionary<string, object> ParseObject(string json) => (Dictionary<string, object>)Parse(json);

        public static string From(object obj)
        {
            var sb = new StringBuilder();
            Write(obj, sb);
            return sb.ToString();
        }

        // ---- writer
        private static void Write(object v, StringBuilder sb)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case int or long or uint or short or byte: sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); break;
                case Array arr:
                    sb.Append('[');
                    for (int i = 0; i < arr.Length; i++) { if (i > 0) sb.Append(','); Write(arr.GetValue(i), sb); }
                    sb.Append(']');
                    break;
                default:
                    sb.Append('{');
                    bool first = true;
                    foreach (var f in v.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append('"').Append(f.Name).Append("\":");
                        Write(f.GetValue(v), sb);
                    }
                    sb.Append('}');
                    break;
            }
        }

        // ---- mapper
        private static object Map(object node, Type t)
        {
            if (node == null) return t.IsValueType ? Activator.CreateInstance(t) : null;
            if (t == typeof(string)) return node.ToString();
            if (t == typeof(bool)) return Convert.ToBoolean(node, CultureInfo.InvariantCulture);
            if (t == typeof(int)) return Convert.ToInt32(node, CultureInfo.InvariantCulture);
            if (t == typeof(long)) return Convert.ToInt64(node, CultureInfo.InvariantCulture);
            if (t == typeof(uint)) return Convert.ToUInt32(node, CultureInfo.InvariantCulture);
            if (t == typeof(float)) return Convert.ToSingle(node, CultureInfo.InvariantCulture);
            if (t == typeof(double)) return Convert.ToDouble(node, CultureInfo.InvariantCulture);
            if (t.IsArray)
            {
                var list = (List<object>)node;
                var arr = Array.CreateInstance(t.GetElementType(), list.Count);
                for (int i = 0; i < list.Count; i++) arr.SetValue(Map(list[i], t.GetElementType()), i);
                return arr;
            }
            var obj = Activator.CreateInstance(t);
            var dict = (Dictionary<string, object>)node;
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (dict.TryGetValue(f.Name, out var v)) f.SetValue(obj, Map(v, f.FieldType));
            return obj;
        }

        // ---- parser
        private static object Parse(string s)
        {
            int i = 0;
            var v = ParseValue(s, ref i);
            return v;
        }

        private static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object ParseValue(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string key = ParseString(s, ref i);
                    Ws(s, ref i);
                    if (s[i] != ':') throw new FormatException("expected : at " + i);
                    i++;
                    d[key] = ParseValue(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("expected , or } at " + i);
                }
            }
            if (c == '[')
            {
                i++;
                var l = new List<object>();
                Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(ParseValue(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("expected , or ] at " + i);
                }
            }
            if (c == '"') return ParseString(s, ref i);
            if (s.Length - i >= 4 && s.Substring(i, 4) == "true") { i += 4; return true; }
            if (s.Length - i >= 5 && s.Substring(i, 5) == "false") { i += 5; return false; }
            if (s.Length - i >= 4 && s.Substring(i, 4) == "null") { i += 4; return null; }
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("expected string at " + i);
            i++;
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\') { i++; sb.Append(s[i] == 'n' ? '\n' : s[i]); }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
    }
}
