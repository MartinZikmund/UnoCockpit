using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnoCockpit.Core.Formatting;
using UnoCockpit.Core.Geo;
using UnoCockpit.Core.Motion;

namespace UnoCockpit.Core.ViewModels;

public sealed partial class TripViewModel : ObservableObject
{
    private static readonly IReadOnlyList<(double EastM, double NorthM)> NoBreadcrumbs = [];

    private TripTracker? _tracker;
    private UnitsFormatter _units;

    public TripViewModel(bool metric)
    {
        _units = new(metric);
        Metric = metric;
    }

    public UnitsFormatter Units => _units;

    [ObservableProperty]
    public partial bool Metric { get; set; }

    [ObservableProperty]
    public partial string SpeedText { get; set; } = "—";

    [ObservableProperty]
    public partial string UnitLabel { get; set; } = "";

    [ObservableProperty]
    public partial double HeadingDeg { get; set; }

    [ObservableProperty]
    public partial string AltitudeText { get; set; } = "—";

    [ObservableProperty]
    public partial double AccuracyM { get; set; }

    [ObservableProperty]
    public partial string DistanceText { get; set; } = "";

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = "00:00";

    [ObservableProperty]
    public partial string AverageText { get; set; } = "—";

    [ObservableProperty]
    public partial GForce G { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<(double EastM, double NorthM)> Breadcrumbs { get; set; } = NoBreadcrumbs;

    /// <summary>Raised after every refresh, so drawing surfaces can redraw once.</summary>
    public event EventHandler? Refreshed;

    partial void OnMetricChanged(bool value)
    {
        _units = new(value);
        if (_tracker is not null)
        {
            Refresh(_tracker, G);
        }
        else
        {
            UnitLabel = _units.SpeedUnit;
            DistanceText = _units.Distance(0);
        }
    }

    [RelayCommand]
    private void ToggleUnits() => Metric = !Metric;

    public void Refresh(TripTracker tracker, GForce g)
    {
        _tracker = tracker;
        SpeedText = _units.Speed(tracker.CurrentSpeedMps);
        UnitLabel = _units.SpeedUnit;
        HeadingDeg = tracker.CurrentHeadingDeg ?? HeadingDeg;
        AltitudeText = _units.Altitude(tracker.Latest?.AltitudeM);
        AccuracyM = tracker.Latest?.AccuracyM ?? 0;
        DistanceText = _units.Distance(tracker.DistanceM);
        ElapsedText = UnitsFormatter.Duration(tracker.Elapsed);
        AverageText = tracker.Elapsed > TimeSpan.Zero ? $"{_units.Speed(tracker.AverageSpeedMps)} {_units.SpeedUnit}" : "—";
        G = g;
        Breadcrumbs = tracker.Breadcrumbs;
        Refreshed?.Invoke(this, EventArgs.Empty);
    }
}
