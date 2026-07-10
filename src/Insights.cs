using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow;

/// <summary>Usage stats: total words, average WPM, per-day counts, and a daily streak.</summary>
public sealed class Insights
{
    public long TotalWords { get; set; }
    public long TotalDictations { get; set; }
    public double TotalSeconds { get; set; }
    public string LastDay { get; set; } = "";
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public Dictionary<string, int> DailyWords { get; set; } = new();

    [JsonIgnore]
    public static string Path => System.IO.Path.Combine(AppSettings.DataDir, "insights.json");

    [JsonIgnore]
    public double AverageWpm => TotalSeconds > 0 ? TotalWords / (TotalSeconds / 60.0) : 0;

    [JsonIgnore]
    public int TodayWords
    {
        get { DailyWords.TryGetValue(Today(), out int w); return w; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string Today() => DateTime.Now.ToString("yyyy-MM-dd");

    public static Insights Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var i = JsonSerializer.Deserialize<Insights>(File.ReadAllText(Path));
                if (i != null) return i;
            }
        }
        catch { }
        return new Insights();
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

    public void Record(int words, double seconds)
    {
        if (words <= 0) return;

        string today = Today();
        string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

        if (LastDay == today) { /* same day, streak unchanged */ }
        else if (LastDay == yesterday) CurrentStreak++;
        else CurrentStreak = 1;

        if (CurrentStreak > LongestStreak) LongestStreak = CurrentStreak;
        LastDay = today;

        DailyWords.TryGetValue(today, out int w);
        DailyWords[today] = w + words;

        TotalWords += words;
        TotalDictations++;
        TotalSeconds += seconds;

        Save();
    }
}
