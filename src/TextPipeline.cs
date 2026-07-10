namespace Flow;

/// <summary>Post-processes a raw transcription: dictionary corrections, then snippet expansion.</summary>
internal static class TextPipeline
{
    public static string Process(string text, FlowDictionary dict, Snippets snips)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        text = dict.Apply(text);
        text = snips.Apply(text);
        return text.Trim();
    }
}
