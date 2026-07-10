using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Flow;

/// <summary>
/// "Polish" transform: sends dictated text to an LLM and returns a cleaned-up rewrite.
/// Provider is chosen in settings: ollama (free, local), anthropic, or openai.
/// </summary>
public sealed class AiTransformer
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly AppSettings _s;

    public AiTransformer(AppSettings s) => _s = s;

    public bool Configured => !string.Equals(_s.AiProvider, "none", StringComparison.OrdinalIgnoreCase);

    private const string SystemPrompt =
        "Rewrite the user's dictated text so it is clear, correctly spelled, and properly punctuated. " +
        "Preserve the original meaning and the speaker's natural voice. Do not add information or commentary. " +
        "Do not use em dashes. Return only the rewritten text, with no preamble, labels, or quotation marks.";

    public async Task<string> PolishAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        try
        {
            return _s.AiProvider.ToLowerInvariant() switch
            {
                "ollama" => await Ollama(text),
                "anthropic" => await Anthropic(text),
                "openai" => await OpenAi(text),
                _ => text,
            };
        }
        catch
        {
            return text; // if the LLM fails, fall back to the raw dictation
        }
    }

    private static StringContent Json(object o) =>
        new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");

    private async Task<string> Ollama(string text)
    {
        var body = new
        {
            model = string.IsNullOrWhiteSpace(_s.AiModel) ? "llama3.2" : _s.AiModel,
            prompt = SystemPrompt + "\n\nText:\n" + text,
            stream = false,
        };
        using var resp = await Http.PostAsync(_s.OllamaUrl.TrimEnd('/') + "/api/generate", Json(body));
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("response").GetString()?.Trim() ?? text;
    }

    private async Task<string> Anthropic(string text)
    {
        string model = _s.AiModel.StartsWith("claude", StringComparison.OrdinalIgnoreCase)
            ? _s.AiModel : "claude-3-5-haiku-latest";
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", _s.AiApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = Json(new
        {
            model,
            max_tokens = 1024,
            system = SystemPrompt,
            messages = new[] { new { role = "user", content = text } },
        });
        using var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()?.Trim() ?? text;
    }

    private async Task<string> OpenAi(string text)
    {
        string model = _s.AiModel.Contains("gpt", StringComparison.OrdinalIgnoreCase)
            ? _s.AiModel : "gpt-4o-mini";
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Add("Authorization", "Bearer " + _s.AiApiKey);
        req.Content = Json(new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = text },
            },
        });
        using var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message")
            .GetProperty("content").GetString()?.Trim() ?? text;
    }
}
