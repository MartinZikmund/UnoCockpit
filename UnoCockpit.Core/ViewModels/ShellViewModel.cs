using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnoCockpit.Core.Formatting;
using UnoCockpit.Core.Game;
using UnoCockpit.Core.Geo;
using UnoCockpit.Core.Motion;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.Storage;
using UnoCockpit.Core.Sun;

namespace UnoCockpit.Core.ViewModels;

public enum AppId
{
    Trip,
    Arcade,
    Photo,
    Sound,
    Lab,
}

/// <summary>
/// Root view model. Sensor samples are pushed in by the app's sensor hub; everything else is derived here.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly TripTracker _tracker = new();
    private readonly ParkDetector _park = new();
    private readonly GForceEstimator _g = new();
    private GeoSample? _lastFix;
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    private double? _temperatureC;

    public ShellViewModel(ICameraSource camera, IMicSource mic, ICapabilityProbe probe, IKeyValueStore store, bool metric)
    {
        Trip = new(metric);
        Arcade = new(seed => new ChargeRunGame(seed), store);
        Photo = new(camera, CreatePostcard);
        Sound = new(mic);
        Lab = new(probe);
        Trip.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TripViewModel.Metric))
            {
                OnTemperature(_temperatureC);
            }
        };

        _park.StateChanged += (_, state) =>
        {
            ParkState = state;
            ForwardParkState();
        };
    }

    public TripViewModel Trip { get; }

    public ArcadeViewModel Arcade { get; }

    public PhotoBoothViewModel Photo { get; }

    public CabinSoundViewModel Sound { get; }

    public SensorLabViewModel Lab { get; }

    public GForceEstimator GForce => _g;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActiveAppLocked))]
    public partial AppId ActiveApp { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActiveAppLocked))]
    public partial ParkState ParkState { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActiveAppLocked))]
    public partial bool IsDemo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDemoBannerVisible))]
    public partial bool IsAutoDemo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDemoBannerVisible))]
    public partial bool IsDemoBannerDismissed { get; set; }

    public bool IsDemoBannerVisible => IsAutoDemo && !IsDemoBannerDismissed;

    [ObservableProperty]
    public partial bool IsDark { get; set; } = true;

    [ObservableProperty]
    public partial bool? ThemeOverrideDark { get; set; }

    [ObservableProperty]
    public partial string ClockText { get; set; } = "";

    [ObservableProperty]
    public partial string TemperatureText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasTemperature { get; set; }

    [ObservableProperty]
    public partial bool HasGpsFix { get; set; }

    [ObservableProperty]
    public partial bool IsOnline { get; set; } = true;

    // The demo drive is simulated, so it moves the gear display but never locks the real apps.
    public bool IsActiveAppLocked => ActiveApp is AppId.Arcade or AppId.Photo or AppId.Sound && !IsDemo && ParkState != ParkState.Parked;

    public GeoSample? LastFix => _lastFix;

    partial void OnThemeOverrideDarkChanged(bool? value) => UpdateTheme();

    partial void OnIsDemoChanged(bool value)
    {
        // Demo and real positions don't belong to the same trip.
        _tracker.Reset();
        ForwardParkState();
    }

    private void ForwardParkState()
    {
        ParkState effective = IsDemo ? ParkState.Parked : ParkState;
        Arcade.OnParkStateChanged(effective);
        Photo.OnParkStateChanged(effective);
        Sound.OnParkStateChanged(effective);
    }

    // Leaving an app releases its camera or microphone; nothing keeps listening off-screen.
    partial void OnActiveAppChanged(AppId oldValue, AppId newValue)
    {
        if (oldValue == AppId.Photo)
        {
            Photo.StopCommand.Execute(null);
        }
        else if (oldValue == AppId.Sound)
        {
            Sound.StopCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void OpenApp(AppId app) => ActiveApp = app;

    [RelayCommand]
    private void DismissDemoBanner() => IsDemoBannerDismissed = true;

    public void OnLocation(LocationResult result, bool isDemo, bool isAutoDemo = false)
    {
        IsDemo = isDemo;
        IsAutoDemo = isAutoDemo;

        if (result is not { Status: LocationStatus.Ok, Sample: { } sample })
        {
            HasGpsFix = false;
            // With no location at all we can't tell if the car moves, so the apps stay usable.
            // A transient timeout (a tunnel) keeps the last known state instead of unlocking mid-drive.
            bool noLocation = result.Status is LocationStatus.Denied or LocationStatus.Unsupported || _lastFix is null;
            if (!isDemo && noLocation)
            {
                _park.ForceState(ParkState.Parked);
            }

            return;
        }

        HasGpsFix = true;
        _lastFix = sample;
        _tracker.Add(sample);
        double? speed = _tracker.CurrentSpeedMps;
        // Wall clock, not the fix timestamp: browsers can hand back the same cached fix with a stale timestamp.
        _park.Update(_now, speed);
        GForce g = _g.Add(sample.Timestamp, speed ?? 0, _tracker.CurrentHeadingDeg);
        Trip.Refresh(_tracker, g);
        UpdateTheme();
    }

    public void OnClock(DateTimeOffset now)
    {
        _now = now;
        ClockText = now.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        UpdateTheme();
    }

    public void OnTemperature(double? celsius)
    {
        _temperatureC = celsius;
        TemperatureText = Trip.Units.Temperature(celsius);
        HasTemperature = celsius is not null;
    }

    public void OnOnline(bool online) => IsOnline = online;

    public void ResetTrip()
    {
        _tracker.Reset();
        Trip.Refresh(_tracker, _g.Current);
    }

    private void UpdateTheme() =>
        IsDark = ThemeOverrideDark ?? (_lastFix is not { } fix || !SunCalculator.IsDaylight(_now, fix.Latitude, fix.Longitude));

    private PostcardData CreatePostcard() => new(
        _lastFix is { } fix ? UnitsFormatter.Coordinates(fix.Latitude, fix.Longitude) : "Somewhere on the road",
        _now.ToLocalTime(),
        TemperatureText);
}
