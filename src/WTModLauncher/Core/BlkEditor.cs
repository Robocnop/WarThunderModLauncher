using System.Text;
using System.Text.RegularExpressions;

namespace WTModLauncher.Core;

/// <summary>
/// Minimal, format-preserving editor for text BLK files (War Thunder config.blk).
/// Only touches the requested key inside a top-level block; everything else is kept byte for byte.
/// </summary>
public static class BlkEditor
{
    /// <summary>Returns the value of <c>key:b=...</c> inside top-level <paramref name="block"/>, or null if absent.</summary>
    public static bool? GetBool(string text, string block, string key)
    {
        var range = FindTopLevelBlock(text, block);
        if (range is null) return null;
        var (bodyStart, bodyEnd) = range.Value;
        var m = KeyRegex(key).Match(text[bodyStart..bodyEnd]);
        if (!m.Success) return null;
        return m.Groups["v"].Value.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || m.Groups["v"].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Sets <c>key:b=yes|no</c> in top-level <paramref name="block"/>, creating key or block if needed.</summary>
    public static string SetBool(string text, string block, string key, bool value)
    {
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var literal = value ? "yes" : "no";
        var range = FindTopLevelBlock(text, block);

        if (range is null)
        {
            var sb = new StringBuilder(text);
            if (sb.Length > 0 && !text.EndsWith('\n')) sb.Append(newline);
            sb.Append(block).Append('{').Append(newline)
              .Append("  ").Append(key).Append(":b=").Append(literal).Append(newline)
              .Append('}').Append(newline);
            return sb.ToString();
        }

        var (bodyStart, bodyEnd) = range.Value;
        var body = text[bodyStart..bodyEnd];
        var m = KeyRegex(key).Match(body);
        if (m.Success)
        {
            var g = m.Groups["v"];
            return text[..(bodyStart + g.Index)] + literal + text[(bodyStart + g.Index + g.Length)..];
        }

        // Insert as first line of the block body, right after "block{" + newline.
        var insertAt = bodyStart;
        if (insertAt < text.Length && text[insertAt] == '\r') insertAt++;
        if (insertAt < text.Length && text[insertAt] == '\n') insertAt++;
        var line = $"  {key}:b={literal}{newline}";
        if (insertAt == bodyStart) line = newline + line; // "block{" had content on the same line
        return text[..insertAt] + line + text[insertAt..];
    }

    private static Regex KeyRegex(string key) =>
        new($@"(?m)^[ \t]*{Regex.Escape(key)}:b=(?<v>[A-Za-z]+)");

    /// <summary>
    /// Returns (bodyStart, bodyEnd): bodyStart = index right after the opening '{',
    /// bodyEnd = index of the matching '}'. Only considers blocks at nesting depth 0.
    /// </summary>
    internal static (int, int)? FindTopLevelBlock(string text, string block)
    {
        var depth = 0;
        var inString = false;
        var lineStart = true;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (c == '"') inString = false;
                continue;
            }
            if (c == '\n') { lineStart = true; continue; }
            if (char.IsWhiteSpace(c)) continue;

            if (depth == 0 && lineStart && string.CompareOrdinal(text, i, block, 0, block.Length) == 0)
            {
                var j = i + block.Length;
                while (j < text.Length && (text[j] == ' ' || text[j] == '\t')) j++;
                if (j < text.Length && text[j] == '{')
                {
                    var end = FindMatchingBrace(text, j);
                    if (end >= 0) return (j + 1, end);
                }
            }
            lineStart = false;

            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}') depth--;
        }
        return null;
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        var depth = 0;
        var inString = false;
        for (var i = openIndex; i < text.Length; i++)
        {
            var c = text[i];
            if (inString) { if (c == '"') inString = false; continue; }
            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return -1;
    }
}
