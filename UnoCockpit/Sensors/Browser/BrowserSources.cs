using System.Runtime.Versioning;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Sensors.Local;

namespace UnoCockpit.Sensors.Browser;

/// <summary>Polls <c>navigator.geolocation.getCurrentPosition</c> through interop.</summary>
[SupportedOSPlatform("browser")]
internal sealed class BrowserLocationSource : ILocationSource
{
    public bool IsDemo => false;

    public async Task<LocationResult> GetAsync(CancellationToken ct) =>
        BrowserJson.ParseLocation(await Interop.GeoGetCurrentJson(5_000));
}

[SupportedOSPlatform("browser")]
internal sealed class BrowserCameraSource : ICameraSource
{
    private string? _videoElementId;

    public bool IsActive { get; private set; }

    public async Task<CapabilityStatus> StartAsync(string videoElementId)
    {
        _videoElementId = videoElementId;
        CapabilityStatus status = ToStatus(await Interop.MediaStartCamera(videoElementId));
        IsActive = status == CapabilityStatus.Active;
        return status;
    }

    public void Stop()
    {
        Interop.MediaStopCamera();
        IsActive = false;
    }

    public async Task<byte[]?> SnapJpegAsync()
    {
        if (_videoElementId is null)
        {
            return null;
        }

        string base64 = await Interop.SnapJpegBase64(_videoElementId);
        return base64.Length == 0 ? null : Convert.FromBase64String(base64);
    }

    internal static CapabilityStatus ToStatus(string result) => result switch
    {
        "ok" => CapabilityStatus.Active,
        "denied" => CapabilityStatus.Denied,
        "unsupported" => CapabilityStatus.Unsupported,
        _ => CapabilityStatus.Unknown,
    };
}

[SupportedOSPlatform("browser")]
internal sealed class BrowserMicSource : IMicSource
{
    public bool IsActive { get; private set; }

    public async Task<CapabilityStatus> StartAsync()
    {
        CapabilityStatus status = BrowserCameraSource.ToStatus(await Interop.MediaStartMic());
        IsActive = status == CapabilityStatus.Active;
        return status;
    }

    public void Stop()
    {
        Interop.MediaStopMic();
        IsActive = false;
    }

    public bool TryReadBands(Span<float> bands)
    {
        if (!IsActive)
        {
            return false;
        }

        double[] values = Interop.MicBands();
        for (int i = 0; i < bands.Length && i < values.Length; i++)
        {
            bands[i] = (float)values[i];
        }

        return true;
    }

    public float ReadLevelDb() => IsActive ? (float)Interop.MicLevelDb() : -100;
}

[SupportedOSPlatform("browser")]
internal sealed class BrowserMotionSource : IMotionSource
{
    public bool IsSupported => Latest is not null;

    public MotionSample? Latest => BrowserJson.ParseMotion(Interop.MotionLatestJson());
}

[SupportedOSPlatform("browser")]
internal sealed class BrowserCapabilityProbe : ICapabilityProbe
{
    public Task<IReadOnlyList<CapabilityReport>> ProbeAsync() => Task.FromResult(BrowserJson.ParseProbe(Interop.ProbeJson()));
}

[SupportedOSPlatform("browser")]
internal sealed class BrowserAudioOut : IAudioOut
{
    public void Play(string cue) => Interop.AudioPlay(cue);
}
