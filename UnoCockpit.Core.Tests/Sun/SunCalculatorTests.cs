using UnoCockpit.Core.Sun;

namespace UnoCockpit.Core.Tests.Sun;

[TestClass]
public sealed class SunCalculatorTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(3);

    private static void AssertNear(DateTimeOffset expected, DateTimeOffset? actual)
    {
        Assert.IsNotNull(actual);
        Assert.IsLessThanOrEqualTo(Tolerance, (actual.Value - expected).Duration(), $"expected {expected:u}, got {actual:u}");
    }

    [TestMethod]
    public void London_Solstice()
    {
        SunTimes s = SunCalculator.GetSunTimes(new DateOnly(2024, 6, 21), 51.5074, -0.1278);

        Assert.AreEqual(PolarCondition.None, s.Polar);
        AssertNear(new DateTimeOffset(2024, 6, 21, 3, 43, 0, TimeSpan.Zero), s.SunriseUtc);
        AssertNear(new DateTimeOffset(2024, 6, 21, 20, 21, 0, TimeSpan.Zero), s.SunsetUtc);
    }

    [TestMethod]
    public void Tromso_June_IsPolarDay()
    {
        SunTimes s = SunCalculator.GetSunTimes(new DateOnly(2026, 6, 21), 69.6492, 18.9553);

        Assert.AreEqual(PolarCondition.PolarDay, s.Polar);
        Assert.IsNull(s.SunriseUtc);
        Assert.IsNull(s.SunsetUtc);
        Assert.IsTrue(SunCalculator.IsDaylight(new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero), 69.6492, 18.9553));
    }

    [TestMethod]
    public void Tromso_December_IsPolarNight()
    {
        SunTimes s = SunCalculator.GetSunTimes(new DateOnly(2026, 12, 21), 69.6492, 18.9553);

        Assert.AreEqual(PolarCondition.PolarNight, s.Polar);
        Assert.IsFalse(SunCalculator.IsDaylight(new DateTimeOffset(2026, 12, 21, 11, 0, 0, TimeSpan.Zero), 69.6492, 18.9553));
    }

    [TestMethod]
    public void Auckland_DateLine_Works()
    {
        const double lat = -36.8485;
        const double lon = 174.7633;

        Assert.IsTrue(SunCalculator.IsDaylight(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), lat, lon));
        Assert.IsFalse(SunCalculator.IsDaylight(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero), lat, lon));
    }

    [TestMethod]
    public void Prague_Evening_IsDark()
    {
        Assert.IsFalse(SunCalculator.IsDaylight(new DateTimeOffset(2026, 11, 15, 18, 0, 0, TimeSpan.Zero), 50.0755, 14.4378));
        Assert.IsTrue(SunCalculator.IsDaylight(new DateTimeOffset(2026, 11, 15, 11, 0, 0, TimeSpan.Zero), 50.0755, 14.4378));
    }
}
