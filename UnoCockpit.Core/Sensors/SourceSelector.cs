namespace UnoCockpit.Core.Sensors;

/// <summary>
/// Uses the real location source, falling back to Demo mode when location is denied or never arrives,
/// and switching back as soon as the real source recovers.
/// </summary>
public sealed class SourceSelector(ILocationSource primary, ILocationSource demo, TimeProvider time) : ILocationSource
{
    public const int DeniedLimit = 3;
    public static readonly TimeSpan PrimaryTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan NoFixWindow = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan RecoveryProbeInterval = TimeSpan.FromSeconds(30);

    private int _deniedCount;
    private bool _everOk;
    private DateTimeOffset? _firstAttempt;
    private DateTimeOffset _lastProbe;
    private Task<LocationResult>? _probe;

    public bool ForceDemo { get; set; }

    /// <summary>True when Demo mode was entered because location didn't work, not by choice.</summary>
    public bool IsAutoDemo { get; private set; }

    public bool IsDemo => ForceDemo || IsAutoDemo;

    bool ILocationSource.IsDemo => IsDemo;

    public async Task<LocationResult> GetAsync(CancellationToken ct)
    {
        if (ForceDemo)
        {
            return await demo.GetAsync(ct);
        }

        if (IsAutoDemo)
        {
            return TryRecover() ?? await demo.GetAsync(ct);
        }

        _firstAttempt ??= time.GetUtcNow();
        LocationResult r = await ReadPrimaryAsync(ct);
        switch (r.Status)
        {
            case LocationStatus.Ok:
                _everOk = true;
                _deniedCount = 0;
                return r;
            case LocationStatus.Denied or LocationStatus.Unsupported:
                _deniedCount++;
                break;
        }

        bool neverArrived = !_everOk && time.GetUtcNow() - _firstAttempt >= NoFixWindow;
        if (_deniedCount >= DeniedLimit || neverArrived)
        {
            IsAutoDemo = true;
            _lastProbe = time.GetUtcNow();
            return await demo.GetAsync(ct);
        }

        return r;
    }

    // Probes the real source in the background so the demo drive never stalls while a prompt is pending.
    private LocationResult? TryRecover()
    {
        if (_probe is null && time.GetUtcNow() - _lastProbe >= RecoveryProbeInterval)
        {
            _lastProbe = time.GetUtcNow();
            _probe = ReadPrimaryAsync(CancellationToken.None);
        }

        if (_probe is not { IsCompleted: true } done)
        {
            return null;
        }

        _probe = null;
        if (done.Result.Status != LocationStatus.Ok)
        {
            return null;
        }

        IsAutoDemo = false;
        _deniedCount = 0;
        _everOk = true;
        return done.Result;
    }

    private async Task<LocationResult> ReadPrimaryAsync(CancellationToken ct)
    {
        try
        {
            return await primary.GetAsync(ct).WaitAsync(PrimaryTimeout, time, ct);
        }
        catch (TimeoutException)
        {
            return LocationResult.Failed(LocationStatus.Timeout);
        }
    }
}
