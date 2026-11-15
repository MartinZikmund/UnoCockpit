namespace UnoCockpit.Core.Geo;

/// <summary>Accumulates a trip from noisy GPS fixes without counting parked jitter.</summary>
public sealed class TripTracker
{
    public const double MaxAccuracyForDistanceM = 50;
    public const double MovingSpeedMps = 1;
    public const int MaxBreadcrumbs = 2_000;

    private readonly List<(double EastM, double NorthM)> _breadcrumbs = new();
    private GeoSample? _first;
    private GeoSample? _previous;
    private GeoSample? _lastAccepted;

    public double DistanceM { get; private set; }

    public TimeSpan Elapsed { get; private set; }

    public double AverageSpeedMps => Elapsed.TotalSeconds > 0 ? DistanceM / Elapsed.TotalSeconds : 0;

    public double? CurrentSpeedMps { get; private set; }

    public double? CurrentHeadingDeg { get; private set; }

    public GeoSample? Latest => _previous;

    public IReadOnlyList<(double EastM, double NorthM)> Breadcrumbs => _breadcrumbs;

    public void Add(GeoSample s)
    {
        if (_first is null || _previous is null || _lastAccepted is null)
        {
            _first = _previous = _lastAccepted = s;
            CurrentSpeedMps = s.SpeedMps;
            CurrentHeadingDeg = s.HeadingDeg;
            _breadcrumbs.Add((0, 0));
            return;
        }

        double dt = (s.Timestamp - _previous.Timestamp).TotalSeconds;
        double stepM = GeoMath.DistanceMeters(_previous.Latitude, _previous.Longitude, s.Latitude, s.Longitude);
        CurrentSpeedMps = s.SpeedMps ?? (dt > 0 ? stepM / dt : CurrentSpeedMps);
        CurrentHeadingDeg = s.HeadingDeg ?? (stepM > 3
            ? GeoMath.BearingDeg(_previous.Latitude, _previous.Longitude, s.Latitude, s.Longitude)
            : CurrentHeadingDeg);
        Elapsed = s.Timestamp - _first.Timestamp;
        _previous = s;

        if (s.AccuracyM > MaxAccuracyForDistanceM)
        {
            return;
        }

        double dispM = GeoMath.DistanceMeters(_lastAccepted.Latitude, _lastAccepted.Longitude, s.Latitude, s.Longitude);
        // With a reported speed, trust it; without one, require a jump bigger than two accuracy radii of jitter.
        bool moved = s.SpeedMps is { } speed
            ? speed >= MovingSpeedMps
            : dispM > Math.Max(2 * s.AccuracyM, 5);

        if (!moved)
        {
            return;
        }

        DistanceM += dispM;
        _lastAccepted = s;
        AddBreadcrumb(s);
    }

    public void Reset()
    {
        _breadcrumbs.Clear();
        _first = _previous = _lastAccepted = null;
        DistanceM = 0;
        Elapsed = TimeSpan.Zero;
        CurrentSpeedMps = null;
        CurrentHeadingDeg = null;
    }

    private void AddBreadcrumb(GeoSample s)
    {
        GeoSample origin = _first!;
        double north = GeoMath.ToRadians(s.Latitude - origin.Latitude) * GeoMath.EarthRadiusM;
        double east = GeoMath.ToRadians(s.Longitude - origin.Longitude) * GeoMath.EarthRadiusM * Math.Cos(GeoMath.ToRadians(origin.Latitude));
        _breadcrumbs.Add((east, north));

        if (_breadcrumbs.Count >= MaxBreadcrumbs)
        {
            for (int i = _breadcrumbs.Count - 2; i > 0; i -= 2)
            {
                _breadcrumbs.RemoveAt(i);
            }
        }
    }
}
