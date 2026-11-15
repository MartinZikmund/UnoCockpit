using UnoCockpit.Core.Geo;

namespace UnoCockpit.Core.Sensors;

public enum LocationStatus
{
    Ok,
    Denied,
    Unavailable,
    Timeout,
    Unsupported,
}

public sealed record LocationResult(LocationStatus Status, GeoSample? Sample)
{
    public static LocationResult Failed(LocationStatus status) => new(status, null);
}

public interface ILocationSource
{
    bool IsDemo { get; }

    Task<LocationResult> GetAsync(CancellationToken ct);
}
