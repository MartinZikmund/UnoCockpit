using System.Diagnostics;
using SkiaSharp;
using UnoCockpit.Core.Game;
using Windows.Foundation;

namespace UnoCockpit.Views.Drawing;

/// <summary>Draws a <see cref="ChargeRunGame"/>: a neon lot scrolling under the car, charge bolts and cones.</summary>
public sealed partial class ArcadeCanvas : ThemedCanvas
{
    // The road sits between the on-screen steering pads.
    private const float RoadLeft = 0.14f;
    private const float RoadRight = 0.86f;
    private const double HudBand = 0.16;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _last;
    private float _scroll;

    public ChargeRunGame? Game { get; set; }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        float w = (float)area.Width;
        float h = (float)area.Height;
        TimeSpan now = _clock.Elapsed;
        float dt = (float)Math.Min(0.1, (now - _last).TotalSeconds);
        _last = now;

        ChargeRunGame? game = Game;
        if (game is { IsRunning: true })
        {
            _scroll = (_scroll + (float)(ChargeRunGame.ScrollPerSecond * game.SpeedFactor) * dt) % 0.25f;
        }

        canvas.Clear(IsDarkTheme ? new SKColor(0x0A, 0x0B, 0x0D) : new SKColor(0xE9, 0xEA, 0xEC));
        DrawRoad(canvas, w, h);
        if (game is null)
        {
            return;
        }

        foreach (Entity e in game.Entities)
        {
            // Entities appear below the score and battery strip instead of sliding under it.
            if (e.Y < HudBand)
            {
                continue;
            }

            float x = RoadX(e.X, w);
            float y = (float)e.Y * h;
            if (e.Kind == EntityKind.Bolt)
            {
                DrawBolt(canvas, x, y, w * 0.03f);
            }
            else
            {
                DrawCone(canvas, x, y, w * 0.026f);
            }
        }

        bool blink = game.IsInvulnerable && (int)(now.TotalMilliseconds / 120) % 2 == 0;
        if (!blink)
        {
            DrawCar(canvas, RoadX(game.CarX, w), (float)ChargeRunGame.CarY * h, w * 0.05f);
        }
    }

    private void DrawRoad(SKCanvas canvas, float w, float h)
    {
        using SKPaint edge = Stroke(Accent, 4);
        float left = RoadLeft * w;
        float right = RoadRight * w;
        canvas.DrawLine(left, 0, left, h, edge);
        canvas.DrawLine(right, 0, right, h, edge);

        // Parking-bay dashes scroll toward the driver to sell the speed.
        using SKPaint dash = Stroke(Faint, 3);
        for (int lane = 1; lane < 4; lane++)
        {
            float x = left + (right - left) * lane / 4;
            for (float y = (_scroll - 0.25f) * h; y < h; y += 0.25f * h)
            {
                canvas.DrawLine(x, y, x, y + 0.1f * h, dash);
            }
        }
    }

    private static float RoadX(double x, float w) => (RoadLeft + (float)x * (RoadRight - RoadLeft)) * w;

    private void DrawCar(SKCanvas canvas, float x, float y, float width)
    {
        float length = width * 2.1f;
        using SKPaint body = Fill(Primary);
        using SKPaint glass = Fill(IsDarkTheme ? new SKColor(0x0A, 0x0B, 0x0D) : new SKColor(0x2B, 0x2F, 0x36));
        using SKPaint lamp = Fill(Accent);
        canvas.DrawRoundRect(new SKRect(x - width / 2, y - length / 2, x + width / 2, y + length / 2), width * 0.35f, width * 0.35f, body);
        canvas.DrawRoundRect(new SKRect(x - width * 0.36f, y - length * 0.22f, x + width * 0.36f, y + length * 0.25f), width * 0.25f, width * 0.25f, glass);
        canvas.DrawRect(new SKRect(x - width * 0.4f, y - length / 2 + 2, x - width * 0.12f, y - length / 2 + 6), lamp);
        canvas.DrawRect(new SKRect(x + width * 0.12f, y - length / 2 + 2, x + width * 0.4f, y - length / 2 + 6), lamp);
    }

    private static void DrawBolt(SKCanvas canvas, float x, float y, float size)
    {
        using SKPath bolt = new();
        bolt.MoveTo(x + size * 0.15f, y - size);
        bolt.LineTo(x - size * 0.55f, y + size * 0.1f);
        bolt.LineTo(x - size * 0.05f, y + size * 0.1f);
        bolt.LineTo(x - size * 0.2f, y + size);
        bolt.LineTo(x + size * 0.55f, y - size * 0.15f);
        bolt.LineTo(x + size * 0.05f, y - size * 0.15f);
        bolt.Close();
        using SKPaint glow = new() { IsAntialias = true, Color = new SKColor(0x3C, 0xE0, 0x7A, 110), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, size * 0.4f) };
        using SKPaint fill = new() { IsAntialias = true, Color = new SKColor(0x3C, 0xE0, 0x7A) };
        canvas.DrawPath(bolt, glow);
        canvas.DrawPath(bolt, fill);
    }

    private static void DrawCone(SKCanvas canvas, float x, float y, float size)
    {
        using SKPath cone = new();
        cone.MoveTo(x, y - size);
        cone.LineTo(x + size * 0.75f, y + size * 0.8f);
        cone.LineTo(x - size * 0.75f, y + size * 0.8f);
        cone.Close();
        using SKPaint orange = new() { IsAntialias = true, Color = new SKColor(0xFF, 0x7A, 0x1A) };
        using SKPaint stripe = new() { IsAntialias = true, Color = SKColors.White, StrokeWidth = size * 0.22f, Style = SKPaintStyle.Stroke };
        canvas.DrawPath(cone, orange);
        canvas.DrawLine(x - size * 0.38f, y, x + size * 0.38f, y, stripe);
        canvas.DrawRoundRect(new SKRect(x - size, y + size * 0.7f, x + size, y + size * 0.95f), 3, 3, orange);
    }
}
