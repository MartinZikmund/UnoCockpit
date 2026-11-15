using UnoCockpit.Core.Sensors;

namespace UnoCockpit.Core.Demo;

/// <summary>Replays a <see cref="DemoTrack"/> in real time, pausing parked at the end before looping.</summary>
public sealed class DemoLocationSource(DemoTrack track, TimeProvider time) : ILocationSource
{
    public static readonly TimeSpan ParkedPause = TimeSpan.FromSeconds(60);

    private readonly DateTimeOffset _start = time.GetUtcNow();

    public bool IsDemo => true;

    public Task<LocationResult> GetAsync(CancellationToken ct)
    {
        DateTimeOffset now = time.GetUtcNow();
        long cycleTicks = (track.Duration + ParkedPause).Ticks;
        TimeSpan t = TimeSpan.FromTicks((now - _start).Ticks % cycleTicks);
        return Task.FromResult(new LocationResult(LocationStatus.Ok, track.SampleAt(t, now)));
    }
}
