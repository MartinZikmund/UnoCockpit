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
    public async Task LeavingPhotoOrSound_StopsTheirStreams()
    {
        FakeCamera cam = new();
        FakeMic mic = new();
        ShellViewModel vm = Create(cam, mic);
        vm.OnLocation(LocationResult.Failed(LocationStatus.Denied), isDemo: false);

        vm.OpenAppCommand.Execute(AppId.Photo);
        await vm.Photo.StartCommand.ExecuteAsync("video-1");
        vm.OpenAppCommand.Execute(AppId.Sound);
        Assert.IsFalse(cam.IsActive);

        await vm.Sound.StartCommand.ExecuteAsync(null);
        vm.OpenAppCommand.Execute(AppId.Trip);
        Assert.IsFalse(mic.IsActive);
    }

    [TestMethod]
    public async Task DemoDriveMoving_DoesNotLockRealApps()
    {
        FakeCamera cam = new();
        ShellViewModel vm = Create(cam);
        vm.OpenAppCommand.Execute(AppId.Photo);
        vm.OnClock(T0);
        vm.OnLocation(Fix(T0, 0), isDemo: true);
        await vm.Photo.StartCommand.ExecuteAsync("video-1");

        vm.OnLocation(Fix(T0.AddSeconds(1), 25), isDemo: true);
        vm.OnLocation(Fix(T0.AddSeconds(2), 25), isDemo: true);

        Assert.AreEqual(ParkState.Moving, vm.ParkState, "the gear display still follows the demo drive");
        Assert.IsFalse(vm.IsActiveAppLocked);
        Assert.IsTrue(cam.IsActive);
    }

    [TestMethod]
    public void SwitchingDemoToRealGps_StartsNewTrip()
    {
        ShellViewModel vm = Create();
        for (int i = 0; i < 10; i++)
        {
            vm.OnLocation(Fix(T0.AddSeconds(i), 20, 37.7694 + i * 0.0002, -122.4862), isDemo: true);
        }

        vm.OnLocation(Fix(T0.AddSeconds(30), 0), isDemo: false);

        Assert.AreEqual("0 m", vm.Trip.DistanceText);
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
    public void RepeatedFixTimestamps_StillPark()
    {
        ShellViewModel vm = Create();
        for (int i = 0; i <= 6; i++)
        {
            vm.OnClock(T0.AddSeconds(i));
            vm.OnLocation(Fix(T0, 0), isDemo: false);
        }

        Assert.AreEqual(ParkState.Parked, vm.ParkState);
    }

    [TestMethod]
    public void NullSpeedJitter_Parks()
    {
        ShellViewModel vm = Create();
        Random rnd = new(3);
        for (int i = 0; i < 20; i++)
        {
            double a = rnd.NextDouble() * Math.PI * 2;
            double r = rnd.NextDouble() * 5 / 111_195.0;
            vm.OnClock(T0.AddSeconds(i));
            vm.OnLocation(new LocationResult(LocationStatus.Ok, new GeoSample(T0.AddSeconds(i), 50.0755 + Math.Sin(a) * r, 14.4378 + Math.Cos(a) * r, null, 5, null, null)), isDemo: false);
        }

        Assert.AreEqual(ParkState.Parked, vm.ParkState);
    }

    [TestMethod]
    public void TransientTimeout_WhileDriving_StaysMoving()
    {
        ShellViewModel vm = Create();
        vm.OnClock(T0);
        vm.OnLocation(Fix(T0, 25), isDemo: false);
        vm.OnClock(T0.AddSeconds(1));
        vm.OnLocation(Fix(T0.AddSeconds(1), 25), isDemo: false);

        vm.OnLocation(LocationResult.Failed(LocationStatus.Timeout), isDemo: false);

        Assert.AreEqual(ParkState.Moving, vm.ParkState);
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
    public void DemoBanner_ShowsForAutoDemo_UntilDismissed()
    {
        ShellViewModel vm = Create();
        vm.OnLocation(Fix(T0, 0), isDemo: true, isAutoDemo: false);
        Assert.IsFalse(vm.IsDemoBannerVisible);

        vm.OnLocation(Fix(T0, 0), isDemo: true, isAutoDemo: true);
        Assert.IsTrue(vm.IsDemoBannerVisible);

        vm.DismissDemoBannerCommand.Execute(null);
        Assert.IsFalse(vm.IsDemoBannerVisible);
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
    public async Task PromptAnsweredAfterDrivingOff_StopsCamera()
    {
        FakeCamera cam = new() { Prompt = new() };
        PhotoBoothViewModel vm = Parked(cam);
        Task start = vm.StartCommand.ExecuteAsync("video-1");

        vm.OnParkStateChanged(ParkState.Moving);
        cam.Prompt.SetResult();
        await start;

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
    public async Task PromptAnsweredAfterDrivingOff_StopsMic()
    {
        FakeMic mic = new() { Prompt = new() };
        CabinSoundViewModel vm = new(mic);
        vm.OnParkStateChanged(ParkState.Parked);
        Task start = vm.StartCommand.ExecuteAsync(null);

        vm.OnParkStateChanged(ParkState.Moving);
        mic.Prompt.SetResult();
        await start;

        Assert.IsFalse(mic.IsActive);
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
        ArcadeViewModel vm = new(_ => new ChargeRunGame(1), store);
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
