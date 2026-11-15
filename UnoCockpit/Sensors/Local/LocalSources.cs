using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.Storage;
using Windows.Storage;

namespace UnoCockpit.Sensors.Local;

/// <summary>Sound cue sink; the browser plays cues through Web Audio.</summary>
public interface IAudioOut
{
    void Play(string cue);
}

internal sealed class SilentAudioOut : IAudioOut
{
    public void Play(string cue)
    {
    }
}

internal sealed class UnavailableCameraSource : ICameraSource
{
    public bool IsActive => false;

    public Task<CapabilityStatus> StartAsync(string videoElementId) => Task.FromResult(CapabilityStatus.Unsupported);

    public void Stop()
    {
    }

    public Task<byte[]?> SnapJpegAsync() => Task.FromResult<byte[]?>(null);
}

internal sealed class UnavailableMicSource : IMicSource
{
    public bool IsActive => false;

    public Task<CapabilityStatus> StartAsync() => Task.FromResult(CapabilityStatus.Unsupported);

    public void Stop()
    {
    }

    public bool TryReadBands(Span<float> bands) => false;

    public float ReadLevelDb() => -100;
}

internal sealed class NoMotionSource : IMotionSource
{
    public bool IsSupported => false;

    public MotionSample? Latest => null;
}

internal sealed class DesktopCapabilityProbe : ICapabilityProbe
{
    public Task<IReadOnlyList<CapabilityReport>> ProbeAsync() => Task.FromResult<IReadOnlyList<CapabilityReport>>(
    [
        new("vehicle", "Vehicle", CapabilityStatus.Unsupported, "Desktop build: open the web version in a car"),
        new("geolocation", "Location (GPS)", CapabilityStatus.Unsupported, "Demo drive"),
        new("gamepad", "Gamepad", CapabilityStatus.Available, "Arrow keys and Space"),
    ]);
}

/// <summary>Small persisted values in app local settings (localStorage on WebAssembly).</summary>
internal sealed class LocalSettingsStore : IKeyValueStore
{
    public string? Get(string key) => ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out object? v) ? v as string : null;

    public void Set(string key, string value) => ApplicationData.Current.LocalSettings.Values[key] = value;
}
