using UnoCockpit.Core.Geo;

namespace UnoCockpit.Core.Tests.Geo;

[TestClass]
public sealed class GeoMathTests
{
    [TestMethod]
    public void Distance_PragueToBrno_IsAbout185km()
    {
        double d = GeoMath.DistanceMeters(50.0755, 14.4378, 49.1951, 16.6068);

        Assert.AreEqual(184_600, d, 1_000);
    }

    [TestMethod]
    public void HeadingDelta_Wraps()
    {
        Assert.AreEqual(20, GeoMath.HeadingDeltaDeg(350, 10), 1e-9);
        Assert.AreEqual(-20, GeoMath.HeadingDeltaDeg(10, 350), 1e-9);
        Assert.AreEqual(-180, GeoMath.HeadingDeltaDeg(0, 180), 1e-9);
    }

    [TestMethod]
    public void Bearing_DueEast_Is90()
    {
        Assert.AreEqual(90, GeoMath.BearingDeg(0, 0, 0, 1), 1e-6);
    }
}
