using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow;

public sealed class Note
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Updated { get; set; } = "";
}

/// <summary>Quick notes shown on the Scratchpad page. Saved wholesale by the UI.</summary>
public sealed class Scratchpad
{
    public List<Note> Notes { get; set; } = new();

    [JsonIgnore]
    public static string Path => System.IO.Path.Combine(AppSettings.DataDir, "scratchpad.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static Scratchpad Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var s = JsonSerializer.Deserialize<Scratchpad>(File.ReadAllText(Path));
                if (s != null) return s;
            }
        }
        catch { }
        return new Scratchpad();
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
}
