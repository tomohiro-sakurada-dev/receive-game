using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Receive.Core
{
    /// <summary>記録の保存に使う最小の JSON。外部の部品に頼らず、Unity でも通常の .NET でも同じに動く。</summary>
    public static class MiniJson
    {
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is int || v is long) { sb.Append(Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is double || v is float) { sb.Append(Convert.ToDouble(v).ToString("R", CultureInfo.InvariantCulture)); return; }
            var dict = v as IDictionary<string, object>;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key); sb.Append(':'); WriteValue(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            var list = v as System.Collections.IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteValue(sb, item);
                }
                sb.Append(']');
                return;
            }
            WriteString(sb, v.ToString());
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        /// <summary>読み取り。壊れた入力には例外を投げる。</summary>
        public static object Parse(string json)
        {
            int i = 0;
            object v = ParseValue(json, ref i);
            SkipWs(json, ref i);
            if (i != json.Length) throw new FormatException("JSON の末尾に余分な文字がある");
            return v;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON が途中で終わっている");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                SkipWs(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs(s, ref i);
                    string k = ParseString(s, ref i);
                    SkipWs(s, ref i);
                    if (s[i] != ':') throw new FormatException("':' がない");
                    i++;
                    d[k] = ParseValue(s, ref i);
                    SkipWs(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("',' か '}' がない");
                }
            }
            if (c == '[')
            {
                i++;
                var l = new List<object>();
                SkipWs(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(ParseValue(s, ref i));
                    SkipWs(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("',' か ']' がない");
                }
            }
            if (c == '"') return ParseString(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException("読めない値");
            string num = s.Substring(start, i - start);
            long n;
            if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            return double.Parse(num, CultureInfo.InvariantCulture);
        }

        private static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("文字列の始まりがない");
            i++;
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("文字列が閉じていない");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
        }
    }
}
