using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Flow;

/// <summary>A colour scheme for the pill body (the orb keeps its own colours).</summary>
internal readonly struct PillTheme
{
    public readonly Color Track, TrackEdge, Field, FieldEdge, Text, Placeholder, Caret;

    public PillTheme(Color track, Color trackEdge, Color field, Color fieldEdge,
                     Color text, Color placeholder, Color caret)
    {
        Track = track; TrackEdge = trackEdge; Field = field; FieldEdge = fieldEdge;
        Text = text; Placeholder = placeholder; Caret = caret;
    }
}

internal static class PillThemes
{
    public static readonly PillTheme Light = new(
        Color.FromArgb(0xEA, 0xEA, 0xEF), Color.FromArgb(0xDE, 0xDE, 0xE5),
        Color.White, Color.FromArgb(0, 0, 0, 0),
        Color.FromArgb(0x30, 0x30, 0x38), Color.FromArgb(0xB4, 0xB4, 0xBE),
        Color.FromArgb(0x7B, 0x5A, 0xF7));

    public static readonly PillTheme DarkNavy = new(
        Color.FromArgb(0x17, 0x15, 0x2E), Color.FromArgb(0x2C, 0x29, 0x52),
        Color.FromArgb(0x22, 0x1F, 0x3E), Color.FromArgb(0x38, 0x34, 0x5C),
        Color.FromArgb(0xEC, 0xEB, 0xF7), Color.FromArgb(0x8B, 0x87, 0xB4),
        Color.FromArgb(0xA7, 0x8B, 0xFF));

    public static readonly PillTheme Graphite = new(
        Color.FromArgb(0x20, 0x20, 0x24), Color.FromArgb(0x34, 0x34, 0x3B),
        Color.FromArgb(0x2B, 0x2B, 0x31), Color.FromArgb(0x3C, 0x3C, 0x44),
        Color.FromArgb(0xED, 0xED, 0xF1), Color.FromArgb(0x8A, 0x8A, 0x93),
        Color.FromArgb(0xA7, 0x8B, 0xFF));

    public static readonly PillTheme IndigoGlass = new(
        Color.FromArgb(214, 0x1C, 0x1A, 0x38), Color.FromArgb(120, 0x50, 0x4C, 0x86),
        Color.FromArgb(238, 0x27, 0x24, 0x48), Color.FromArgb(90, 0x46, 0x42, 0x74),
        Color.FromArgb(0xEF, 0xEF, 0xF9), Color.FromArgb(0x9A, 0x96, 0xC0),
        Color.FromArgb(0xB3, 0x9B, 0xFF));

    public static readonly PillTheme Lavender = new(
        Color.FromArgb(0xE7, 0xE3, 0xF6), Color.FromArgb(0xD6, 0xD0, 0xEE),
        Color.White, Color.FromArgb(0, 0, 0, 0),
        Color.FromArgb(0x32, 0x2F, 0x45), Color.FromArgb(0xB0, 0xA9, 0xC8),
        Color.FromArgb(0x7B, 0x5A, 0xF7));

    public static PillTheme Get(string? name) => (name ?? "").Trim().ToLowerInvariant() switch
    {
        "darknavy" or "dark" or "navy" => DarkNavy,
        "graphite" or "charcoal" => Graphite,
        "indigoglass" or "glass" => IndigoGlass,
        "lavender" => Lavender,
        _ => Light,
    };
}

/// <summary>
/// Draws the dictation bar: a rounded track with a text field that types words in,
/// and a glowing voice orb on the right. Drawn in logical units; caller scales for DPI.
/// </summary>
internal static class PillRenderer
{
    public static void Render(Graphics g, int w, int h, float[] bars, string display,
                              bool listening, bool caretOn, float phase, PillTheme theme, float[] scroll)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        float pad = h * 0.11f;

        // Track
        var outer = new RectangleF(0.75f, 0.75f, w - 1.5f, h - 1.5f);
        using (var p = Geo.RoundedRectF(outer, (h - 1.5f) / 2f))
        {
            using var fill = new SolidBrush(theme.Track);
            using var edge = new Pen(theme.TrackEdge, 1f);
            g.FillPath(fill, p);
            g.DrawPath(edge, p);
        }

        // Right voice orb
        float orb = h - 2f * pad;
        var circle = new RectangleF(w - pad - orb, pad, orb, orb);
        DrawVoiceOrb(g, circle, bars, phase);

        // Text field (extended toward the left; only a thin rim on the left)
        float fieldLeft = pad * 0.6f;
        float fieldRight = w - pad - orb - pad;
        var field = new RectangleF(fieldLeft, pad, fieldRight - fieldLeft, h - 2f * pad);
        using (var p = Geo.RoundedRectF(field, field.Height / 2f))
        {
            using var b = new SolidBrush(theme.Field);
            g.FillPath(b, p);
            if (theme.FieldEdge.A > 0)
            {
                using var pen = new Pen(theme.FieldEdge, 1f);
                g.DrawPath(pen, p);
            }
        }

        DrawScrollingText(g, field, display, listening, caretOn, theme, scroll);
    }

    private static void DrawVoiceOrb(Graphics g, RectangleF r, float[] bars, float phase)
    {
        float a = 0.16f;
        int len = bars.Length;
        if (len > 0)
        {
            int k = Math.Min(6, len);
            float sum = 0;
            for (int i = 0; i < k; i++) sum += Math.Clamp(bars[len - 1 - i], 0f, 1f);
            a = Math.Clamp(0.16f + 0.84f * (sum / k), 0.16f, 1f);
        }

        var state = g.Save();
        using (var clipPath = new GraphicsPath()) { clipPath.AddEllipse(r); g.SetClip(clipPath); }

        using (var bgPath = new GraphicsPath())
        {
            bgPath.AddEllipse(r);
            using var pgb = new PathGradientBrush(bgPath)
            {
                CenterPoint = new PointF(r.X + r.Width * 0.5f, r.Y + r.Height * 0.46f),
                CenterColor = Color.FromArgb(255, 0x2B, 0x28, 0x5E),
                SurroundColors = new[] { Color.FromArgb(255, 0x0B, 0x0A, 0x20) },
            };
            g.FillPath(pgb, bgPath);
        }

        int n = 48;
        float cy = r.Y + r.Height / 2f;
        float maxAmp = r.Height * 0.40f;
        var top = new PointF[n + 1];
        var bot = new PointF[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float x = r.X + t * r.Width;
            float bell = MathF.Sin(MathF.PI * t);
            float flow = 0.6f * MathF.Sin(t * 9.0f + phase) + 0.4f * MathF.Sin(t * 15.0f - phase * 0.8f);
            float env = a * bell * (0.5f + 0.5f * (0.5f + 0.5f * flow));
            float hh = env * maxAmp;
            top[i] = new PointF(x, cy - hh);
            bot[i] = new PointF(x, cy + hh);
        }

        using (var shape = new GraphicsPath())
        {
            shape.AddCurve(top, 0.5f);
            var botRev = (PointF[])bot.Clone();
            Array.Reverse(botRev);
            shape.AddCurve(botRev, 0.5f);
            shape.CloseFigure();

            var rect = new RectangleF(r.X, r.Y, r.Width, r.Height);
            using var grad = new LinearGradientBrush(rect, Color.White, Color.White, LinearGradientMode.Horizontal);
            grad.InterpolationColors = new ColorBlend
            {
                Colors = new[]
                {
                    Color.FromArgb(235, 0x35, 0xE8, 0xFF),
                    Color.FromArgb(235, 0x5F, 0x8B, 0xFF),
                    Color.FromArgb(235, 0xFF, 0x5F, 0xC0),
                    Color.FromArgb(235, 0x9B, 0x6B, 0xFF),
                },
                Positions = new[] { 0f, 0.4f, 0.72f, 1f },
            };
            using (var glowPen = new Pen(Color.FromArgb(70, 0x7A, 0x9B, 0xFF), r.Height * 0.11f) { LineJoin = LineJoin.Round })
                g.DrawPath(glowPen, shape);
            g.FillPath(grad, shape);
        }

        using (var bloomPath = new GraphicsPath())
        {
            float bw = r.Width * (0.34f + 0.30f * a);
            float bh = r.Height * (0.30f + 0.22f * a);
            var br = new RectangleF(r.X + r.Width / 2f - bw / 2f, cy - bh / 2f, bw, bh);
            bloomPath.AddEllipse(br);
            using var pg = new PathGradientBrush(bloomPath)
            {
                CenterColor = Color.FromArgb((int)(120 * a + 40), 255, 255, 255),
                SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) },
            };
            g.FillPath(pg, bloomPath);
        }

        g.Restore(state);
    }

    private static void DrawScrollingText(Graphics g, RectangleF field, string display,
                                          bool listening, bool caretOn, PillTheme theme, float[] scroll)
    {
        float leftPad = field.Height * 0.26f;   // small so letters use the space
        float rightPad = field.Height * 0.5f;   // room for the caret
        var rect = new RectangleF(field.X + leftPad, field.Y, field.Width - leftPad - rightPad, field.Height);
        float emSize = field.Height * 0.5f;
        float caretW = Math.Max(1.5f, field.Height * 0.07f);

        using var font = new Font("Segoe UI", emSize, GraphicsUnit.Pixel);
        using var sf = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.None,
            LineAlignment = StringAlignment.Center,
            Alignment = StringAlignment.Near,
        };

        var state = g.Save();
        g.SetClip(rect);

        if (string.IsNullOrEmpty(display))
        {
            scroll[0] = 0f;
            if (listening && caretOn) DrawCaret(g, rect.Left, rect, caretW, theme.Caret);
        }
        else
        {
            float tw = g.MeasureString(display, font, new SizeF(100000f, rect.Height), sf).Width;
            float avail = rect.Width - caretW - 2f;
            float desired = tw <= avail ? 0f : tw - avail; // keep the tail (newest words) at the right edge
            // Ease toward the tail in both directions: smooth (no swing) yet never stuck off-screen.
            scroll[0] += (desired - scroll[0]) * 0.4f;
            if (Math.Abs(desired - scroll[0]) < 0.5f) scroll[0] = desired;
            if (scroll[0] < 0f) scroll[0] = 0f;

            float x = rect.Left - scroll[0];
            using (var b = new SolidBrush(theme.Text))
                g.DrawString(display, font, b, new RectangleF(x, rect.Y, tw + 8f, rect.Height), sf);
            if (caretOn) DrawCaret(g, x + tw + 1.5f, rect, caretW, theme.Caret);

            // Soft fade on the left edge so scrolled-off text dissolves instead of clipping hard.
            if (scroll[0] > 0.5f)
            {
                float fadeW = field.Height * 0.6f;
                var fadeRect = new RectangleF(rect.Left - 1f, rect.Y, fadeW, rect.Height);
                using var fade = new LinearGradientBrush(fadeRect, theme.Field, Color.FromArgb(0, theme.Field), LinearGradientMode.Horizontal);
                g.FillRectangle(fade, fadeRect);
            }
        }

        g.Restore(state);
    }

    private static void DrawCaret(Graphics g, float x, RectangleF rect, float caretW, Color color)
    {
        float ch = rect.Height * 0.56f;
        var cr = new RectangleF(x, rect.Y + (rect.Height - ch) / 2f, caretW, ch);
        using var bp = Geo.RoundedRectF(cr, caretW / 2f);
        using var b = new SolidBrush(color);
        g.FillPath(b, bp);
    }
}
