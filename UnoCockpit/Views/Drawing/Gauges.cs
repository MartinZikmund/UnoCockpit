using SkiaSharp;
using UnoCockpit.Core.Motion;
using Windows.Foundation;

namespace UnoCockpit.Views.Drawing;

/// <summary>A compass card that turns under a fixed pointer, so "up" is always the direction of travel.</summary>
public sealed partial class CompassRose : ThemedCanvas
{
    public static readonly DependencyProperty HeadingDegProperty = DependencyProperty.Register(
        nameof(HeadingDeg), typeof(double), typeof(CompassRose), new PropertyMetadata(0.0, (d, _) => ((CompassRose)d).Invalidate()));

    public double HeadingDeg
    {
        get => (double)GetValue(HeadingDegProperty);
        set => SetValue(HeadingDegProperty, value);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        float cx = (float)area.Width / 2;
        float cy = (float)area.Height / 2;
        float r = Math.Min(cx, cy) - 12;
        if (r <= 10)
        {
            return;
        }

        using SKPaint ring = Stroke(Faint, 2);
        canvas.DrawCircle(cx, cy, r, ring);

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(-(float)HeadingDeg);
        using SKPaint minor = Stroke(Secondary.WithAlpha(110), 2);
        using SKPaint major = Stroke(Primary, 3);
        for (int deg = 0; deg < 360; deg += 10)
        {
            bool isMajor = deg % 90 == 0;
            canvas.Save();
            canvas.RotateDegrees(deg);
            canvas.DrawLine(0, -r, 0, -r + (isMajor ? r * 0.14f : r * 0.07f), isMajor ? major : minor);
            canvas.Restore();
        }

        // North is marked in red, the way magnetic compasses do.
        using SKPath north = new();
        north.MoveTo(0, -r * 0.82f);
        north.LineTo(r * 0.06f, -r * 0.68f);
        north.LineTo(-r * 0.06f, -r * 0.68f);
        north.Close();
        using SKPaint northPaint = Fill(Warning);
        canvas.DrawPath(north, northPaint);
        canvas.Restore();

        using SKPath pointer = new();
        pointer.MoveTo(cx, cy - r - 6);
        pointer.LineTo(cx - 10, cy - r - 22);
        pointer.LineTo(cx + 10, cy - r - 22);
        pointer.Close();
        using SKPaint pointerPaint = Fill(Accent);
        canvas.DrawPath(pointer, pointerPaint);
    }
}

/// <summary>A "friction circle": braking/accelerating up and down, cornering left and right, with a short fading trail.</summary>
public sealed partial class FrictionCircle : ThemedCanvas
{
    public static readonly DependencyProperty GProperty = DependencyProperty.Register(
        nameof(G), typeof(GForce), typeof(FrictionCircle), new PropertyMetadata(default(GForce), (d, e) => ((FrictionCircle)d).OnG((GForce)e.NewValue)));

    private const int TrailLength = 6;
    private const float FullScaleG = 1f;

    private readonly Queue<GForce> _trail = new();

    public GForce G
    {
        get => (GForce)GetValue(GProperty);
        set => SetValue(GProperty, value);
    }

    private void OnG(GForce g)
    {
        _trail.Enqueue(g);
        while (_trail.Count > TrailLength)
        {
            _trail.Dequeue();
        }

        Invalidate();
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        float cx = (float)area.Width / 2;
        float cy = (float)area.Height / 2;
        float r = Math.Min(cx, cy) - 12;
        if (r <= 10)
        {
            return;
        }

        using SKPaint ring = Stroke(Faint, 2);
        foreach (float g in new[] { 0.25f, 0.5f, 1f })
        {
            canvas.DrawCircle(cx, cy, r * g / FullScaleG, ring);
        }

        canvas.DrawLine(cx - r, cy, cx + r, cy, ring);
        canvas.DrawLine(cx, cy - r, cx, cy + r, ring);

        // Acceleration pushes you back into the seat, so it plots downwards; braking plots up.
        SKPoint ToPoint(GForce g) => new(
            cx + r * Math.Clamp((float)g.LateralG / FullScaleG, -1.1f, 1.1f),
            cy + r * Math.Clamp((float)g.LongitudinalG / FullScaleG, -1.1f, 1.1f));

        int i = 0;
        foreach (GForce g in _trail)
        {
            float age = (float)(i + 1) / _trail.Count;
            using SKPaint dot = Fill(Accent.WithAlpha((byte)(40 + 140 * age)));
            canvas.DrawCircle(ToPoint(g), 4 + 4 * age, dot);
            i++;
        }

        using SKPaint current = Fill(Accent);
        using SKPaint halo = Stroke(Accent.WithAlpha(80), 6);
        SKPoint p = ToPoint(G);
        canvas.DrawCircle(p, 12, current);
        canvas.DrawCircle(p, 20, halo);
    }
}

/// <summary>The trip's path drawn on a plain 100 m grid, auto-fitted, with an arrow at the car.</summary>
public sealed partial class BreadcrumbCanvas : ThemedCanvas
{
    private IReadOnlyList<(double EastM, double NorthM)> _points = [];
    private double _headingDeg;

    public void Update(IReadOnlyList<(double EastM, double NorthM)> points, double headingDeg)
    {
        _points = points;
        _headingDeg = headingDeg;
        Invalidate();
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        float w = (float)area.Width;
        float h = (float)area.Height;
        const float Pad = 28;

        double minE = 0, maxE = 0, minN = 0, maxN = 0;
        foreach ((double e, double n) in _points)
        {
            minE = Math.Min(minE, e);
            maxE = Math.Max(maxE, e);
            minN = Math.Min(minN, n);
            maxN = Math.Max(maxN, n);
        }

        double span = Math.Max(200, Math.Max(maxE - minE, maxN - minN));
        double scale = Math.Min(w - 2 * Pad, h - 2 * Pad) / span;
        double midE = (minE + maxE) / 2;
        double midN = (minN + maxN) / 2;
        SKPoint ToPoint(double e, double n) => new((float)(w / 2 + (e - midE) * scale), (float)(h / 2 - (n - midN) * scale));

        // Grid every 100 m, or coarser when zoomed out.
        double step = 100;
        while (step * scale < 32)
        {
            step *= 2;
        }

        using SKPaint grid = Stroke(Faint, 1);
        double startE = Math.Floor((midE - w / 2 / scale) / step) * step;
        for (double e = startE; ToPoint(e, 0).X < w; e += step)
        {
            float x = ToPoint(e, 0).X;
            canvas.DrawLine(x, 0, x, h, grid);
        }

        double startN = Math.Floor((midN - h / 2 / scale) / step) * step;
        for (double n = startN; ToPoint(0, n).Y > 0; n += step)
        {
            float y = ToPoint(0, n).Y;
            canvas.DrawLine(0, y, w, y, grid);
        }

        if (_points.Count == 0)
        {
            return;
        }

        using SKPath path = new();
        path.MoveTo(ToPoint(_points[0].EastM, _points[0].NorthM));
        for (int i = 1; i < _points.Count; i++)
        {
            path.LineTo(ToPoint(_points[i].EastM, _points[i].NorthM));
        }

        using SKPaint line = Stroke(Accent, 5);
        canvas.DrawPath(path, line);

        using SKPaint start = Stroke(Secondary, 3);
        canvas.DrawCircle(ToPoint(_points[0].EastM, _points[0].NorthM), 7, start);

        SKPoint car = ToPoint(_points[^1].EastM, _points[^1].NorthM);
        canvas.Save();
        canvas.Translate(car.X, car.Y);
        canvas.RotateDegrees((float)_headingDeg);
        using SKPath arrow = new();
        arrow.MoveTo(0, -14);
        arrow.LineTo(10, 10);
        arrow.LineTo(0, 5);
        arrow.LineTo(-10, 10);
        arrow.Close();
        using SKPaint arrowFill = Fill(Primary);
        canvas.DrawPath(arrow, arrowFill);
        canvas.Restore();
    }
}
