using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Flow;

/// <summary>
/// The floating dictation bar. Drawn as a layered window (per-pixel alpha at native DPI)
/// so it stays crisp with smooth rounded edges. Shows a live waveform driven by the mic
/// level and types transcribed words into its white field. Never takes focus.
/// </summary>
public sealed class OverlayForm : Form
{
    public enum State { Listening, Transcribing }

    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private const int LogicalW = 172;
    private const int LogicalH = 38;
    private const int BarCount = 24;

    private readonly float[] _bars = new float[BarCount];
    private readonly System.Windows.Forms.Timer _timer;
    private Bitmap? _bmp;
    private State _state = State.Listening;
    private float _phase;
    private float _orbPhase;
    private float _smooth;

    // Typewriter state
    private readonly object _textLock = new();
    private string _targetText = "";
    private int _shownChars;
    private int _caretPhase;
    private string _display = "";
    private bool _caretOn = true;
    private readonly float[] _scroll = new float[1]; // monotonic left-scroll offset

    public Func<float>? LevelSource { get; set; }
    internal PillTheme Theme { get; set; } = PillThemes.Light;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Size = new Size(LogicalW, LogicalH);

        _timer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30 fps
        _timer.Tick += OnFrame;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT;
            return cp;
        }
    }

    public void Prime() => _ = Handle;

    /// <summary>Set the text the field should type toward. Safe to call from any thread.</summary>
    public void SetText(string text)
    {
        text ??= "";
        lock (_textLock)
        {
            string shown = _targetText.Substring(0, Math.Min(_shownChars, _targetText.Length));
            _targetText = text;
            // If the new text simply extends what's shown, keep typing forward (animation).
            // If it diverges (a revision or a slid window), snap so it never re-types or swings.
            if (!text.StartsWith(shown, StringComparison.Ordinal))
                _shownChars = text.Length;
            if (_shownChars > _targetText.Length) _shownChars = _targetText.Length;
        }
    }

    public void ShowState(string _, State state)
    {
        _state = state;
        if (state == State.Listening)
        {
            Array.Clear(_bars);
            _smooth = 0f;
            lock (_textLock) { _targetText = ""; _shownChars = 0; }
            _display = "";
            _caretPhase = 0;
            _scroll[0] = 0f;
        }
        if (!Visible) Show();
        PushFrame();
        _timer.Start();
    }

    public void HideState()
    {
        _timer.Stop();
        Hide();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        // Waveform
        float incoming;
        if (_state == State.Listening)
        {
            float target = Math.Clamp(LevelSource?.Invoke() ?? 0f, 0f, 1f);
            _smooth = target > _smooth ? _smooth + (target - _smooth) * 0.6f
                                       : _smooth + (target - _smooth) * 0.25f;
            incoming = _smooth;
        }
        else
        {
            _phase += 0.5f;
            incoming = 0.25f + 0.5f * MathF.Abs(MathF.Sin(_phase));
        }
        Array.Copy(_bars, 1, _bars, 0, BarCount - 1);
        _bars[BarCount - 1] = incoming;
        _orbPhase += 0.22f;

        // Typewriter reveal (fast, catches up quickly when behind)
        bool typing;
        lock (_textLock)
        {
            if (_shownChars < _targetText.Length)
            {
                int remaining = _targetText.Length - _shownChars;
                _shownChars = Math.Min(_targetText.Length, _shownChars + Math.Max(3, remaining / 3));
            }
            typing = _shownChars < _targetText.Length;
            _display = _targetText.Substring(0, _shownChars);
        }
        _caretPhase++;
        _caretOn = typing || (_caretPhase % 32) < 20; // solid while typing, blink when idle

        PushFrame();
    }

    private float GetScale()
    {
        float s = 1f;
        try { uint dpi = Native.GetDpiForWindow(Handle); if (dpi > 0) s = dpi / 96f; } catch { }
        if (s < 1f) s = DeviceDpi / 96f;
        return s <= 0 ? 1f : s;
    }

    private void PushFrame()
    {
        if (!IsHandleCreated) return;

        float scale = GetScale();
        int w = (int)Math.Round(LogicalW * scale);
        int h = (int)Math.Round(LogicalH * scale);

        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        int x = wa.Left + (wa.Width - w) / 2;
        int y = wa.Bottom - h - (int)Math.Round(52 * scale);

        if (_bmp == null || _bmp.Width != w || _bmp.Height != h)
        {
            _bmp?.Dispose();
            _bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        }

        using (var g = Graphics.FromImage(_bmp))
        {
            g.Clear(Color.Transparent);
            g.ScaleTransform(scale, scale);
            PillRenderer.Render(g, LogicalW, LogicalH, _bars, _display, _state == State.Listening, _caretOn, _orbPhase, Theme, _scroll);
        }

        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr hBmp = _bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr old = Native.SelectObject(memDc, hBmp);
        try
        {
            var size = new Native.SIZE { cx = w, cy = h };
            var src = new Native.POINT { x = 0, y = 0 };
            var dst = new Native.POINT { x = x, y = y };
            var blend = new Native.BLENDFUNCTION
            {
                BlendOp = Native.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = Native.AC_SRC_ALPHA,
            };
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
        }
        finally
        {
            Native.SelectObject(memDc, old);
            Native.DeleteObject(hBmp);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _bmp?.Dispose();
        }
        base.Dispose(disposing);
    }
}
