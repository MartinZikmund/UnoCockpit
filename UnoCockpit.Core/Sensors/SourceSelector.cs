namespace UnoCockpit.Core.Sensors;

/// <summary>
/// Uses the real location source, falling back to Demo mode when location is denied or never arrives,
/// and switching back as soon as the real source recovers.
/// </summary>
public sealed class SourceSelector(ILocationSource primary, ILocationSource demo, TimeProvider time) : ILocationSource
{
    public const int DeniedLimit = 3;
    public const int NoFixLimit = 10;
    public static readonly TimeSpan RecoveryProbeInterval = TimeSpan.FromSeconds(30);

    private int _deniedCount;
    private int _noFixCount;
    private bool _everOk;
    private DateTimeOffset _lastProbe;

    public bool ForceDemo { get; set; }

    /// <summary>True when Demo mode was entered because location didn't work, not by choice.</summary>
    public bool IsAutoDemo { get; private set; }

    public bool IsDemo => ForceDemo || IsAutoDemo;

    public async Task<LocationResult> GetAsync(CancellationToken ct)
    {
        if (ForceDemo)
        {
            return await demo.GetAsync(ct);
        }

        if (IsAutoDemo)
        {
            DateTimeOffset now = time.GetUtcNow();
            if (now - _lastProbe < RecoveryProbeInterval)
            {
                return await demo.GetAsync(ct);
            }

            _lastProbe = now;
            LocationResult probe = await primary.GetAsync(ct);
            if (probe.Status != LocationStatus.Ok)
            {
                return await demo.GetAsync(ct);
            }

            IsAutoDemo = false;
            _deniedCount = _noFixCount = 0;
            _everOk = true;
            return probe;
        }

        LocationResult r = await primary.GetAsync(ct);
        switch (r.Status)
        {
            case LocationStatus.Ok:
                _everOk = true;
                _deniedCount = _noFixCount = 0;
                return r;
            case LocationStatus.Denied or LocationStatus.Unsupported:
                _deniedCount++;
                break;
            default:
                _noFixCount++;
                break;
        }

        if (_deniedCount >= DeniedLimit || (!_everOk && _noFixCount >= NoFixLimit))
        {
            IsAutoDemo = true;
            _lastProbe = time.GetUtcNow();
            return await demo.GetAsync(ct);
        }

        return r;
    }

    bool ILocationSource.IsDemo => IsDemo;
}
