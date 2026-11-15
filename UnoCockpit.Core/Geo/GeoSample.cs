namespace UnoCockpit.Core.Geo;

/// <summary>One position fix. Speed and heading are null when the browser doesn't report them.</summary>
public sealed record GeoSample(
    DateTimeOffset Timestamp,
    double Latitude,
    double Longitude,
    double? AltitudeM,
    double AccuracyM,
    double? SpeedMps,
    double? HeadingDeg);
