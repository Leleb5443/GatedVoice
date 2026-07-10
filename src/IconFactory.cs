using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Flow;

internal static class IconFactory
{
    /// <summary>The app mark: a purple gradient squircle with a white voice waveform.</summary>
    public static Icon Create(bool enabled)
    {
        const int S = 32;
        using var bmp = new Bitmap(S, S);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var rect = new Rectangle(1, 1, S - 2, S - 2);
            using (var path = Geo.RoundedRect(rect, 9))
            {
                if (enabled)
                {
                    using var grad = new LinearGradientBrush(rect,
                        Color.FromArgb(0x7B, 0x5A, 0xF7), Color.FromArgb(0x9E, 0x6C, 0xFF), 115f);
                    g.FillPath(grad, path);
                }
                else
                {
                    using var b = new SolidBrush(Color.FromArgb(0x8C, 0x8A, 0x94));
                    g.FillPath(b, path);
                }
            }

            float[] hs = { 0.42f, 0.72f, 1f, 0.66f, 0.5f };
            int n = hs.Length;
            float bw = 3f, gap = 2.7f, total = n * bw + (n - 1) * gap;
            float x0 = (S - total) / 2f, cy = S / 2f, maxH = S * 0.52f;
            using var white = new SolidBrush(Color.FromArgb(enabled ? 255 : 230, 255, 255, 255));
            for (int i = 0; i < n; i++)
            {
                float hh = hs[i] * maxH;
                using var bp = Geo.RoundedRectF(new RectangleF(x0 + i * (bw + gap), cy - hh / 2f, bw, hh), bw / 2f);
                g.FillPath(white, bp);
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
