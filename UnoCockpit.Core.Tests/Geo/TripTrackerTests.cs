using UnoCockpit.Core.Geo;

namespace UnoCockpit.Core.Tests.Geo;

[TestClass]
public sealed class TripTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    // ~1 m of latitude in degrees.
    private const double MetreLat = 1 / 111_195.0;

    private static GeoSample Fix(double seconds, double northM, double eastM = 0, double accuracy = 5, double? speed = null) =>
        new(T0.AddSeconds(seconds), 50 + northM * MetreLat, 14 + eastM * MetreLat / Math.Cos(50 * Math.PI / 180), null, accuracy, speed, null);

    [TestMethod]
    public void NullSpeed_IsDerived()
    {
        TripTracker t = new();
        t.Add(Fix(0, 0, accuracy: 2));
        t.Add(Fix(1, 10, accuracy: 2));

        Assert.IsNotNull(t.CurrentSpeedMps);
        Assert.AreEqual(10, t.CurrentSpeedMps.Value, 0.5);
    }

    [TestMethod]
    public void NullSpeed_ParkedJitter_SettlesToZero()
    {
        TripTracker t = new();
        Random rnd = new(7);
        for (int i = 0; i < 30; i++)
        {
            double angle = rnd.NextDouble() * Math.PI * 2;
            double r = rnd.NextDouble() * 5;
            t.Add(Fix(i, Math.Sin(angle) * r, Math.Cos(angle) * r, accuracy: 5));
        }

        Assert.AreEqual(0, t.CurrentSpeedMps);
    }

    [TestMethod]
    public void NullSpeed_SteadyDrive_IsDerived()
    {
        TripTracker t = new();
        for (int i = 0; i <= 10; i++)
        {
            t.Add(Fix(i, i * 10, accuracy: 5));
        }

        Assert.AreEqual(10, t.CurrentSpeedMps!.Value, 1);
    }

    [TestMethod]
    public void RepeatedCachedFix_NullSpeed_IsZero()
    {
        TripTracker t = new();
        t.Add(Fix(0, 0));
        t.Add(Fix(0, 0));

        Assert.AreEqual(0, t.CurrentSpeedMps);
    }

    [TestMethod]
    public void ParkedJitter_DoesNotAccumulate()
    {
        TripTracker t = new();
        Random rnd = new(42);
        for (int i = 0; i < 60; i++)
        {
            double angle = rnd.NextDouble() * Math.PI * 2;
            double r = rnd.NextDouble() * 8;
            t.Add(Fix(i, Math.Sin(angle) * r, Math.Cos(angle) * r, accuracy: 10, speed: 0));
        }

        Assert.IsLessThan(1, t.DistanceM);
    }

    [TestMethod]
    public void PoorAccuracy_IsIgnoredForDistance()
    {
        TripTracker t = new();
        t.Add(Fix(0, 0));
        t.Add(Fix(1, 30, accuracy: 80, speed: 30));

        Assert.AreEqual(0, t.DistanceM, 1e-9);
    }

    [TestMethod]
    public void Drive_AccumulatesDistance()
    {
        TripTracker t = new();
        for (int i = 0; i <= 50; i++)
        {
            t.Add(Fix(i, i * 20, speed: 20));
        }

        Assert.AreEqual(1_000, t.DistanceM, 20);
        Assert.AreEqual(20, t.AverageSpeedMps, 0.5);
        Assert.AreEqual(TimeSpan.FromSeconds(50), t.Elapsed);
    }

    [TestMethod]
    public void Breadcrumbs_AreLocalMetres()
    {
        TripTracker t = new();
        t.Add(Fix(0, 0));
        t.Add(Fix(10, 100, 50, speed: 11));

        (double east, double north) = t.Breadcrumbs[^1];
        Assert.AreEqual(100, north, 1);
        Assert.AreEqual(50, east, 1);
    }

    [TestMethod]
    public void Breadcrumbs_AreCapped()
    {
        TripTracker t = new();
        for (int i = 0; i < 5_000; i++)
        {
            t.Add(Fix(i, i * 10, speed: 10));
        }

        Assert.IsLessThanOrEqualTo(2_000, t.Breadcrumbs.Count);
    }

    [TestMethod]
    public void Reset_ClearsTrip()
    {
        TripTracker t = new();
        t.Add(Fix(0, 0));
        t.Add(Fix(1, 20, speed: 20));
        t.Reset();

        Assert.AreEqual(0, t.DistanceM);
        Assert.IsEmpty(t.Breadcrumbs);
        Assert.IsNull(t.CurrentSpeedMps);
    }
}
