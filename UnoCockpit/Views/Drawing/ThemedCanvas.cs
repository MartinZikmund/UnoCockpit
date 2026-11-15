using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace UnoCockpit.Views.Drawing;

/// <summary>
/// Base for the Skia gauges: redraws on theme changes and exposes the cockpit palette as Skia colors.
/// Text is left to XAML on purpose; WebAssembly has no system fonts for Skia to fall back on.
/// </summary>
public abstract partial class ThemedCanvas : SKCanvasElement
{
    protected ThemedCanvas()
    {
        ActualThemeChanged += (_, _) => Invalidate();
    }

    protected bool IsDarkTheme => ActualTheme == ElementTheme.Dark;

    protected SKColor Primary => IsDarkTheme ? new(0xF4, 0xF5, 0xF7) : new(0x17, 0x1A, 0x20);

    protected SKColor Secondary => IsDarkTheme ? new(0x9A, 0xA0, 0xA9) : new(0x5C, 0x5E, 0x62);

    protected SKColor Faint => IsDarkTheme ? new(0x2B, 0x2E, 0x34) : new(0xDD, 0xDE, 0xE1);

    protected SKColor Accent => IsDarkTheme ? new(0x5B, 0x84, 0xF0) : new(0x3E, 0x6A, 0xE1);

    protected SKColor Warning => IsDarkTheme ? new(0xF0, 0x38, 0x4D) : new(0xE3, 0x19, 0x37);

    protected static SKPaint Stroke(SKColor color, float width) => new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        Color = color,
    };

    protected static SKPaint Fill(SKColor color) => new() { IsAntialias = true, Color = color };
}
