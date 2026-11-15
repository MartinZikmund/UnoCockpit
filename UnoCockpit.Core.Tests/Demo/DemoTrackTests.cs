using System.Text;
using Microsoft.Extensions.Time.Testing;
using UnoCockpit.Core.Demo;
using UnoCockpit.Core.Geo;
using UnoCockpit.Core.Sensors;

namespace UnoCockpit.Core.Tests.Demo;

[TestClass]
public sealed class DemoTrackTests
{
    private static readonly DateTimeOffset Wall = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static DemoTrack Load(string json) => DemoTrack.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private const string TwoPoints = """
        {"points":[
          {"t":0,"lat":50.0,"lon":14.0,"alt":200,"speed":0,"heading":350},
          {"t":10,"lat":50.001,"lon":14.002,"alt":210,"speed":10,"heading":10}
        ]}
        """;

    [TestMethod]
    public void SampleAt_Midpoint_Interpolates()
    {
        GeoSample s = Load(TwoPoints).SampleAt(TimeSpan.FromSeconds(5), Wall);

        Assert.AreEqual(50.0005, s.Latitude, 1e-9);
        Assert.AreEqual(14.001, s.Longitude, 1e-9);
        Assert.AreEqual(205, s.AltitudeM!.Value, 1e-9);
        Assert.AreEqual(5, s.SpeedMps!.Value, 1e-9);
        Assert.AreEqual(Wall, s.Timestamp);
    }

    [TestMethod]
    public void SampleAt_HeadingShortestArc()
    {
        GeoSample s = Load(TwoPoints).SampleAt(TimeSpan.FromSeconds(5), Wall);

        Assert.AreEqual(0, s.HeadingDeg!.Value, 1e-9);
    }

    [TestMethod]
    public void SampleAt_AfterEnd_IsParked()
    {
        GeoSample s = Load(TwoPoints).SampleAt(TimeSpan.FromSeconds(99), Wall);

        Assert.AreEqual(50.001, s.Latitude, 1e-9);
        Assert.AreEqual(0, s.SpeedMps);
    }

    [TestMethod]
    public async Task DemoSource_UsesTimeProvider()
    {
        DemoTrack track = Load(TwoPoints);
        FakeTimeProvider time = new(Wall);
        DemoLocationSource source = new(track, time);
        time.Advance(TimeSpan.FromSeconds(5));

        LocationResult r = await source.GetAsync(CancellationToken.None);

        Assert.IsTrue(source.IsDemo);
        Assert.AreEqual(LocationStatus.Ok, r.Status);
        Assert.AreEqual(track.SampleAt(TimeSpan.FromSeconds(5), time.GetUtcNow()), r.Sample);
    }

    [TestMethod]
    public async Task DemoSource_LoopsAfterParking()
    {
        DemoTrack track = Load(TwoPoints);
        FakeTimeProvider time = new(Wall);
        DemoLocationSource source = new(track, time);
        time.Advance(track.Duration + DemoLocationSource.ParkedPause + TimeSpan.FromSeconds(5));

        LocationResult r = await source.GetAsync(CancellationToken.None);

        Assert.AreEqual(5, r.Sample!.SpeedMps!.Value, 1e-6);
    }

    [TestMethod]
    public void BundledTrack_IsValid()
    {
        using FileStream f = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "demo-track.json"));
        DemoTrack track = DemoTrack.Load(f);

        Assert.IsTrue(track.Duration >= TimeSpan.FromSeconds(180) && track.Duration <= TimeSpan.FromSeconds(300), $"duration {track.Duration}");
        double maxSpeed = 0;
        for (int s = 0; s <= track.Duration.TotalSeconds; s++)
        {
            maxSpeed = Math.Max(maxSpeed, track.SampleAt(TimeSpan.FromSeconds(s), Wall).SpeedMps!.Value);
        }

        Assert.IsLessThanOrEqualTo(30, maxSpeed);
        Assert.IsGreaterThan(10, maxSpeed);
        for (int s = 0; s <= 10; s++)
        {
            Assert.AreEqual(0, track.SampleAt(track.Duration - TimeSpan.FromSeconds(s), Wall).SpeedMps!.Value, 1e-9);
        }
    }
}
