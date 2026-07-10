using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow;

/// <summary>One vocabulary term Flow should spell correctly, plus phrases it's often misheard as.</summary>
public sealed class DictEntry
{
    public string Word { get; set; } = "";
    public List<string> SoundsLike { get; set; } = new();
}

/// <summary>
/// Custom dictionary. Terms bias the Whisper decoder (via an initial prompt) so names
/// and jargon spell right, and any "sounds like" phrases are corrected after the fact.
/// </summary>
public sealed class FlowDictionary
{
    public List<DictEntry> Entries { get; set; } = new();

    [JsonIgnore]
    public static string Path => System.IO.Path.Combine(AppSettings.DataDir, "dictionary.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static FlowDictionary Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var d = JsonSerializer.Deserialize<FlowDictionary>(File.ReadAllText(Path));
                if (d != null) return d;
            }
        }
        catch { }

        var seed = Seed();
        seed.Save();
        return seed;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataDir);
            File.WriteAllText(Path, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { }
    }

    /// <summary>Initial prompt fed to Whisper so it prefers these spellings.</summary>
    public string BuildPrompt()
    {
        if (Entries.Count == 0) return "";
        var words = new List<string>();
        foreach (var e in Entries)
            if (!string.IsNullOrWhiteSpace(e.Word)) words.Add(e.Word.Trim());
        if (words.Count == 0) return "";
        return "Spell these terms exactly when they occur: " + string.Join(", ", words) + ".";
    }

    /// <summary>Fix any known "sounds like" phrases in a transcription.</summary>
    public string Apply(string text)
    {
        foreach (var e in Entries)
            foreach (var phrase in e.SoundsLike)
                text = TextTools.ReplacePhrase(text, phrase, e.Word);
        return text;
    }

    // A few example entries so the feature is discoverable. Edit or clear these in the app.
    private static FlowDictionary Seed() => new()
    {
        Entries =
        {
            new DictEntry { Word = "GitHub", SoundsLike = { "git hub" } },
            new DictEntry { Word = "PostgreSQL", SoundsLike = { "postgres q l", "postgre sequel" } },
            new DictEntry { Word = "OAuth", SoundsLike = { "o auth", "oauth" } },
            new DictEntry { Word = "Whisper" },
        },
    };
}
