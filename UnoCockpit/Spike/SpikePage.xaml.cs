using System.Diagnostics;
using System.Globalization;
using System.Text;
using SkiaSharp;
using Uno.UI.NativeElementHosting;
using Uno.WinUI.Graphics2DSK;
using UnoCockpit.Sensors.Browser;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Gaming.Input;

namespace UnoCockpit.Spike;

/// <summary>Phase 0 go/no-go page, opened with <c>?spike</c>. Removed once the car test passes.</summary>
public sealed partial class SpikePage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private BrowserHtmlElement? _video;
    private int _overlayTaps;
    private bool _cameraOn;
    private bool _unoGeoBusy;
    private bool _jsGeoBusy;
    private bool _unoAccessRequested;
    private bool _firstFrameReported;

    public SpikePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            CompositionTarget.Rendering -= OnRendering;
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsBrowser())
        {
            _video = BrowserHtmlElement.CreateHtmlElement("video");
            _video.SetHtmlAttribute(("autoplay", ""), ("playsinline", ""), ("muted", ""));
            _video.SetCssStyle(("width", "100%"), ("height", "100%"), ("object-fit", "cover"), ("transform", "scaleX(-1)"), ("background", "#000"));
            VideoHost.Content = _video;
            ProbeText.Text = Interop.ProbeJson().Replace(",\"", ",\n\"");
        }
        else
        {
            VideoHost.Content = new TextBlock { Text = "Browser only" };
        }

        _timer.Tick += (_, _) => Poll();
        _timer.Start();
        CompositionTarget.Rendering += OnRendering;
        Poll();
    }

    private void OnRendering(object? sender, object e)
    {
        Spinner.Invalidate();
        FpsText.Text = $"Renderer · {Spinner.Fps:0} fps";
        if (!_firstFrameReported && OperatingSystem.IsBrowser())
        {
            _firstFrameReported = true;
            using System.Text.Json.JsonDocument probe = System.Text.Json.JsonDocument.Parse(Interop.ProbeJson());
            SummaryText.Text = $"First frame {probe.RootElement.GetProperty("sinceNavigationMs").GetInt64() / 1000.0:0.0} s after navigation · {DateTime.Now:HH:mm}";
        }
    }

    private void Poll()
    {
        PollGamepad();
        _ = PollUnoGeoAsync();
        if (OperatingSystem.IsBrowser())
        {
            _ = PollJsGeoAsync();
        }
    }

    private void PollGamepad()
    {
        IReadOnlyList<Gamepad> pads = Gamepad.Gamepads;
        if (pads.Count == 0)
        {
            GamepadText.Text = "No gamepad detected";
            return;
        }

        GamepadReading r = pads[0].GetCurrentReading();
        GamepadText.Text = $"count {pads.Count}\nLX {r.LeftThumbstickX:+0.00;-0.00} LY {r.LeftThumbstickY:+0.00;-0.00}\nRT {r.RightTrigger:0.00} LT {r.LeftTrigger:0.00}\nbuttons {r.Buttons}";
    }

    private async Task PollUnoGeoAsync()
    {
        if (_unoGeoBusy)
        {
            return;
        }

        _unoGeoBusy = true;
        StringBuilder sb = new();
        try
        {
            if (!_unoAccessRequested)
            {
                _unoAccessRequested = true;
                GeolocationAccessStatus access = await Geolocator.RequestAccessAsync();
                sb.AppendLine($"access {access}");
            }

            Geolocator locator = new() { DesiredAccuracyInMeters = 10 };
            Task<Geoposition> read = locator.GetGeopositionAsync(TimeSpan.Zero, TimeSpan.FromSeconds(5)).AsTask();
            Task done = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(6)));
            if (done != read)
            {
                sb.AppendLine("timeout (6 s)");
            }
            else
            {
                Geocoordinate c = (await read).Coordinate;
                sb.AppendLine(Format(c.Point.Position.Latitude, c.Point.Position.Longitude, c.Point.Position.Altitude, c.Accuracy, c.Speed, c.Heading));
                sb.AppendLine($"at {c.Timestamp:HH:mm:ss}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"error {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _unoGeoBusy = false;
        }

        UnoGeoText.Text = sb.ToString();
    }

    private async Task PollJsGeoAsync()
    {
        if (_jsGeoBusy || !OperatingSystem.IsBrowser())
        {
            return;
        }

        _jsGeoBusy = true;
        try
        {
            string json = await Interop.GeoGetCurrentJson(5000);
            JsGeoText.Text = $"{json.Replace(",\"", ",\n\"")}\nat {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            JsGeoText.Text = $"error {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            _jsGeoBusy = false;
        }
    }

    private async void OnCameraClick(object sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsBrowser() || _video is null)
        {
            CameraText.Text = "Browser only";
            return;
        }

        if (_cameraOn)
        {
            Interop.MediaStopCamera();
            _cameraOn = false;
            CameraButton.Content = "Start camera";
            CameraText.Text = "stopped";
            return;
        }

        CameraText.Text = "requesting…";
        string result = await Interop.MediaStartCamera(_video.ElementId);
        _cameraOn = result == "ok";
        CameraButton.Content = _cameraOn ? "Stop camera" : "Start camera";
        CameraText.Text = result;
    }

    private void OnOverlayClick(object sender, RoutedEventArgs e) => OverlayButton.Content = $"Tap me: {++_overlayTaps}";

    private static string Format(double lat, double lon, double? alt, double acc, double? speed, double? heading) =>
        string.Create(CultureInfo.InvariantCulture, $"lat {lat:0.000000}\nlon {lon:0.000000}\nalt {alt?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}\nacc {acc:0.0} m\nspeed {speed?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"} m/s\nheading {heading?.ToString("0", CultureInfo.InvariantCulture) ?? "-"}");
}

/// <summary>A spinning gradient ring redrawn every frame, to judge renderer smoothness.</summary>
public sealed partial class SpinCanvas : SKCanvasElement
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Queue<long> _frames = new();

    public double Fps { get; private set; }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        long now = _clock.ElapsedMilliseconds;
        _frames.Enqueue(now);
        while (_frames.Count > 0 && now - _frames.Peek() > 1000)
        {
            _frames.Dequeue();
        }

        Fps = _frames.Count;

        float cx = (float)area.Width / 2;
        float cy = (float)area.Height / 2;
        float r = Math.Max(10, Math.Min(cx, cy) - 16);
        float angle = now / 1000f * 180f;

        using SKPaint ring = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 14,
            StrokeCap = SKStrokeCap.Round,
            Shader = SKShader.CreateSweepGradient(new SKPoint(cx, cy), [new SKColor(0x3E, 0x6A, 0xE1), new SKColor(0xE3, 0x19, 0x37), new SKColor(0x3E, 0x6A, 0xE1)]),
        };

        canvas.Save();
        canvas.RotateDegrees(angle, cx, cy);
        canvas.DrawArc(new SKRect(cx - r, cy - r, cx + r, cy + r), 0, 300, false, ring);
        canvas.Restore();
    }
}
