using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;

namespace Flow;

/// <summary>
/// Wraps a loaded Whisper model. The factory (and model weights) stay in memory;
/// each call builds a lightweight processor. Serialized so overlapping dictations
/// can't collide.
/// </summary>
public sealed class Transcriber : IDisposable
{
    private readonly WhisperFactory _factory;
    private readonly string _language;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _tmpWav;

    public Transcriber(string modelPath, string language, string tag = "main")
    {
        _factory = WhisperFactory.FromPath(modelPath);
        _language = string.IsNullOrWhiteSpace(language) ? "en" : language;
        _tmpWav = Path.Combine(AppSettings.DataDir, "capture_" + tag + ".wav");
    }

    /// <param name="pcm16kMono">16 kHz mono 16-bit PCM.</param>
    /// <param name="prompt">Optional initial prompt used to bias spelling (dictionary).</param>
    public async Task<string> TranscribeAsync(byte[] pcm16kMono, string? prompt = null)
    {
        if (pcm16kMono.Length == 0) return string.Empty;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using (var writer = new WaveFileWriter(_tmpWav, new WaveFormat(16000, 16, 1)))
            {
                writer.Write(pcm16kMono, 0, pcm16kMono.Length);
            }

            var builder = _factory.CreateBuilder().WithLanguage(_language);
            if (!string.IsNullOrWhiteSpace(prompt))
                builder = builder.WithPrompt(prompt);

            using var processor = builder.Build();
            using var fs = File.OpenRead(_tmpWav);

            var sb = new StringBuilder();
            await foreach (var seg in processor.ProcessAsync(fs).ConfigureAwait(false))
                sb.Append(seg.Text);

            return TextTools.StripTags(sb.ToString());
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _factory.Dispose();
        _gate.Dispose();
    }
}
