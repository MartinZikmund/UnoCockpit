using System.Text.Json;
using System.Text.Json.Serialization;
using UnoCockpit.Core.Geo;

namespace UnoCockpit.Core.Demo;

/// <summary>A recorded drive replayed in Demo mode.</summary>
public sealed class DemoTrack
{
    private readonly IReadOnlyList<Point> _points;

    private DemoTrack(IReadOnlyList<Point> points)
    {
        _points = points;
    }

    public TimeSpan Duration => TimeSpan.FromSeconds(_points[^1].T);

    public static DemoTrack Load(Stream json)
    {
        TrackFile file = JsonSerializer.Deserialize(json, DemoTrackJsonContext.Default.TrackFile)
            ?? throw new InvalidDataException("Empty demo track.");
        if (file.Points is not { Count: > 0 } points)
        {
            throw new InvalidDataException("Demo track has no points.");
        }

        return new(points.OrderBy(p => p.T).ToList());
    }

    public GeoSample SampleAt(TimeSpan t, DateTimeOffset wallClock)
    {
        double s = t.TotalSeconds;
        if (s >= _points[^1].T)
        {
            Point last = _points[^1];
            return new(wallClock, last.Lat, last.Lon, last.Alt, 5, 0, last.Heading);
        }

        int i = 0;
        while (i < _points.Count - 2 && _points[i + 1].T <= s)
        {
            i++;
        }

        Point a = _points[i];
        Point b = _points[i + 1];
        double f = b.T > a.T ? Math.Clamp((s - a.T) / (b.T - a.T), 0, 1) : 0;
        return new(
            wallClock,
            Lerp(a.Lat, b.Lat, f),
            Lerp(a.Lon, b.Lon, f),
            Lerp(a.Alt, b.Alt, f),
            5,
            Lerp(a.Speed, b.Speed, f),
            GeoMath.NormalizeDeg(a.Heading + GeoMath.HeadingDeltaDeg(a.Heading, b.Heading) * f));
    }

    private static double Lerp(double a, double b, double f) => a + (b - a) * f;

    internal sealed record Point(
        [property: JsonPropertyName("t")] double T,
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lon")] double Lon,
        [property: JsonPropertyName("alt")] double Alt,
        [property: JsonPropertyName("speed")] double Speed,
        [property: JsonPropertyName("heading")] double Heading);

    internal sealed record TrackFile([property: JsonPropertyName("points")] List<Point>? Points);
}

[JsonSerializable(typeof(DemoTrack.TrackFile))]
internal sealed partial class DemoTrackJsonContext : JsonSerializerContext;
