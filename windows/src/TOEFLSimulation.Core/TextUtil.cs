using System.Globalization;
using System.Text;

namespace TOEFLSimulation.Core;

/// <summary>Small helpers that reproduce Swift string and number behavior used by the Mac app.</summary>
public static class TextUtil
{
    /// <summary>Swift <c>Double.rounded()</c>: to nearest, halves away from zero.</summary>
    public static double SwiftRound(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>Swift <c>String.capitalized</c> for simple words: first letter of each word upper-cased, the rest lower-cased.</summary>
    public static string Capitalized(string value)
    {
        var builder = new StringBuilder(value.Length);
        var startOfWord = true;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c)) { startOfWord = true; builder.Append(c); continue; }
            builder.Append(startOfWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            startOfWord = false;
        }
        return builder.ToString();
    }

    /// <summary>Swift <c>trimmingCharacters(in: .whitespacesAndNewlines)</c>.</summary>
    public static string TrimWhitespace(string value) => value.Trim();

    /// <summary>Swift <c>trimmingCharacters(in: .whitespacesAndNewlines.union(.punctuationCharacters))</c>.</summary>
    public static string TrimWhitespaceAndPunctuation(string value)
    {
        int start = 0, end = value.Length;
        while (start < end && (char.IsWhiteSpace(value[start]) || char.IsPunctuation(value[start]))) start++;
        while (end > start && (char.IsWhiteSpace(value[end - 1]) || char.IsPunctuation(value[end - 1]))) end--;
        return value[start..end];
    }

    /// <summary>Swift <c>trimmingCharacters(in: .punctuationCharacters)</c>.</summary>
    public static string TrimPunctuation(string value)
    {
        int start = 0, end = value.Length;
        while (start < end && char.IsPunctuation(value[start])) start++;
        while (end > start && char.IsPunctuation(value[end - 1])) end--;
        return value[start..end];
    }

    /// <summary>Swift <c>split(whereSeparator:)</c> with empty pieces omitted. Combining marks stay with the preceding character.</summary>
    public static List<string> Split(string text, Func<char, bool> isSeparator)
    {
        var result = new List<string>();
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var separator = isSeparator(c) && !(IsCombining(c) && start >= 0);
            if (separator)
            {
                if (start >= 0) { result.Add(text[start..i]); start = -1; }
            }
            else if (start < 0)
            {
                start = i;
            }
        }
        if (start >= 0) result.Add(text[start..]);
        return result;
    }

    private static bool IsCombining(char c)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
    }

    /// <summary>Swift <c>split { $0.isWhitespace }.count</c>: the word count shown under writing boxes.</summary>
    public static int WordCount(string text) => Split(text, char.IsWhiteSpace).Count;

    /// <summary>Number of user-perceived characters (Swift <c>String.count</c>).</summary>
    public static int CharacterCount(string text) => new StringInfo(text).LengthInTextElements;

    /// <summary>The first <paramref name="count"/> user-perceived characters (Swift <c>prefix(_:)</c>).</summary>
    public static string PrefixCharacters(string text, int count)
    {
        if (count <= 0) return "";
        var info = new StringInfo(text);
        return count >= info.LengthInTextElements ? text : info.SubstringByTextElements(0, count);
    }

    /// <summary>The last <paramref name="count"/> user-perceived characters (Swift <c>suffix(_:)</c>).</summary>
    public static string SuffixCharacters(string text, int count)
    {
        var info = new StringInfo(text);
        var length = info.LengthInTextElements;
        return count >= length ? text : info.SubstringByTextElements(length - count, count);
    }

    /// <summary>Swift <c>String(format: "%.1f", value)</c>.</summary>
    public static string OneDecimal(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>Swift <c>String(format: "%02d:%02d", seconds / 60, seconds % 60)</c>, the exam clock.</summary>
    public static string ClockString(int seconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{seconds / 60:00}:{seconds % 60:00}");

    /// <summary>Swift <c>"\(Int((value * 100).rounded()))%"</c>.</summary>
    public static string Percent(double fraction) =>
        ((int)SwiftRound(fraction * 100)).ToString(CultureInfo.InvariantCulture) + "%";
}
