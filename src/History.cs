using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow;

public sealed class HistoryItem
{
    public string Time { get; set; } = "";   // ISO local, "yyyy-MM-ddTHH:mm:ss"
    public string Text { get; set; } = "";
    public int Words { get; set; }
}

/// <summary>A rolling log of recent dictations, shown on the Home page.</summary>
public sealed class History
{
    public List<HistoryItem> Items { get; set; } = new();

    [JsonIgnore]
    public static string Path => System.IO.Path.Combine(AppSettings.DataDir, "history.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static History Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var h = JsonSerializer.Deserialize<History>(File.ReadAllText(Path));
                if (h != null) return h;
            }
        }
        catch { }
        return new History();
    }

    public void Add(string text, int words)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Items.Insert(0, new HistoryItem
        {
            Time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Text = text.Trim(),
            Words = words,
        });
        if (Items.Count > 200) Items.RemoveRange(200, Items.Count - 200);
        Save();
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
