using Microsoft.Extensions.Time.Testing;
using UnoCockpit.Core.Sensors;

namespace UnoCockpit.Core.Tests.Sensors;

[TestClass]
public sealed class SourceSelectorTests
{
    private static async Task Call(SourceSelector s, int times)
    {
        for (int i = 0; i < times; i++)
        {
            await s.GetAsync(CancellationToken.None);
        }
    }

    [TestMethod]
    public async Task Denied3x_SwitchesToDemo()
    {
        FakeLocationSource primary = new() { Fallback = LocationResult.Failed(LocationStatus.Denied) };
        FakeLocationSource demo = new(isDemo: true) { Fallback = FakeLocationSource.Ok() };
        SourceSelector s = new(primary, demo, new FakeTimeProvider());

        await Call(s, 2);
        Assert.IsFalse(s.IsDemo);

        LocationResult r = await s.GetAsync(CancellationToken.None);
        Assert.IsTrue(s.IsDemo);
        Assert.IsTrue(s.IsAutoDemo);
        Assert.AreEqual(LocationStatus.Ok, r.Status);
    }

    [TestMethod]
    public async Task NoFixFor10s_SwitchesToDemo()
    {
        FakeTimeProvider time = new();
        FakeLocationSource primary = new() { Fallback = LocationResult.Failed(LocationStatus.Timeout) };
        SourceSelector s = new(primary, new FakeLocationSource(true) { Fallback = FakeLocationSource.Ok() }, time);

        await Call(s, 1);
        time.Advance(TimeSpan.FromSeconds(9));
        await Call(s, 1);
        Assert.IsFalse(s.IsDemo);

        time.Advance(TimeSpan.FromSeconds(1));
        await Call(s, 1);
        Assert.IsTrue(s.IsDemo);
    }

    [TestMethod]
    public async Task HangingPrimary_TimesOut_ThenDemo()
    {
        FakeTimeProvider time = new();
        FakeLocationSource primary = new() { Hang = new() };
        SourceSelector s = new(primary, new FakeLocationSource(true) { Fallback = FakeLocationSource.Ok() }, time);

        Task<LocationResult> first = s.GetAsync(CancellationToken.None);
        Assert.IsFalse(first.IsCompleted);
        time.Advance(SourceSelector.PrimaryTimeout);
        Assert.AreEqual(LocationStatus.Timeout, (await first).Status);

        Task<LocationResult> second = s.GetAsync(CancellationToken.None);
        time.Advance(SourceSelector.PrimaryTimeout);
        LocationResult r = await second;

        Assert.IsTrue(s.IsDemo);
        Assert.AreEqual(LocationStatus.Ok, r.Status);
    }

    [TestMethod]
    public async Task AutoDemo_RecoveryProbe_DoesNotBlock()
    {
        FakeTimeProvider time = new();
        FakeLocationSource primary = new() { Fallback = LocationResult.Failed(LocationStatus.Denied) };
        SourceSelector s = new(primary, new FakeLocationSource(true) { Fallback = FakeLocationSource.Ok() }, time);
        await Call(s, 3);
        Assert.IsTrue(s.IsDemo);

        primary.Hang = new();
        time.Advance(SourceSelector.RecoveryProbeInterval);
        Task<LocationResult> during = s.GetAsync(CancellationToken.None);

        Assert.IsTrue(during.IsCompleted, "demo keeps flowing while the real source is probed");
        Assert.IsTrue(s.IsDemo);
    }

    [TestMethod]
    public async Task TimeoutAfterOk_StaysPrimary()
    {
        FakeLocationSource primary = new() { Fallback = LocationResult.Failed(LocationStatus.Timeout) };
        primary.Results.Enqueue(FakeLocationSource.Ok());
        SourceSelector s = new(primary, new FakeLocationSource(true), new FakeTimeProvider());

        await Call(s, 30);

        Assert.IsFalse(s.IsDemo);
    }

    [TestMethod]
    public async Task ForceDemo_UsesDemo()
    {
        FakeLocationSource primary = new() { Fallback = FakeLocationSource.Ok() };
        FakeLocationSource demo = new(true) { Fallback = FakeLocationSource.Ok(37, -122) };
        SourceSelector s = new(primary, demo, new FakeTimeProvider()) { ForceDemo = true };

        LocationResult r = await s.GetAsync(CancellationToken.None);

        Assert.IsTrue(s.IsDemo);
        Assert.IsFalse(s.IsAutoDemo);
        Assert.AreEqual(37, r.Sample!.Latitude);
        Assert.AreEqual(0, primary.Calls);
    }

    [TestMethod]
    public async Task PrimaryRecovers_SwitchesBack()
    {
        FakeTimeProvider time = new();
        FakeLocationSource primary = new() { Fallback = LocationResult.Failed(LocationStatus.Denied) };
        SourceSelector s = new(primary, new FakeLocationSource(true) { Fallback = FakeLocationSource.Ok() }, time);
        await Call(s, 3);
        Assert.IsTrue(s.IsDemo);

        primary.Fallback = FakeLocationSource.Ok();
        int callsBefore = primary.Calls;
        await Call(s, 5);
        Assert.AreEqual(callsBefore, primary.Calls, "primary is only re-probed every 30 s");

        time.Advance(SourceSelector.RecoveryProbeInterval);
        await Call(s, 1);
        Assert.IsFalse(s.IsDemo);
    }
}
