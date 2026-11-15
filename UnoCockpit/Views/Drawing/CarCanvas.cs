using System.Diagnostics;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using UnoCockpit.Core.Geo;
using Windows.Foundation;

namespace UnoCockpit.Views.Drawing;

/// <summary>
/// An original, generic top-down EV. Headlights fade in at night, the front wheels steer with heading changes,
/// and two-finger pinch / rotate spins and zooms it (double-tap resets).
/// </summary>
public sealed partial class CarCanvas : SKCanvasElement
{
    public static readonly DependencyProperty IsNightProperty = DependencyProperty.Register(
        nameof(IsNight), typeof(bool), typeof(CarCanvas), new PropertyMetadata(true, (d, _) => ((CarCanvas)d).StartAnimating()));

    public static readonly DependencyProperty IsParkedProperty = DependencyProperty.Register(
        nameof(IsParked), typeof(bool), typeof(CarCanvas), new PropertyMetadata(true, (d, _) => ((CarCanvas)d).StartAnimating()));

    public static readonly DependencyProperty HeadingDegProperty = DependencyProperty.Register(
        nameof(HeadingDeg), typeof(double), typeof(CarCanvas), new PropertyMetadata(0.0, (d, e) => ((CarCanvas)d).OnHeadingChanged((double)e.OldValue, (double)e.NewValue)));

    private const float MaxWheelAngle = 30;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastFrame;
    private bool _animating;
    private float _lights;
    private float _wheelAngle;
    private float _targetWheelAngle;
    private float _userRotation;
    private float _userScale = 1;

    public CarCanvas()
    {
        ManipulationMode = ManipulationModes.Rotate | ManipulationModes.Scale | ManipulationModes.RotateInertia | ManipulationModes.ScaleInertia;
        ManipulationDelta += (_, e) =>
        {
            _userRotation += e.Delta.Rotation;
            _userScale = Math.Clamp(_userScale * e.Delta.Scale, 0.6f, 1.8f);
            Invalidate();
        };
        DoubleTapped += (_, _) =>
        {
            _userRotation = 0;
            _userScale = 1;
            Invalidate();
        };
        Loaded += (_, _) => StartAnimating();
        Unloaded += (_, _) => StopAnimating();
    }

    public bool IsNight
    {
        get => (bool)GetValue(IsNightProperty);
        set => SetValue(IsNightProperty, value);
    }

    public bool IsParked
    {
        get => (bool)GetValue(IsParkedProperty);
        set => SetValue(IsParkedProperty, value);
    }

    public double HeadingDeg
    {
        get => (double)GetValue(HeadingDegProperty);
        set => SetValue(HeadingDegProperty, value);
    }

    private void OnHeadingChanged(double oldValue, double newValue)
    {
        // Updates arrive about once a second, so a 10° change is a noticeable turn.
        _targetWheelAngle = Math.Clamp((float)GeoMath.HeadingDeltaDeg(oldValue, newValue) * 3, -MaxWheelAngle, MaxWheelAngle);
        StartAnimating();
    }

    private void StartAnimating()
    {
        if (_animating)
        {
            return;
        }

        _animating = true;
        _lastFrame = _clock.Elapsed;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopAnimating()
    {
        _animating = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, object e)
    {
        TimeSpan now = _clock.Elapsed;
        float dt = (float)Math.Min(0.1, (now - _lastFrame).TotalSeconds);
        _lastFrame = now;

        float lightsTarget = IsNight ? 1 : 0;
        _lights = Approach(_lights, lightsTarget, dt * 2.5f);
        _wheelAngle = Approach(_wheelAngle, _targetWheelAngle, dt * 60);
        _targetWheelAngle = Approach(_targetWheelAngle, 0, dt * 12);
        Invalidate();

        if (_lights == lightsTarget && _wheelAngle == 0 && _targetWheelAngle == 0)
        {
            StopAnimating();
        }
    }

    private static float Approach(float value, float target, float step) =>
        value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        float w = (float)area.Width;
        float h = (float)area.Height;
        float length = Math.Min(h * 0.86f, w * 1.7f) * _userScale;
        float width = length * 0.44f;

        canvas.Save();
        canvas.Translate(w / 2, h / 2);
        canvas.RotateDegrees(_userRotation);

        bool night = IsNight;
        SKColor body = night ? new SKColor(0x2C, 0x2F, 0x35) : new SKColor(0xF3, 0xF4, 0xF6);
        SKColor outline = night ? new SKColor(0x4A, 0x4E, 0x57) : new SKColor(0xC4, 0xC7, 0xCD);
        SKColor glass = night ? new SKColor(0x0B, 0x0C, 0x0E) : new SKColor(0x23, 0x27, 0x2E);

        DrawLightCones(canvas, length, width);
        DrawShadow(canvas, length, width, night);
        DrawWheels(canvas, length, width);
        DrawBody(canvas, length, width, body, outline);
        DrawGlass(canvas, length, width, glass);
        DrawLamps(canvas, length, width);

        canvas.Restore();
    }

    private static SKPath BodyPath(float l, float w)
    {
        float hl = l / 2;
        float hw = w / 2;
        SKPath p = new();
        p.MoveTo(0, -hl);
        p.CubicTo(hw * 0.75f, -hl, hw, -hl * 0.92f, hw, -hl * 0.72f);
        p.LineTo(hw * 0.98f, hl * 0.78f);
        p.CubicTo(hw * 0.96f, hl * 0.97f, hw * 0.7f, hl, 0, hl);
        p.CubicTo(-hw * 0.7f, hl, -hw * 0.96f, hl * 0.97f, -hw * 0.98f, hl * 0.78f);
        p.LineTo(-hw, -hl * 0.72f);
        p.CubicTo(-hw, -hl * 0.92f, -hw * 0.75f, -hl, 0, -hl);
        p.Close();
        return p;
    }

    private static void DrawShadow(SKCanvas canvas, float l, float w, bool night)
    {
        using SKPaint shadow = new()
        {
            IsAntialias = true,
            Color = new SKColor(0, 0, 0, night ? (byte)140 : (byte)60),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, w * 0.08f),
        };
        canvas.DrawRoundRect(new SKRect(-w * 0.55f, -l * 0.5f, w * 0.55f, l * 0.52f), w * 0.3f, w * 0.3f, shadow);
    }

    private void DrawWheels(SKCanvas canvas, float l, float w)
    {
        using SKPaint tyre = new() { IsAntialias = true, Color = new SKColor(0x10, 0x11, 0x13) };
        float tw = w * 0.16f;
        float th = l * 0.15f;
        float x = w / 2 - tw * 0.35f;
        float frontY = -l * 0.31f;
        float rearY = l * 0.31f;

        foreach ((float cx, float cy, bool front) in new[] { (-x, frontY, true), (x, frontY, true), (-x, rearY, false), (x, rearY, false) })
        {
            canvas.Save();
            canvas.Translate(cx, cy);
            if (front)
            {
                canvas.RotateDegrees(_wheelAngle);
            }

            canvas.DrawRoundRect(new SKRect(-tw / 2, -th / 2, tw / 2, th / 2), tw * 0.3f, tw * 0.3f, tyre);
            canvas.Restore();
        }
    }

    private static void DrawBody(SKCanvas canvas, float l, float w, SKColor body, SKColor outline)
    {
        using SKPath path = BodyPath(l, w);
        using SKPaint fill = new()
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(-w / 2, 0), new SKPoint(w / 2, 0),
                [body.WithAlpha(255), Lighten(body, 18), body],
                [0f, 0.5f, 1f],
                SKShaderTileMode.Clamp),
        };
        using SKPaint stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1.5f, w * 0.01f), Color = outline };
        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, stroke);
    }

    private static void DrawGlass(SKCanvas canvas, float l, float w, SKColor glass)
    {
        // One sweep of glass from windshield to rear window, like a panoramic roof.
        float hw = w / 2;
        using SKPath roof = new();
        roof.MoveTo(-hw * 0.72f, -l * 0.2f);
        roof.CubicTo(-hw * 0.6f, -l * 0.3f, hw * 0.6f, -l * 0.3f, hw * 0.72f, -l * 0.2f);
        roof.LineTo(hw * 0.78f, l * 0.22f);
        roof.CubicTo(hw * 0.6f, l * 0.3f, -hw * 0.6f, l * 0.3f, -hw * 0.78f, l * 0.22f);
        roof.Close();

        using SKPaint paint = new()
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, -l * 0.3f), new SKPoint(0, l * 0.3f),
                [Lighten(glass, 30), glass, Lighten(glass, 14)],
                [0f, 0.55f, 1f],
                SKShaderTileMode.Clamp),
        };
        canvas.DrawPath(roof, paint);

        using SKPaint mirror = new() { IsAntialias = true, Color = glass };
        canvas.DrawOval(new SKRect(-hw * 1.18f, -l * 0.2f, -hw * 0.94f, -l * 0.155f), mirror);
        canvas.DrawOval(new SKRect(hw * 0.94f, -l * 0.2f, hw * 1.18f, -l * 0.155f), mirror);
    }

    private void DrawLamps(SKCanvas canvas, float l, float w)
    {
        float hw = w / 2;
        byte glow = (byte)(120 + 135 * _lights);
        using SKPaint head = new() { IsAntialias = true, Color = new SKColor(0xF4, 0xF8, 0xFF, glow), StrokeWidth = w * 0.035f, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke };
        canvas.DrawLine(-hw * 0.82f, -l * 0.455f, -hw * 0.45f, -l * 0.485f, head);
        canvas.DrawLine(hw * 0.82f, -l * 0.455f, hw * 0.45f, -l * 0.485f, head);

        using SKPaint tail = new() { IsAntialias = true, Color = new SKColor(0xE3, 0x19, 0x37, (byte)(150 + 105 * _lights)), StrokeWidth = w * 0.03f, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke };
        canvas.DrawLine(-hw * 0.85f, l * 0.475f, hw * 0.85f, l * 0.475f, tail);
    }

    private void DrawLightCones(SKCanvas canvas, float l, float w)
    {
        if (_lights <= 0.01f)
        {
            return;
        }

        float hw = w / 2;
        float reach = l * 0.55f;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * hw * 0.62f;
            using SKPath cone = new();
            cone.MoveTo(x, -l * 0.47f);
            cone.LineTo(x + side * w * 0.75f, -l * 0.47f - reach);
            cone.LineTo(x - side * w * 0.35f, -l * 0.47f - reach);
            cone.Close();
            using SKPaint light = new()
            {
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, w * 0.06f),
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, -l * 0.47f), new SKPoint(0, -l * 0.47f - reach),
                    [new SKColor(0xE8, 0xF0, 0xFF, (byte)(70 * _lights)), SKColors.Transparent],
                    SKShaderTileMode.Clamp),
            };
            canvas.DrawPath(cone, light);
        }
    }

    private static SKColor Lighten(SKColor c, byte amount) =>
        new((byte)Math.Min(255, c.Red + amount), (byte)Math.Min(255, c.Green + amount), (byte)Math.Min(255, c.Blue + amount), c.Alpha);
}
