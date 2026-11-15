namespace UnoCockpit.Core.Geo;

/// <summary>Accumulates a trip from noisy GPS fixes without counting parked jitter.</summary>
public sealed class TripTracker
{
    public const double MaxAccuracyForDistanceM = 50;
    public const double MovingSpeedMps = 1;
    public const int MaxBreadcrumbs = 2_000;
    public const double StationaryAfterSeconds = 3;
    public const double MaxPlausibleSpeedMps = 100;

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

        double stepM = GeoMath.DistanceMeters(_previous.Latitude, _previous.Longitude, s.Latitude, s.Longitude);
        double stepS = (s.Timestamp - _previous.Timestamp).TotalSeconds;
        if (stepM > MaxPlausibleSpeedMps * Math.Max(stepS, 1))
        {
            // A teleport (demo loop restart, switching sources): start a new trip instead of counting it.
            Reset();
            Add(s);
            return;
        }

        CurrentHeadingDeg = s.HeadingDeg ?? (stepM > 3
            ? GeoMath.BearingDeg(_previous.Latitude, _previous.Longitude, s.Latitude, s.Longitude)
            : CurrentHeadingDeg);
        Elapsed = s.Timestamp - _first.Timestamp;
        _previous = s;

        double dispM = GeoMath.DistanceMeters(_lastAccepted.Latitude, _lastAccepted.Longitude, s.Latitude, s.Longitude);
        double sinceAcceptedS = (s.Timestamp - _lastAccepted.Timestamp).TotalSeconds;
        bool accurate = s.AccuracyM <= MaxAccuracyForDistanceM;
        // With a reported speed, trust it; without one, require a jump bigger than two accuracy radii of jitter.
        bool moved = accurate && (s.SpeedMps is { } speed
            ? speed >= MovingSpeedMps
            : dispM > Math.Max(2 * s.AccuracyM, 5));

        if (s.SpeedMps is { } reported)
        {
            CurrentSpeedMps = reported;
        }
        else if (moved && sinceAcceptedS > 0)
        {
            CurrentSpeedMps = dispM / sinceAcceptedS;
        }
        else if (CurrentSpeedMps is null || sinceAcceptedS >= StationaryAfterSeconds)
        {
            // Jitter around a parked car never clears the threshold above, so it settles to zero.
            CurrentSpeedMps = 0;
        }

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
