using UnoCockpit.Core.Motion;

namespace UnoCockpit.Core.Tests.Motion;

[TestClass]
public sealed class GForceEstimatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Accelerating_AtOneG_ConvergesToOneG()
    {
        GForceEstimator e = new();
        for (int i = 0; i <= 10; i++)
        {
            e.Add(T0.AddSeconds(i), i * GForceEstimator.G, 0);
        }

        Assert.AreEqual(1, e.Current.LongitudinalG, 0.1);
    }

    [TestMethod]
    public void ConstantTurn_LateralMatchesVTimesOmega()
    {
        GForceEstimator e = new();
        for (int i = 0; i <= 20; i++)
        {
            e.Add(T0.AddSeconds(i), 20, i * 9.0 % 360);
        }

        double expected = 20 * (9 * Math.PI / 180) / GForceEstimator.G;
        Assert.AreEqual(expected, e.Current.LateralG, 0.03);
    }

    [TestMethod]
    public void HeadingWrap_IsSmall()
    {
        GForceEstimator e = new();
        e.Add(T0, 20, 359);
        GForce g = e.Add(T0.AddSeconds(1), 20, 1);

        Assert.IsLessThan(0.1, Math.Abs(g.LateralG));
    }

    [TestMethod]
    public void LowSpeed_IgnoresHeading()
    {
        GForceEstimator e = new();
        e.Add(T0, 2, 0);
        GForce g = e.Add(T0.AddSeconds(1), 2, 90);

        Assert.AreEqual(0, g.LateralG);
    }

    [TestMethod]
    public void Output_IsClamped()
    {
        GForceEstimator e = new();
        for (int i = 0; i <= 10; i++)
        {
            e.Add(T0.AddSeconds(i), i * 100, 0);
        }

        Assert.AreEqual(GForceEstimator.MaxG, e.Current.LongitudinalG, 1e-9);
    }

    [TestMethod]
    public void Measured_OverridesDerived()
    {
        GForceEstimator e = new();
        e.AddMeasured(T0, -4.9, 2.45);
        GForce g = e.Add(T0.AddSeconds(1), 50, 0);

        Assert.AreEqual(-0.5, g.LongitudinalG, 0.01);
        Assert.AreEqual(0.25, g.LateralG, 0.01);
    }

    [TestMethod]
    public void Measured_ExpiresAfter2s()
    {
        GForceEstimator e = new();
        e.AddMeasured(T0, -4.9, 2.45);
        e.Add(T0.AddSeconds(3), 10, 0);
        GForce g = e.Add(T0.AddSeconds(4), 10, 0);

        Assert.AreEqual(0, g.LongitudinalG, 0.01);
    }
}
