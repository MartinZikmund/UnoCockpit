using UnoCockpit.Core.Geo;

namespace UnoCockpit.Core.Motion;

public readonly record struct GForce(double LongitudinalG, double LateralG);

/// <summary>
/// Estimates G-forces from GPS speed and heading changes, or passes through real accelerometer
/// readings when the browser provides them.
/// </summary>
public sealed class GForceEstimator
{
    public const double G = 9.80665;
    public const double MaxG = 1.5;
    public const double SmoothingAlpha = 0.3;
    public const double MinTurnSpeedMps = 3;
    public static readonly TimeSpan MeasuredFreshness = TimeSpan.FromSeconds(2);

    private DateTimeOffset? _prevTime;
    private double _prevSpeed;
    private double? _prevHeading;
    private GForce _derived;
    private GForce _measured;
    private DateTimeOffset? _measuredAt;

    public GForce Current { get; private set; }

    public GForce Add(DateTimeOffset t, double speedMps, double? headingDeg)
    {
        if (_prevTime is { } prevTime && (t - prevTime).TotalSeconds is var dt && dt > 0)
        {
            double longitudinal = (speedMps - _prevSpeed) / dt / G;
            double lateral = 0;
            if (headingDeg is { } h && _prevHeading is { } ph && speedMps >= MinTurnSpeedMps && _prevSpeed >= MinTurnSpeedMps)
            {
                double omega = GeoMath.ToRadians(GeoMath.HeadingDeltaDeg(ph, h)) / dt;
                lateral = speedMps * omega / G;
            }

            _derived = new(
                SmoothingAlpha * longitudinal + (1 - SmoothingAlpha) * _derived.LongitudinalG,
                SmoothingAlpha * lateral + (1 - SmoothingAlpha) * _derived.LateralG);
        }

        _prevTime = t;
        _prevSpeed = speedMps;
        _prevHeading = headingDeg;

        bool measuredFresh = _measuredAt is { } at && t - at <= MeasuredFreshness;
        Current = Clamp(measuredFresh ? _measured : _derived);
        return Current;
    }

    public void AddMeasured(DateTimeOffset t, double longitudinalMps2, double lateralMps2)
    {
        _measured = new(longitudinalMps2 / G, lateralMps2 / G);
        _measuredAt = t;
        Current = Clamp(_measured);
    }

    private static GForce Clamp(GForce g) =>
        new(Math.Clamp(g.LongitudinalG, -MaxG, MaxG), Math.Clamp(g.LateralG, -MaxG, MaxG));
}
