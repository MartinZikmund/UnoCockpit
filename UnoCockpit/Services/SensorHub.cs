using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Dispatching;
using UnoCockpit.Core.Game;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.ViewModels;
using UnoCockpit.Core.Weather;
using UnoCockpit.Sensors.Browser;

namespace UnoCockpit.Services;

/// <summary>
/// Polls the sensors and pushes samples into the view models: location, clock and the slow stuff once a second,
/// gamepad and microphone every frame, but only while the app that needs them is on screen.
/// </summary>
public sealed class SensorHub : IDisposable
{
    private static readonly TimeSpan LocationTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan WeatherInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxFrame = TimeSpan.FromMilliseconds(100);

    private readonly ShellViewModel _vm;
    private readonly Sources _sources;
    private readonly OpenMeteoWeatherSource _weather = new(new HttpClient());
    private readonly DispatcherQueueTimer _timer;
    private readonly Stopwatch _frameClock = new();
    private bool _locationBusy;
    private bool _framesHooked;
    private DateTimeOffset _lastWeather = DateTimeOffset.MinValue;
    private bool _wasRunning;
    private bool _wasCameraActive;
    private bool _wasMicActive;

    public SensorHub(ShellViewModel vm, Sources sources, DispatcherQueue queue)
    {
        _vm = vm;
        _sources = sources;
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => OnSecond();
        vm.PropertyChanged += OnShellPropertyChanged;
        vm.Arcade.Game.Collected += (_, kind) => sources.Audio.Play(kind == EntityKind.Bolt ? "bolt" : "cone");
    }

    public void Start()
    {
        _ = _vm.Lab.RefreshCommand.ExecuteAsync(null);
        _timer.Start();
        OnSecond();
        UpdateFrameHook();
    }

    public void Dispose()
    {
        _timer.Stop();
        _vm.PropertyChanged -= OnShellPropertyChanged;
        if (_framesHooked)
        {
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.ActiveApp) or nameof(ShellViewModel.IsActiveAppLocked))
        {
            UpdateFrameHook();
        }
    }

    private void UpdateFrameHook()
    {
        bool want = _vm.ActiveApp is AppId.Arcade or AppId.Sound && !_vm.IsActiveAppLocked;
        if (want == _framesHooked)
        {
            return;
        }

        _framesHooked = want;
        if (want)
        {
            _frameClock.Restart();
            CompositionTarget.Rendering += OnFrame;
        }
        else
        {
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    private void OnFrame(object? sender, object e)
    {
        TimeSpan dt = _frameClock.Elapsed;
        _frameClock.Restart();
        if (dt > MaxFrame)
        {
            dt = MaxFrame;
        }

        if (_vm.ActiveApp == AppId.Arcade)
        {
            GamepadState pad = _sources.Gamepad.Read();
            _vm.Arcade.Tick(dt, new GameInput(pad.X, pad.A || pad.Start), pad.Connected);
            PlayGameStateCues();
        }
        else if (_vm.ActiveApp == AppId.Sound)
        {
            _vm.Sound.Tick(dt);
        }
    }

    private void PlayGameStateCues()
    {
        bool running = _vm.Arcade.IsRunning;
        if (running && !_wasRunning)
        {
            _sources.Audio.Play("start");
        }
        else if (!running && _wasRunning && _vm.Arcade.IsOver)
        {
            _sources.Audio.Play("over");
        }

        _wasRunning = running;
    }

    private void OnSecond()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _vm.OnClock(now);
        _ = PollLocationAsync();

        if (OperatingSystem.IsBrowser())
        {
            _vm.OnOnline(Interop.IsOnline());
            if (Interop.SpeechTake() is { Length: > 0 } heard && VoiceCommands.TryParse(heard) is { } app)
            {
                _vm.OpenAppCommand.Execute(app);
            }
        }

        if (_sources.Motion.Latest is { } motion)
        {
            _vm.GForce.AddMeasured(now, motion.LongitudinalMps2, motion.LateralMps2);
            _vm.Lab.Update("motion", CapabilityStatus.Active, string.Create(CultureInfo.InvariantCulture, $"{motion.LongitudinalMps2:0.0} / {motion.LateralMps2:0.0} m/s²"));
        }

        if (_vm.LastFix is { } fix && _vm.IsOnline && now - _lastWeather >= WeatherInterval)
        {
            _lastWeather = now;
            _ = UpdateWeatherAsync(fix.Latitude, fix.Longitude);
        }

        UpdateLab();
    }

    private async Task PollLocationAsync()
    {
        if (_locationBusy)
        {
            return;
        }

        _locationBusy = true;
        try
        {
            using CancellationTokenSource cts = new(LocationTimeout);
            Task<LocationResult> read = _sources.Location.GetAsync(cts.Token);
            Task done = await Task.WhenAny(read, Task.Delay(LocationTimeout));
            LocationResult result = done == read ? await read : LocationResult.Failed(LocationStatus.Timeout);
            _vm.OnLocation(result, _sources.Location.IsDemo, _sources.Location.IsAutoDemo);
        }
        finally
        {
            _locationBusy = false;
        }
    }

    private async Task UpdateWeatherAsync(double lat, double lon) =>
        _vm.OnTemperature(await _weather.GetTemperatureCAsync(lat, lon, CancellationToken.None));

    private void UpdateLab()
    {
        if (_vm.LastFix is { } fix)
        {
            _vm.Lab.Update("geolocation", _vm.IsDemo ? CapabilityStatus.Available : CapabilityStatus.Active,
                _vm.IsDemo
                    ? "Demo drive (location unavailable)"
                    : string.Create(CultureInfo.InvariantCulture, $"±{fix.AccuracyM:0} m · speed {(fix.SpeedMps is null ? "derived" : "reported")}"));
        }

        GamepadState pad = _sources.Gamepad.Read();
        if (pad.Connected)
        {
            _vm.Lab.Update("gamepad", CapabilityStatus.Active, "Controller connected");
        }

        if (_sources.Camera.IsActive)
        {
            _vm.Lab.Update("camera", CapabilityStatus.Active, "Streaming to Photo Booth");
        }
        else if (_wasCameraActive)
        {
            _vm.Lab.Update("camera", CapabilityStatus.Available, "Stopped");
        }

        if (_sources.Mic.IsActive)
        {
            _vm.Lab.Update("microphone", CapabilityStatus.Active, string.Create(CultureInfo.InvariantCulture, $"{_vm.Sound.LevelDb:0} dBFS"));
        }
        else if (_wasMicActive)
        {
            _vm.Lab.Update("microphone", CapabilityStatus.Available, "Stopped");
        }

        _wasCameraActive = _sources.Camera.IsActive;
        _wasMicActive = _sources.Mic.IsActive;
    }
}
