using UnoCockpit.Core.Motion;

namespace UnoCockpit.Core.Tests.Motion;

[TestClass]
public sealed class ParkDetectorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static ParkDetector ParkedDetector()
    {
        ParkDetector d = new();
        for (int i = 0; i <= 5; i++)
        {
            d.Update(T0.AddSeconds(i), 0);
        }

        return d;
    }

    [TestMethod]
    public void StartsUnknown() => Assert.AreEqual(ParkState.Unknown, new ParkDetector().State);

    [TestMethod]
    public void Slow5s_BecomesParked()
    {
        ParkDetector d = new();
        d.Update(T0, 0.3);
        d.Update(T0.AddSeconds(4.9), 0.3);
        Assert.AreEqual(ParkState.Unknown, d.State);

        d.Update(T0.AddSeconds(5), 0.3);
        Assert.AreEqual(ParkState.Parked, d.State);
    }

    [TestMethod]
    public void Fast_BecomesMovingImmediately()
    {
        ParkDetector d = ParkedDetector();
        d.Update(T0.AddSeconds(6), 2.0);
        d.Update(T0.AddSeconds(7), 2.0);

        Assert.AreEqual(ParkState.Moving, d.State);
    }

    [TestMethod]
    public void FirstFastSample_FromUnknown_IsMoving()
    {
        ParkDetector d = new();
        d.Update(T0, 2.0);

        Assert.AreEqual(ParkState.Moving, d.State);
    }

    [TestMethod]
    public void BetweenThresholds_KeepsState()
    {
        ParkDetector parked = ParkedDetector();
        for (int i = 6; i < 20; i++)
        {
            parked.Update(T0.AddSeconds(i), 1.0);
        }

        Assert.AreEqual(ParkState.Parked, parked.State);

        ParkDetector moving = new();
        moving.Update(T0, 10);
        for (int i = 1; i < 20; i++)
        {
            moving.Update(T0.AddSeconds(i), 1.0);
        }

        Assert.AreEqual(ParkState.Moving, moving.State);
    }

    [TestMethod]
    public void SingleJitterSpike_StaysParked()
    {
        ParkDetector d = ParkedDetector();
        d.Update(T0.AddSeconds(6), 1.6);
        d.Update(T0.AddSeconds(7), 0);

        Assert.AreEqual(ParkState.Parked, d.State);
    }

    [TestMethod]
    public void NullSpeed_KeepsState()
    {
        ParkDetector d = ParkedDetector();
        d.Update(T0.AddSeconds(6), null);

        Assert.AreEqual(ParkState.Parked, d.State);
    }

    [TestMethod]
    public void StateChanged_RaisedOncePerTransition()
    {
        ParkDetector d = new();
        List<ParkState> changes = new();
        d.StateChanged += (_, s) => changes.Add(s);

        for (int i = 0; i <= 10; i++)
        {
            d.Update(T0.AddSeconds(i), 0);
        }

        d.Update(T0.AddSeconds(11), 3);
        d.Update(T0.AddSeconds(12), 3);

        CollectionAssert.AreEqual(new[] { ParkState.Parked, ParkState.Moving }, changes);
    }

    [TestMethod]
    public void ForceState_RaisesChange()
    {
        ParkDetector d = new();
        ParkState? raised = null;
        d.StateChanged += (_, s) => raised = s;
        d.ForceState(ParkState.Parked);

        Assert.AreEqual(ParkState.Parked, raised);
    }
}
