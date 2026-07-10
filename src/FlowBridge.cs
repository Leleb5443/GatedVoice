using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Flow;

/// <summary>
/// Exposed to the WebView2 UI as <c>window.chrome.webview.hostObjects.flow</c>.
/// Handles window chrome and reads/writes the JSON data files the engine uses.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class FlowBridge
{
    private readonly MainWindow _win;
    private readonly Action _reload;

    public FlowBridge(MainWindow win, Action reload)
    {
        _win = win;
        _reload = reload;
    }

    // ---- window chrome ----
    public void Minimize() => _win.MinimizeWindow();
    public void ToggleMaximize() => _win.ToggleMaximize();
    public void CloseWindow() => _win.HideWindow();
    public void DragMove() => _win.DragMove();
    public void ResizeWindow(int edge) => _win.StartResize(edge);
    public void OpenExternal(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    // ---- data ----
    public string GetData()
    {
        try
        {
            var root = new JsonObject
            {
                ["settings"] = ReadNode(AppSettings.SettingsPath),
                ["dictionary"] = ReadNode(FlowDictionary.Path),
                ["snippets"] = ReadNode(Snippets.Path),
                ["insights"] = ReadNode(Insights.Path),
                ["history"] = ReadNode(History.Path),
                ["scratchpad"] = ReadNode(Scratchpad.Path),
            };
            return root.ToJsonString();
        }
        catch (Exception ex)
        {
            return "{\"error\":" + JsonSerializer.Serialize(ex.Message) + "}";
        }
    }

    public void SaveDictionary(string json) => WriteIfValid(FlowDictionary.Path, json, reload: true);
    public void SaveSnippets(string json) => WriteIfValid(Snippets.Path, json, reload: true);
    public void SaveSettings(string json) => WriteIfValid(AppSettings.SettingsPath, json, reload: true);
    public void SaveScratchpad(string json) => WriteIfValid(Scratchpad.Path, json, reload: false);

    private static JsonNode? ReadNode(string path)
    {
        try { if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path)); }
        catch { }
        return null;
    }

    private void WriteIfValid(string path, string json, bool reload)
    {
        try
        {
            using (JsonDocument.Parse(json)) { } // reject malformed payloads
            Directory.CreateDirectory(AppSettings.DataDir);
            File.WriteAllText(path, json);
            if (reload) _reload();
        }
        catch { }
    }
}
