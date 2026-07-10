using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow;

/// <summary>A voice trigger phrase mapped to canned text that replaces it.</summary>
public sealed class Snippet
{
    public string Trigger { get; set; } = "";
    public string Expansion { get; set; } = "";
}

/// <summary>Text expansion: say a trigger phrase, get the expansion pasted instead.</summary>
public sealed class Snippets
{
    public List<Snippet> Items { get; set; } = new();

    [JsonIgnore]
    public static string Path => System.IO.Path.Combine(AppSettings.DataDir, "snippets.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static Snippets Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var s = JsonSerializer.Deserialize<Snippets>(File.ReadAllText(Path));
                if (s != null) return s;
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

    public string Apply(string text)
    {
        foreach (var s in Items)
            text = TextTools.ReplacePhrase(text, s.Trigger, s.Expansion);
        return text;
    }

    // Example snippets so the feature is discoverable. Edit or clear these in the app.
    private static Snippets Seed() => new()
    {
        Items =
        {
            new Snippet { Trigger = "my email", Expansion = "you@example.com" },
            new Snippet { Trigger = "my website", Expansion = "example.com" },
        },
    };
}
