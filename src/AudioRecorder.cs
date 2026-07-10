using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Flow;

/// <summary>
/// Captures microphone audio as 16 kHz mono 16-bit PCM (the format Whisper wants).
/// Windows' WaveIn layer resamples from the hardware format for us.
/// </summary>
public sealed class AudioRecorder
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private TaskCompletionSource<byte[]>? _stopTcs;
    private volatile float _level;
    private readonly object _sync = new();

    public bool IsRecording { get; private set; }

    /// <summary>Live mic loudness, 0..1, updated as audio arrives. Drives the waveform.</summary>
    public float Level => _level;

    public void Start(int deviceNumber = 0)
    {
        if (IsRecording) return;

        _level = 0f;
        _buffer = new MemoryStream();
        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 30,
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
        _waveIn.StartRecording();
        IsRecording = true;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_sync) { _buffer?.Write(e.Buffer, 0, e.BytesRecorded); }

        // RMS loudness of this chunk, mapped to 0..1 for the waveform.
        int n = e.BytesRecorded / 2;
        if (n > 0)
        {
            double sumSq = 0;
            for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
            {
                short s = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                sumSq += (double)s * s;
            }
            double rms = Math.Sqrt(sumSq / n);
            float lvl = (float)Math.Min(1.0, Math.Max(0.0, (rms - 120.0) / 2600.0));
            _level = MathF.Pow(lvl, 0.7f); // perceptual boost so quiet speech still moves the bars
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsRecording = false;
        byte[] data;
        lock (_sync) { data = _buffer?.ToArray() ?? Array.Empty<byte>(); }

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }
        _buffer?.Dispose();
        _buffer = null;

        _stopTcs?.TrySetResult(data);
    }

    /// <summary>A copy of the audio captured so far (for live partial transcription).</summary>
    public byte[] SnapshotPcm()
    {
        lock (_sync) { return _buffer?.ToArray() ?? Array.Empty<byte>(); }
    }

    /// <summary>Stops recording and returns the captured 16 kHz mono 16-bit PCM bytes.</summary>
    public Task<byte[]> StopAsync()
    {
        if (!IsRecording || _waveIn == null)
            return Task.FromResult(Array.Empty<byte>());

        _stopTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waveIn.StopRecording();
        return _stopTcs.Task;
    }
}
