using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace MicSentry;

// Draws a small flat, rounded 5-point star entirely in code (no image assets on disk),
// so the tray icon's look lives right next to the logic that picks its color.
internal static class TrayIconFactory
{
    public static readonly Color MutedColor = Color.FromArgb(255, 224, 54, 58);
    public static readonly Color ActiveColor = Color.FromArgb(255, 43, 168, 74);
    public static readonly Color DisabledColor = Color.FromArgb(255, 138, 138, 138);

    public static Icon CreateStarIcon(Color fillColor, int size = 32)
    {
        using var bitmap = CreateStarBitmap(fillColor, size);

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    public static Bitmap CreateStarBitmap(Color fillColor, int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            var points = StarPoints(size / 2f, size / 2f, size * 0.46f, size * 0.20f);
            using var path = RoundedPolygonPath(points, size * 0.09f);

            var strokeColor = ControlPaint.Dark(fillColor, 0.1f);
            using var fillBrush = new SolidBrush(fillColor);
            using var strokePen = new Pen(strokeColor, Math.Max(1.2f, size * 0.045f)) { LineJoin = LineJoin.Round };

            g.FillPath(fillBrush, path);
            g.DrawPath(strokePen, path);
        }
        return bitmap;
    }

    private static PointF[] StarPoints(float cx, float cy, float outerRadius, float innerRadius)
    {
        var pts = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            float r = (i % 2 == 0) ? outerRadius : innerRadius;
            pts[i] = new PointF(cx + (float)(r * Math.Cos(angle)), cy + (float)(r * Math.Sin(angle)));
        }
        return pts;
    }

    // Rounds every corner of an arbitrary closed polygon by pulling back a small
    // distance along each edge and bridging the gap with a bezier that hugs the
    // original vertex — the standard "superellipse corner" approximation trick.
    private static GraphicsPath RoundedPolygonPath(PointF[] points, float radius)
    {
        int n = points.Length;
        var pts = Array.ConvertAll(points, p => new Vector2(p.X, p.Y));
        var entry = new Vector2[n];
        var exit = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            var prev = pts[(i - 1 + n) % n];
            var cur = pts[i];
            var next = pts[(i + 1) % n];

            float r = Math.Min(radius, Math.Min(Vector2.Distance(prev, cur), Vector2.Distance(next, cur)) * 0.5f);

            entry[i] = cur + Vector2.Normalize(prev - cur) * r;
            exit[i] = cur + Vector2.Normalize(next - cur) * r;
        }

        static PointF ToPointF(Vector2 v) => new(v.X, v.Y);

        var path = new GraphicsPath();
        path.StartFigure();

        for (int i = 0; i < n; i++)
        {
            int prevIdx = (i - 1 + n) % n;
            path.AddLine(ToPointF(exit[prevIdx]), ToPointF(entry[i]));

            var c1 = Vector2.Lerp(entry[i], pts[i], 2f / 3f);
            var c2 = Vector2.Lerp(exit[i], pts[i], 2f / 3f);
            path.AddBezier(ToPointF(entry[i]), ToPointF(c1), ToPointF(c2), ToPointF(exit[i]));
        }

        path.CloseFigure();
        return path;
    }
}
