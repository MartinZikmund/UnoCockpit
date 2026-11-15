using UnoCockpit.Core.Game;
using UnoCockpit.Core.Geo;
using UnoCockpit.Core.Motion;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Core.Tests.ViewModels;

[TestClass]
public sealed class ShellViewModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 15, 11, 0, 0, TimeSpan.Zero);

    internal static ShellViewModel Create(FakeCamera? camera = null, FakeMic? mic = null) =>
        new(camera ?? new FakeCamera(), mic ?? new FakeMic(), new FakeProbe(), new FakeStore(), metric: true);

    private static LocationResult Fix(DateTimeOffset t, double speed, double lat = 50.0755, double lon = 14.4378) =>
        new(LocationStatus.Ok, new GeoSample(t, lat, lon, 200, 5, speed, 90));

    [TestMethod]
    public void ArcadeLocked_WhenMoving()
    {
        ShellViewModel vm = Create();
        vm.OpenAppCommand.Execute(AppId.Arcade);

        vm.OnLocation(Fix(T0, 10), isDemo: false);
        vm.OnLocation(Fix(T0.AddSeconds(1), 10), isDemo: false);

        Assert.AreEqual(ParkState.Moving, vm.ParkState);
        Assert.IsTrue(vm.IsActiveAppLocked);
    }

    [TestMethod]
    public void TripHud_NeverLocked()
    {
        ShellViewModel vm = Create();
        vm.OnLocation(Fix(T0, 10), isDemo: false);

        Assert.AreEqual(AppId.Trip, vm.ActiveApp);
        Assert.IsFalse(vm.IsActiveAppLocked);
    }

    [TestMethod]
    public void NoFix_ForcesParked()
    {
        ShellViewModel vm = Create();
        vm.OnLocation(LocationResult.Failed(LocationStatus.Denied), isDemo: false);

        Assert.AreEqual(ParkState.Parked, vm.ParkState);
        Assert.IsFalse(vm.HasGpsFix);
    }

    [TestMethod]
    public void IsDark_FollowsSun_UnlessOverridden()
    {
        ShellViewModel vm = Create();
        vm.OnLocation(Fix(T0, 0), isDemo: false);

        vm.OnClock(new DateTimeOffset(2026, 11, 15, 11, 0, 0, TimeSpan.Zero));
        Assert.IsFalse(vm.IsDark);

        vm.OnClock(new DateTimeOffset(2026, 11, 15, 18, 0, 0, TimeSpan.Zero));
        Assert.IsTrue(vm.IsDark);

        vm.ThemeOverrideDark = false;
        Assert.IsFalse(vm.IsDark);
    }

    [TestMethod]
    public void NoFixYet_IsDark() => Assert.IsTrue(Create().IsDark);

    [TestMethod]
    public void Temperature_HiddenWhenNull()
    {
        ShellViewModel vm = Create();
        vm.OnTemperature(null);
        Assert.IsFalse(vm.HasTemperature);

        vm.OnTemperature(7.6);
        Assert.IsTrue(vm.HasTemperature);
        Assert.AreEqual("8°C", vm.TemperatureText);
    }

    [TestMethod]
    public void Location_UpdatesTrip()
    {
        ShellViewModel vm = Create();
        vm.OnLocation(Fix(T0, 27.78), isDemo: false);

        Assert.AreEqual("100", vm.Trip.SpeedText);
        Assert.IsTrue(vm.HasGpsFix);
    }
}

[TestClass]
public sealed class PhotoBoothViewModelTests
{
    private static PhotoBoothViewModel Parked(FakeCamera cam)
    {
        PhotoBoothViewModel vm = new(cam, () => new PostcardData("50.08° N, 14.44° E", DateTimeOffset.UnixEpoch, "12°C"));
        vm.OnParkStateChanged(ParkState.Parked);
        return vm;
    }

    [TestMethod]
    public async Task Moving_StopsCamera()
    {
        FakeCamera cam = new();
        PhotoBoothViewModel vm = Parked(cam);
        await vm.StartCommand.ExecuteAsync("video-1");
        Assert.IsTrue(vm.IsPreviewing);

        vm.OnParkStateChanged(ParkState.Moving);

        Assert.AreEqual(1, cam.StopCalls);
        Assert.IsFalse(cam.IsActive);
        Assert.IsFalse(vm.IsPreviewing);
        Assert.AreEqual(CapabilityStatus.RequiresPark, vm.Status);
    }

    [TestMethod]
    public async Task Start_WhileMoving_DoesNotStartCamera()
    {
        FakeCamera cam = new();
        PhotoBoothViewModel vm = new(cam, () => null!);
        vm.OnParkStateChanged(ParkState.Moving);

        await vm.StartCommand.ExecuteAsync("video-1");

        Assert.IsFalse(cam.IsActive);
        Assert.AreEqual(CapabilityStatus.RequiresPark, vm.Status);
    }

    [TestMethod]
    public async Task Snap_StoresJpeg_AndPostcard()
    {
        FakeCamera cam = new();
        PhotoBoothViewModel vm = Parked(cam);
        await vm.StartCommand.ExecuteAsync("video-1");

        await vm.SnapCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(cam.Snap, vm.LastSnapJpeg);
        Assert.AreEqual("12°C", vm.LastPostcard!.TemperatureText);
        Assert.IsTrue(vm.HasPostcard);
        Assert.IsFalse(cam.IsActive, "camera is released once the photo is taken");
    }

    [TestMethod]
    public async Task Start_Denied_SetsStatus()
    {
        FakeCamera cam = new() { StartResult = CapabilityStatus.Denied };
        PhotoBoothViewModel vm = Parked(cam);

        await vm.StartCommand.ExecuteAsync("video-1");

        Assert.AreEqual(CapabilityStatus.Denied, vm.Status);
        Assert.IsFalse(vm.IsPreviewing);
    }
}

[TestClass]
public sealed class CabinSoundViewModelTests
{
    private static async Task<CabinSoundViewModel> Listening(FakeMic mic)
    {
        CabinSoundViewModel vm = new(mic);
        vm.OnParkStateChanged(ParkState.Parked);
        await vm.StartCommand.ExecuteAsync(null);
        return vm;
    }

    [TestMethod]
    public async Task Moving_StopsMic()
    {
        FakeMic mic = new();
        CabinSoundViewModel vm = await Listening(mic);

        vm.OnParkStateChanged(ParkState.Moving);

        Assert.AreEqual(1, mic.StopCalls);
        Assert.IsFalse(vm.IsListening);
        Assert.AreEqual(CapabilityStatus.RequiresPark, vm.Status);
    }

    [TestMethod]
    public async Task Challenge_RecordsPeakOver5s()
    {
        FakeMic mic = new();
        CabinSoundViewModel vm = await Listening(mic);
        foreach (float level in new[] { -40f, -10f, -20f, -30f, -35f, -5f })
        {
            mic.Levels.Enqueue(level);
        }

        vm.StartChallengeCommand.Execute(null);
        for (int i = 0; i < 5; i++)
        {
            vm.Tick(TimeSpan.FromSeconds(1));
        }

        Assert.IsFalse(vm.IsChallengeRunning);
        Assert.AreEqual(-10f, vm.ChallengeScoreDb);
        Assert.AreEqual(-10f, vm.BestScoreDb);

        vm.Tick(TimeSpan.FromSeconds(1));
        Assert.AreEqual(-10f, vm.ChallengeScoreDb, "levels after the window don't count");
    }

    [TestMethod]
    public async Task Tick_FillsBands()
    {
        CabinSoundViewModel vm = await Listening(new FakeMic());
        vm.Tick(TimeSpan.FromMilliseconds(16));

        Assert.HasCount(32, vm.Bands);
        Assert.AreEqual(0.5f, vm.Bands[0]);
    }
}

[TestClass]
public sealed class ArcadeViewModelTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(100);

    [TestMethod]
    public void GamepadDisconnect_Pauses()
    {
        ArcadeViewModel vm = new(seed => new ChargeRunGame(seed), new FakeStore());
        vm.Tick(Frame, new GameInput(0, true), gamepadConnected: true);
        vm.Tick(Frame, new GameInput(0, false), gamepadConnected: true);
        double battery = vm.Battery;

        vm.Tick(Frame, new GameInput(0, false), gamepadConnected: false);
        vm.Tick(Frame, new GameInput(0, false), gamepadConnected: false);

        Assert.IsTrue(vm.IsPaused);
        Assert.AreEqual(battery, vm.Battery, 0.01);
    }

    [TestMethod]
    public void Moving_Pauses()
    {
        ArcadeViewModel vm = new(seed => new ChargeRunGame(seed), new FakeStore());
        vm.Tick(Frame, new GameInput(0, true), gamepadConnected: false);

        vm.OnParkStateChanged(ParkState.Moving);

        Assert.IsTrue(vm.IsPaused);
    }

    [TestMethod]
    public void HighScore_Persists()
    {
        FakeStore store = new();
        ArcadeViewModel vm = new(seed => new ChargeRunGame(seed), store);
        vm.Tick(Frame, new GameInput(0, true), false);
        for (int i = 0; i < 400 && !vm.IsOver; i++)
        {
            vm.Tick(Frame, new GameInput(0, false), false);
        }

        Assert.IsTrue(vm.IsOver);
        Assert.IsGreaterThan(0, vm.HighScore);
        Assert.AreEqual(vm.HighScore.ToString(System.Globalization.CultureInfo.InvariantCulture), store.Values[ArcadeViewModel.HighScoreKey]);

        ArcadeViewModel reloaded = new(seed => new ChargeRunGame(seed), store);
        Assert.AreEqual(vm.HighScore, reloaded.HighScore);
    }
}

[TestClass]
public sealed class SensorLabViewModelTests
{
    [TestMethod]
    public async Task Refresh_LoadsReports_AndUpdateReplaces()
    {
        SensorLabViewModel vm = new(new FakeProbe());
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.HasCount(2, vm.Reports);

        vm.Update("geolocation", CapabilityStatus.Active, "±5 m");

        Assert.HasCount(2, vm.Reports);
        Assert.AreEqual(CapabilityStatus.Active, vm.Reports[0].Status);
        Assert.AreEqual("±5 m", vm.Reports[0].Detail);
    }
}
