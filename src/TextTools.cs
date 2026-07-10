using System.Text.RegularExpressions;

namespace Flow;

internal static class TextTools
{
    /// <summary>Case-insensitive whole-phrase replacement (word-boundary anchored).</summary>
    public static string ReplacePhrase(string text, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from)) return text;
        string pattern = @"\b" + Regex.Escape(from.Trim()) + @"\b";
        return Regex.Replace(text, pattern, _ => to, RegexOptions.IgnoreCase);
    }

    public static int WordCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>Removes filler words like "um" and "uh".</summary>
    public static string RemoveFillers(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        text = Regex.Replace(text, @"\b(um+|uh+|erm+|hmm+)\b,?", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s{2,}", " ");
        return text.Trim();
    }

    /// <summary>Removes Whisper's non-speech tags like [BLANK_AUDIO], [Music], *laughs*.</summary>
    public static string StripTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = Regex.Replace(text, @"\[[^\]]*\]", " ");
        text = Regex.Replace(text, @"\*[^\*]*\*", " ");
        text = Regex.Replace(text, @"\s{2,}", " ");
        return text.Trim();
    }
}
