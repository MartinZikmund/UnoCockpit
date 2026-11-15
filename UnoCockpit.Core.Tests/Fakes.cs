using UnoCockpit.Core.Geo;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.Storage;

namespace UnoCockpit.Core.Tests;

internal sealed class FakeLocationSource(bool isDemo = false) : ILocationSource
{
    public Queue<LocationResult> Results { get; } = new();

    public LocationResult Fallback { get; set; } = LocationResult.Failed(LocationStatus.Unavailable);

    public int Calls { get; private set; }

    /// <summary>When set, every call waits on this, like a permission prompt nobody answers.</summary>
    public TaskCompletionSource<LocationResult>? Hang { get; set; }

    public bool IsDemo => isDemo;

    public Task<LocationResult> GetAsync(CancellationToken ct)
    {
        Calls++;
        return Hang?.Task ?? Task.FromResult(Results.Count > 0 ? Results.Dequeue() : Fallback);
    }

    public static LocationResult Ok(double lat = 50, double lon = 14) =>
        new(LocationStatus.Ok, new GeoSample(DateTimeOffset.UnixEpoch, lat, lon, null, 5, 0, null));
}

internal sealed class FakeCamera : ICameraSource
{
    public CapabilityStatus StartResult { get; set; } = CapabilityStatus.Active;

    public byte[]? Snap { get; set; } = [0xFF, 0xD8, 0xFF];

    public int StopCalls { get; private set; }

    public bool IsActive { get; private set; }

    public Task<CapabilityStatus> StartAsync(string videoElementId)
    {
        IsActive = StartResult == CapabilityStatus.Active;
        return Task.FromResult(StartResult);
    }

    public void Stop()
    {
        StopCalls++;
        IsActive = false;
    }

    public Task<byte[]?> SnapJpegAsync() => Task.FromResult(Snap);
}

internal sealed class FakeMic : IMicSource
{
    public Queue<float> Levels { get; } = new();

    public int StopCalls { get; private set; }

    public bool IsActive { get; private set; }

    public Task<CapabilityStatus> StartAsync()
    {
        IsActive = true;
        return Task.FromResult(CapabilityStatus.Active);
    }

    public void Stop()
    {
        StopCalls++;
        IsActive = false;
    }

    public bool TryReadBands(Span<float> bands)
    {
        bands.Fill(0.5f);
        return true;
    }

    public float ReadLevelDb() => Levels.Count > 0 ? Levels.Dequeue() : -100;
}

internal sealed class FakeProbe : ICapabilityProbe
{
    public Task<IReadOnlyList<CapabilityReport>> ProbeAsync() => Task.FromResult<IReadOnlyList<CapabilityReport>>(
    [
        new("geolocation", "Location", CapabilityStatus.Available, "GPS"),
        new("gamepad", "Gamepad", CapabilityStatus.Unsupported, "No API"),
    ]);
}

internal sealed class FakeStore : IKeyValueStore
{
    public Dictionary<string, string> Values { get; } = new();

    public string? Get(string key) => Values.GetValueOrDefault(key);

    public void Set(string key, string value) => Values[key] = value;
}
