# Uno Cockpit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an Uno Platform 7.0 WebAssembly app with a car-touchscreen look. It runs in the Tesla browser, uses every sensor the browser exposes, and is deployed to GitHub Pages so people can open it in their car.

**Architecture:** An Uno single-project app (`browserwasm` + `desktop`, Skia) over a plain `net10.0` library, `UnoCockpit.Core`. Core holds all logic, the sensor interfaces, the Demo sources and the view models (CommunityToolkit.Mvvm, no WinUI types), so all of it can be unit tested. The app holds the XAML, the Skia drawing (`SKCanvasElement`), and browser sensor implementations that use `[JSImport]` into one readable module, `wwwroot/js/cockpit.js`. A `SensorHub` in the app polls the sources on dispatcher timers and pushes samples into the view models, which stay passive.

**Tech Stack:** Uno.Sdk 7.0.22, .NET 10, CommunityToolkit.Mvvm, SkiaSharp via `SKCanvasElement`, `System.Runtime.InteropServices.JavaScript`, MSTest, GitHub Actions + GitHub Pages, Playwright (MCP) for smoke tests and screenshots.

**Spec:** `docs/superpowers/specs/2026-11-15-uno-cockpit-design.md`. Read it first; every task's requirements include it.

## Global Constraints

- `global.json` pins `"Uno.Sdk": "7.0.22"`. The installed templates are 7.1.0-dev.18, so re-pin after scaffolding.
- App TFMs: exactly `net10.0-browserwasm;net10.0-desktop`. Core and tests: `net10.0`.
- No "Tesla" in any type, resource key, namespace or asset name, and no Tesla logo, wordmark, model render or Gotham font. "Tesla" appears only descriptively in README/docs.
- No 0–60 timer, lap timer, speed challenge or max-speed display.
- Arcade, Photo Booth and Cabin Sound are locked unless `ParkState.Parked`. On `Moving`, camera and mic are released.
- Parked = speed < 2 km/h (0.556 m/s) sustained 5 s. Moving = speed > 5 km/h (1.389 m/s).
- Never call `navigator.geolocation.watchPosition` or Uno `Geolocator.RequestAccessAsync` on the browser path. Poll `getCurrentPosition` / `GetGeopositionAsync` at 1 Hz.
- Coordinates sent to any third party are rounded to 2 decimals. No analytics.
- Minimum touch target 64×64. 4 px grid. Inter font (OFL). Never `FontWeight=Bold`; use SemiBold or lighter.
- `{ThemeResource}` brushes with `Light`/`Dark`/`HighContrast` dictionaries. `x:Bind` with functions, no `IValueConverter`. `AutomationProperties.Name` + `AutomationId` on icon-only buttons.
- Pages URL `https://martinzikmund.github.io/UnoCockpit/`, built with `-p:WasmShellWebAppBasePath=/UnoCockpit/`. Ship `.nojekyll` (otherwise `_framework/` is dropped).
- Load budget: ≤ 12 MB transferred and interactive in ≤ 15 s on a Ryzen car.
- C# style: file-scoped namespaces, nullable enabled, explicit type + target-typed `new()`, braces always, expression bodies for one-liners. Conventional Commits. Commit messages end with the session's `Co-Authored-By` / `Claude-Session` lines.
- Builds: if `dotnet build` hangs, rerun with `MSBUILDDISABLENODEREUSE=1`. Never kill dotnet processes globally.

## Review Focus

1. **GPS denied, unsupported or timing out** (the TMC reports say this happens). The user expects Demo mode to turn on automatically with a visible banner, not a frozen HUD. Pinned by `SourceSelectorTests` (Task 9).
2. **`coords.speed` is null and there's jitter while parked.** Chromium often reports null speed, and a parked car drifts 3–15 m. The user expects speed derived from positions, an odometer that doesn't creep, and no Parked↔Moving flapping. Pinned by `TripTrackerTests.ParkedJitter_DoesNotAccumulate`, `TripTrackerTests.NullSpeed_IsDerived` and `ParkDetectorTests.SingleJitterSpike_StaysParked` (Tasks 3, 5).
3. **Heading wraps 359° → 1°.** The user expects a tiny lateral G, not a huge spike. Pinned by `GForceEstimatorTests.HeadingWrap_IsSmall` (Task 4).
4. **Car starts moving while camera or mic is live.** The user expects the streams to stop immediately and the overlay to show. Pinned by `PhotoBoothViewModelTests.Moving_StopsCamera` and `CabinSoundViewModelTests.Moving_StopsMic` (Task 9).
5. **Polar day or night** (Norway is a big Tesla market) **and longitudes near ±180.** The user expects a correct day/night theme with no exception. Pinned by `SunCalculatorTests.Tromso_June_IsPolarDay`, `Tromso_December_IsPolarNight` and `Auckland_DateLine_Works` (Task 6).

---

## File Structure

```
UnoCockpit.sln
global.json                       Uno.Sdk 7.0.22
Directory.Build.props             Nullable, LangVersion, TreatWarningsAsErrors=false
LICENSE  THIRD-PARTY-NOTICES.md  README.md  .nojekyll (copied into wwwroot at publish)
.github/workflows/ci.yml          build + Core tests on PR/push
.github/workflows/pages.yml       publish wasm → Pages on push to main
.github/ISSUE_TEMPLATE/car-report.yml
UnoCockpit.Core/
  Geo/GeoSample.cs  Geo/GeoMath.cs  Geo/TripTracker.cs
  Motion/GForceEstimator.cs  Motion/ParkDetector.cs
  Sun/SunCalculator.cs
  Demo/DemoTrack.cs  Demo/DemoLocationSource.cs  Demo/Simulated*.cs
  Game/ChargeRunGame.cs  Game/GameInput.cs
  Sensors/I*Source.cs  Sensors/CapabilityReport.cs  Sensors/SourceSelector.cs
  Weather/OpenMeteoWeatherSource.cs
  Formatting/UnitsFormatter.cs
  ViewModels/{Shell,Trip,Arcade,PhotoBooth,CabinSound,SensorLab}ViewModel.cs
UnoCockpit.Core.Tests/            mirrors Core folders, *Tests.cs
UnoCockpit/                       Uno app (template output, trimmed)
  App.xaml(.cs)  ShellPage.xaml(.cs)
  Themes/Colors.xaml  Themes/Typography.xaml  Themes/Controls.xaml
  Assets/Fonts/InterVariable.ttf  Assets/demo-track.json
  Services/SensorHub.cs  Services/SourceFactory.cs  Services/ThemeController.cs
  Sensors/Browser/{Interop,BrowserLocationSource,UnoGeolocatorSource,BrowserGamepadSource,BrowserCameraSource,BrowserMicSource,BrowserMotionSource,BrowserCapabilityProbe,BrowserAudioOut}.cs
  Views/StatusBar.xaml  Views/Dock.xaml  Views/ParkToPlayOverlay.xaml
  Views/Drawing/{CarCanvas,CompassRose,FrictionCircle,BreadcrumbCanvas,ArcadeCanvas,SpectrumCanvas}.cs
  Views/Apps/{TripHudView,ArcadeView,PhotoBoothView,CabinSoundView,SensorLabView}.xaml
  Views/CameraHost.cs             BrowserHtmlElement("video") host
  Spike/SpikePage.xaml(.cs)       Phase 0 only, opened via ?spike
  wwwroot/js/cockpit.js
```

---

### Task 1: Scaffold, CI, GitHub repo and Pages deploy

**Files:**
- Create: everything under the root listed above except Core logic, views and JS (template output plus `UnoCockpit.Core`, `UnoCockpit.Core.Tests`, workflows, LICENSE, `global.json`).

**Interfaces:**
- Produces: buildable `UnoCockpit.sln`; `UnoCockpit` references `UnoCockpit.Core`; the test project references Core; Pages URL live.

- [ ] **Step 1: Generate the app** in `D:\Personal\UnoCockpit`:
  `dotnet new unoapp -n UnoCockpit -o . -preset blank -tfm net10.0 -presentation mvvm -markup xaml -theme fluent -platforms wasm desktop -renderer skia -tests none -vscode --skip-restore`
  Then set `global.json` to `"Uno.Sdk": "7.0.22"`. Delete the template's sample `MainPage` content but keep the file as `ShellPage` (rename class and file). Keep `UnoFeatures` to `Mvvm` only (Skia is implied and brings `SKCanvasElement`).
- [ ] **Step 2: Add projects:** `dotnet new classlib -n UnoCockpit.Core -f net10.0` (+ `CommunityToolkit.Mvvm` package), `dotnet new mstest -n UnoCockpit.Core.Tests -f net10.0` (reference Core). Add both to the sln. Add a project reference from the app to Core. Add `Directory.Build.props` with `<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>`.
- [ ] **Step 3: Verify the builds:**
  `dotnet build UnoCockpit/UnoCockpit.csproj -f net10.0-browserwasm` → `Build succeeded`; `dotnet test UnoCockpit.Core.Tests` → template test passes.
- [ ] **Step 4: Add workflows.** `ci.yml`: on push/PR, setup-dotnet `10.0.x`, `dotnet test UnoCockpit.Core.Tests`, `dotnet build UnoCockpit/UnoCockpit.csproj -f net10.0-browserwasm -c Release`. `pages.yml`: on push to `main` + `workflow_dispatch`, `dotnet publish UnoCockpit/UnoCockpit.csproj -f net10.0-browserwasm -c Release -p:WasmShellWebAppBasePath=/UnoCockpit/ -o out`, `touch` `.nojekyll` into the publish `wwwroot`, `actions/upload-pages-artifact` from that `wwwroot`, `actions/deploy-pages`. Add the MIT `LICENSE` (Martin Zikmund, 2026) and a stub README.
- [ ] **Step 5: Commit**, then create the repo and push:
  `gh repo create MartinZikmund/UnoCockpit --public --source . --push --description "Uno Platform 7.0 WebAssembly cockpit for in-car browsers"`
  Then enable Pages: `gh api -X POST repos/MartinZikmund/UnoCockpit/pages -f build_type=workflow`.
- [ ] **Step 6: Verify the deploy:** `gh run watch` on the `pages` run → success. Then open `https://martinzikmund.github.io/UnoCockpit/` in Playwright; the blank shell renders and there are no 404s in network requests.

### Task 2: Phase 0 spike, the car go/no-go page

**Files:**
- Create: `UnoCockpit/Spike/SpikePage.xaml(.cs)`, `UnoCockpit/wwwroot/js/cockpit.js` (first sections: `geo`, `media`, `probe`), `UnoCockpit/Sensors/Browser/Interop.cs`
- Modify: `UnoCockpit/App.xaml.cs` (navigate to `SpikePage` when the URL query contains `spike`; read it via a `[JSImport("globalThis.location.search")]`-style getter)

**Interfaces:**
- Produces: `Interop` static partial class with `[JSImport]` methods reused by later tasks:
  `Task<string> GeoGetCurrentJson(int timeoutMs)` (resolves to JSON `{ok,lat,lon,alt,acc,speed,heading,ts}` or `{ok:false,code}`),
  `Task<string> MediaStartCamera(string videoElementId)` (resolves to `"ok"|"denied"|"unsupported"|"error:<msg>"`), `void MediaStopCamera()`,
  `string ProbeJson()`.
  Module load: `await JSHost.ImportAsync("cockpit", "./js/cockpit.js")` resolved against `document.baseURI`. Verify it works under the `/UnoCockpit/` base path.

- [ ] **Step 1: Build `SpikePage`** with four live panels:
  (a) a `SKCanvasElement` spinning gradient ring redrawn every frame, with an FPS counter;
  (b) GPS, two columns: Uno `Geolocator.GetGeopositionAsync()` vs `Interop.GeoGetCurrentJson`, each polled at 1 Hz and showing lat/lon/speed/heading/accuracy or the error;
  (c) Gamepad: `Windows.Gaming.Input.Gamepad.Gamepads` count + `GetCurrentReading()` sticks/buttons;
  (d) a "Start camera" button feeding a `BrowserHtmlElement.CreateHtmlElement("video")` hosted in a `ContentControl` (`InputPolicy` default), with a tap counter on a XAML button overlapping its corner to check layering and input.
  Also show: `ProbeJson()` raw, `navigator.userAgent`, the WebGL renderer string, and the time from navigation start to first frame.
- [ ] **Step 2: Verify locally:** `dotnet run --project UnoCockpit -f net10.0-browserwasm`. Then in Playwright with geolocation granted and fake media flags, open `/?spike`. Expected: the ring animates, both GPS columns show the emulated position, the camera shows Chromium's fake pattern, and the overlapping button counts taps.
- [ ] **Step 3: Measure.** Run a Playwright cold load of the deployed `/?spike`: total transferred bytes and time to first frame. Record both in `docs/measurements.md`.
- [ ] **Step 4: Commit + push**, wait for the Pages deploy, then **hand off to Martin**: send the URL `https://martinzikmund.github.io/UnoCockpit/?spike` and a checklist (ring smooth? GPS columns? gamepad? camera parked? button over video? load time?). **Gate:** Tasks 3–9 (pure Core) continue while waiting. Tasks 10+ start only after Martin's report. Act on the result: if Uno `Geolocator` fails and interop works, `BrowserLocationSource` (interop) becomes the default. If `<video>` embedding fails, switch Photo Booth to the frame-copy fallback in the spec. Record any Uno bug in `C:\Users\Martin\.claude\uno-issues\`.

### Task 3: Geo math and trip tracking

**Files:**
- Create: `UnoCockpit.Core/Geo/GeoSample.cs`, `GeoMath.cs`, `TripTracker.cs`
- Test: `UnoCockpit.Core.Tests/Geo/GeoMathTests.cs`, `TripTrackerTests.cs`

**Interfaces:**
- Produces:
  `public sealed record GeoSample(DateTimeOffset Timestamp, double Latitude, double Longitude, double? AltitudeM, double AccuracyM, double? SpeedMps, double? HeadingDeg);`
  `GeoMath.DistanceMeters(double lat1, double lon1, double lat2, double lon2) -> double` (haversine, R = 6 371 008.8 m);
  `GeoMath.BearingDeg(...) -> double` [0,360);
  `GeoMath.HeadingDeltaDeg(double fromDeg, double toDeg) -> double` in [-180,180).
  `TripTracker`: `void Add(GeoSample s)`; `double DistanceM`; `TimeSpan Elapsed`; `double AverageSpeedMps`; `double? CurrentSpeedMps`; `double? CurrentHeadingDeg`; `IReadOnlyList<(double EastM, double NorthM)> Breadcrumbs` (local ENU relative to the first fix, max 2 000 points, decimate by dropping every other point when full); `void Reset()`.

- [ ] **Step 1: Write the failing tests:**
  - `Distance_PragueToBrno_IsAbout185km`: (50.0755, 14.4378)→(49.1951, 16.6068), expect 184 600 ± 1 000.
  - `HeadingDelta_Wraps`: (350→10) == 20, (10→350) == -20, (0→180) == -180.
  - `NullSpeed_IsDerived`: two fixes 10 m apart, 1 s apart, `SpeedMps=null` → `CurrentSpeedMps` ≈ 10 ± 0.5.
  - `ParkedJitter_DoesNotAccumulate`: 60 fixes 1 s apart randomly within 8 m of one point, accuracy 10 m, speed 0 → `DistanceM` < 1.
  - `PoorAccuracy_IsIgnoredForDistance`: a fix with accuracy 80 m adds no distance.
  - `Drive_AccumulatesDistance`: a 1 km straight line in 50 fixes at 20 m/s → `DistanceM` 1 000 ± 20, `AverageSpeedMps` ≈ 20.
- [ ] **Step 2: Run:** `dotnet test UnoCockpit.Core.Tests --filter FullyQualifiedName~Geo` → FAIL (types missing).
- [ ] **Step 3: Implement.** Distance only accumulates when accuracy ≤ 50 m AND (displacement > max(accuracy, 5 m) since the last accepted point OR effective speed ≥ 1 m/s). Effective speed = `SpeedMps ?? derived`.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat(core): Add geo math and trip tracker`.

### Task 4: G-force estimator

**Files:**
- Create: `UnoCockpit.Core/Motion/GForceEstimator.cs`
- Test: `UnoCockpit.Core.Tests/Motion/GForceEstimatorTests.cs`

**Interfaces:**
- Produces: `public readonly record struct GForce(double LongitudinalG, double LateralG);` and `GForceEstimator`: `GForce Add(DateTimeOffset t, double speedMps, double? headingDeg)`; `GForce Current`; `void AddMeasured(double longitudinalMps2, double lateralMps2)` (real `DeviceMotion` override; when called within the last 2 s, GPS-derived values are ignored). g = 9.80665. EMA α = 0.3. Heading ignored below 3 m/s. Output clamped to ±1.5 g.

- [ ] **Step 1: Failing tests:**
  - `Accelerating_0to10mps_in1s_IsAbout1g`: (t0, 0), (t0+1 s, 9.80665) → after 5 identical-rate samples, `LongitudinalG` ≈ 1 ± 0.1.
  - `ConstantTurn_LateralMatchesVTimesOmega`: 20 m/s, heading +9°/s → lateral ≈ 20·(9π/180)/g ≈ 0.32 ± 0.03.
  - `HeadingWrap_IsSmall`: heading 359 → 1 in 1 s at 20 m/s → |LateralG| < 0.1.
  - `LowSpeed_IgnoresHeading`: 2 m/s, heading jumps 90° → LateralG == 0.
  - `Measured_OverridesDerived`.
- [ ] **Step 2: Run** `--filter FullyQualifiedName~GForce` → FAIL.
- [ ] **Step 3: Implement** with `GeoMath.HeadingDeltaDeg` for the turn rate.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat(core): Add G-force estimator`.

### Task 5: Park detector

**Files:**
- Create: `UnoCockpit.Core/Motion/ParkDetector.cs`
- Test: `UnoCockpit.Core.Tests/Motion/ParkDetectorTests.cs`

**Interfaces:**
- Produces: `public enum ParkState { Unknown, Parked, Moving }`; `ParkDetector`: `ParkState State`; `event EventHandler<ParkState>? StateChanged`; `void Update(DateTimeOffset t, double? speedMps)`; `void ForceState(ParkState s)` (used when there's no location).

- [ ] **Step 1: Failing tests:** `StartsUnknown`; `Slow5s_BecomesParked` (0.3 m/s for 5 s → Parked, not before 5 s); `Fast_BecomesMovingImmediately` (2.0 m/s → Moving on the first sample); `BetweenThresholds_KeepsState` (1.0 m/s doesn't change Parked or Moving); `SingleJitterSpike_StaysParked` (Parked, then one 1.2 m/s sample → still Parked); `NullSpeed_KeepsState`; `StateChanged_RaisedOncePerTransition`.
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement.** **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat(core): Add park detector`.

### Task 6: Sun calculator (auto day/night)

**Files:**
- Create: `UnoCockpit.Core/Sun/SunCalculator.cs`
- Test: `UnoCockpit.Core.Tests/Sun/SunCalculatorTests.cs`

**Interfaces:**
- Produces: `public enum PolarCondition { None, PolarDay, PolarNight }`; `public sealed record SunTimes(DateTimeOffset? SunriseUtc, DateTimeOffset? SunsetUtc, PolarCondition Polar);`; `static SunTimes SunCalculator.GetSunTimes(DateOnly date, double lat, double lon)`; `static bool SunCalculator.IsDaylight(DateTimeOffset utcNow, double lat, double lon)`. Use the NOAA solar algorithm with zenith 90.833°.

- [ ] **Step 1: Failing tests** (tolerance ±3 min):
  - `London_Solstice`: 2024-06-21, (51.5074, -0.1278) → sunrise 03:43 UTC, sunset 20:21 UTC.
  - `Tromso_June_IsPolarDay`: 2026-06-21, (69.6492, 18.9553) → `PolarDay`, both times null; `IsDaylight` true at 00:00 UTC.
  - `Tromso_December_IsPolarNight`: 2026-12-21 → `PolarNight`; `IsDaylight` false at 11:00 UTC.
  - `Auckland_DateLine_Works`: 2026-01-15, (-36.8485, 174.7633) → no exception; `IsDaylight` true at 2026-01-15T00:00Z (13:00 local) and false at 2026-01-15T12:00Z (01:00 local).
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement.** `IsDaylight` must check the neighbouring UTC dates too, so date-line cases work. **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat(core): Add NOAA sun calculator`.

### Task 7: Demo track and simulated sources

**Files:**
- Create: `UnoCockpit.Core/Demo/DemoTrack.cs`, `DemoLocationSource.cs`, `SimulatedGamepadSource.cs`; `UnoCockpit.Core/Sensors/ILocationSource.cs`, `IGamepadSource.cs`, `ICameraSource.cs`, `IMicSource.cs`, `IMotionSource.cs`, `ICapabilityProbe.cs`, `CapabilityReport.cs`; `UnoCockpit/Assets/demo-track.json`
- Test: `UnoCockpit.Core.Tests/Demo/DemoTrackTests.cs`

**Interfaces:**
- Produces (Sensors):
  `public enum LocationStatus { Ok, Denied, Unavailable, Timeout, Unsupported }`; `public sealed record LocationResult(LocationStatus Status, GeoSample? Sample);`;
  `public interface ILocationSource { bool IsDemo { get; } Task<LocationResult> GetAsync(CancellationToken ct); }`;
  `public readonly record struct GamepadState(bool Connected, double X, double Y, bool A, bool B, bool Start);`; `public interface IGamepadSource { GamepadState Read(); }`;
  `public enum CapabilityStatus { Available, Active, Denied, Unsupported, RequiresPark, Unknown }`;
  `public interface ICameraSource { bool IsActive { get; } Task<CapabilityStatus> StartAsync(string videoElementId); void Stop(); Task<byte[]?> SnapJpegAsync(); }`;
  `public interface IMicSource { bool IsActive { get; } Task<CapabilityStatus> StartAsync(); void Stop(); bool TryReadBands(Span<float> bands); float ReadLevelDb(); }` (32 bands, 0..1);
  `public readonly record struct MotionSample(double LongitudinalMps2, double LateralMps2);`; `public interface IMotionSource { bool IsSupported { get; } MotionSample? Latest { get; } }`;
  `public sealed record CapabilityReport(string Id, string Name, CapabilityStatus Status, string Detail);`; `public interface ICapabilityProbe { Task<IReadOnlyList<CapabilityReport>> ProbeAsync(); }`.
- Produces (Demo): `DemoTrack.Load(Stream json) -> DemoTrack`; `TimeSpan Duration`; `GeoSample SampleAt(TimeSpan t, DateTimeOffset wallClock)` (linear interpolation of lat/lon/alt/speed, shortest-arc heading, clamps after the end = parked at the last point). `DemoLocationSource(DemoTrack track, TimeProvider time)` implements `ILocationSource` (`IsDemo=true`), elapsed since construction, looping after `Duration + 60 s` of parking. JSON: `{"points":[{"t":0,"lat":..,"lon":..,"alt":..,"speed":..,"heading":..}]}`.

- [ ] **Step 1: Failing tests:** `SampleAt_Midpoint_Interpolates`; `SampleAt_HeadingShortestArc` (350→10 midpoint == 0); `SampleAt_AfterEnd_IsParked` (speed 0, last position); `DemoSource_UsesTimeProvider` (FakeTimeProvider from `Microsoft.Extensions.TimeProvider.Testing`, advance 30 s → matches `SampleAt(30 s)`); `BundledTrack_IsValid` (load `UnoCockpit/Assets/demo-track.json` via a linked test file: duration 180–300 s, ends with speed 0 for ≥ 10 s, max speed ≤ 30 m/s).
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement**, and generate `demo-track.json` with a small script (kept in `tools/make-demo-track.csx`, not shipped). Use a synthesized ~4 min loop: accelerate, cruise, two bends, a roundabout, decelerate, then 20 s parked. Place it at a neutral public spot (e.g. around a large public park), not anyone's home area.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat(core): Add sensor contracts and demo track`.

### Task 8: Charge Run game rules

**Files:**
- Create: `UnoCockpit.Core/Game/ChargeRunGame.cs`, `GameInput.cs`
- Test: `UnoCockpit.Core.Tests/Game/ChargeRunGameTests.cs`

**Interfaces:**
- Produces: `public readonly record struct GameInput(double Steer, bool Start);` (Steer −1..1); `public enum EntityKind { Bolt, Cone }`; `public readonly record struct Entity(EntityKind Kind, double X, double Y);` (X 0..1 across the road, Y 0..1 down the screen); `ChargeRunGame(int seed)`: `void Update(TimeSpan dt, GameInput input)`, `double CarX`, `double Battery` (0..100), `int Score`, `bool IsRunning`, `bool IsOver`, `IReadOnlyList<Entity> Entities`, `double SpeedFactor`, `event EventHandler<EntityKind>? Collected`. Constants: car at Y = 0.85, hit radius 0.06, steer speed 1.2 lanes/s, battery drain 3 %/s, bolt +20 (cap 100), cone −25 with 1 s invulnerability, speed factor +2 % every 10 s, score += distance·10 + 50 per bolt.

- [ ] **Step 1: Failing tests:** `NotRunning_UntilStart`; `Battery_Drains3PerSecond` (1 s → 97 ± 0.1); `Bolt_Adds20_Capped`; `Cone_Subtracts25_ThenInvulnerable1s`; `BatteryZero_IsOver`; `Steer_ClampsToRoad` (CarX stays in [0.08, 0.92]); `SameSeed_SameEntities` (deterministic).
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement.** Put entities in tests through an `internal` `Spawn(Entity e)` with `InternalsVisibleTo` the test project. **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat(core): Add Charge Run game rules`.

### Task 9: View models, source selection, weather, units

**Files:**
- Create: `UnoCockpit.Core/Sensors/SourceSelector.cs`, `Weather/OpenMeteoWeatherSource.cs`, `Formatting/UnitsFormatter.cs`, `ViewModels/ShellViewModel.cs`, `TripViewModel.cs`, `ArcadeViewModel.cs`, `PhotoBoothViewModel.cs`, `CabinSoundViewModel.cs`, `SensorLabViewModel.cs`
- Test: matching `*Tests.cs` under `UnoCockpit.Core.Tests/`

**Interfaces:**
- Produces:
  - `public enum AppId { Trip, Arcade, Photo, Sound, Lab }`.
  - `SourceSelector(ILocationSource primary, ILocationSource demo)`: `Task<LocationResult> GetAsync(CancellationToken)`; `bool IsDemo`; `bool ForceDemo { get; set; }`. It switches to demo after 3 consecutive `Denied`/`Unsupported` results, or after 10 consecutive `Timeout`/`Unavailable` with no `Ok` ever. It switches back when `ForceDemo` is false and the primary returns `Ok` (probed every 30 s).
  - `OpenMeteoWeatherSource(HttpClient http)`: `Task<double?> GetTemperatureCAsync(double lat, double lon, CancellationToken ct)` → GET `https://api.open-meteo.com/v1/forecast?latitude={lat:F2}&longitude={lon:F2}&current=temperature_2m` (invariant culture). Returns null on any failure.
  - `UnitsFormatter(bool metric)`: `string Speed(double? mps)` ("—" when null, km/h or mph rounded), `string Distance(double m)`, `string Temperature(double? c)` (null → ""), `static bool DefaultMetric(RegionInfo r)`.
  - `ShellViewModel` (`ObservableObject`): `AppId ActiveApp`, `ParkState ParkState`, `bool IsDemo`, `bool IsDark`, `bool? ThemeOverrideDark`, `string ClockText`, `string TemperatureText`, `bool HasTemperature`, `bool HasGpsFix`, `bool IsOnline`, `bool IsActiveAppLocked` (= active app ∈ {Arcade, Photo, Sound} && ParkState != Parked), `IRelayCommand<AppId> OpenAppCommand`. Methods called by `SensorHub`: `void OnLocation(LocationResult r, bool isDemo)`, `void OnClock(DateTimeOffset now)`, `void OnTemperature(double? c)`, `void OnOnline(bool online)`. It owns `TripTracker`, `ParkDetector`, `GForceEstimator` and exposes `TripViewModel Trip`, `ArcadeViewModel Arcade`, `PhotoBoothViewModel Photo`, `CabinSoundViewModel Sound`, `SensorLabViewModel Lab`, wiring `ParkDetector.StateChanged` → `Photo.OnParkStateChanged`, `Sound.OnParkStateChanged`, `Arcade.OnParkStateChanged`. When `OnLocation` arrives with no fix (demo off + not Ok), `ParkDetector.ForceState(Parked)`. IsDark = override ?? !SunCalculator.IsDaylight(now, lastFix), defaulting to dark with no fix.
  - `TripViewModel`: `string SpeedText`, `string UnitLabel`, `double HeadingDeg`, `string AltitudeText`, `double AccuracyM`, `string DistanceText`, `string ElapsedText`, `string AverageText`, `GForce G`, `IReadOnlyList<(double EastM,double NorthM)> Breadcrumbs`, `bool Metric` (toggle), `void Refresh(TripTracker t, GForce g)`.
  - `ArcadeViewModel(Func<int, ChargeRunGame> factory)`: `ChargeRunGame Game`, `int HighScore` (via an `IKeyValueStore` interface in Core: `string? Get(string k); void Set(string k, string v)`), `void Tick(TimeSpan dt, GameInput input, bool gamepadConnected)` (pauses when it was connected and becomes disconnected), `bool IsPaused`, `void OnParkStateChanged(ParkState s)` (pauses on Moving).
  - `PhotoBoothViewModel(ICameraSource cam, Func<PostcardData> postcardData)`: `CapabilityStatus Status`, `bool IsPreviewing`, `byte[]? LastSnapJpeg`, `PostcardData? LastPostcard`, `IAsyncRelayCommand<string> StartCommand` (videoElementId), `IAsyncRelayCommand SnapCommand`, `IRelayCommand StopCommand`, `void OnParkStateChanged(ParkState s)` (Moving → `cam.Stop()`, `Status=RequiresPark`). `public sealed record PostcardData(string PlaceText, DateTimeOffset LocalTime, string TemperatureText);`.
  - `CabinSoundViewModel(IMicSource mic)`: `float[] Bands` (32), `float LevelDb`, `float PeakDb`, `bool IsChallengeRunning`, `double ChallengeRemainingS`, `float ChallengeScoreDb`, `IAsyncRelayCommand StartCommand`, `IRelayCommand StopCommand`, `IRelayCommand StartChallengeCommand` (5 s window), `void Tick(TimeSpan dt)`, `void OnParkStateChanged(ParkState s)` (Moving → `mic.Stop()`).
  - `SensorLabViewModel(ICapabilityProbe probe)`: `ObservableCollection<CapabilityReport> Reports`, `IAsyncRelayCommand RefreshCommand`, `void Update(string id, CapabilityStatus s, string detail)` (live values from the hub).

- [ ] **Step 1: Failing tests:**
  - `SourceSelectorTests`: `Denied3x_SwitchesToDemo`; `Timeout10x_NoOkEver_SwitchesToDemo`; `TimeoutAfterOk_StaysPrimary`; `ForceDemo_UsesDemo`; `PrimaryRecovers_SwitchesBack` (FakeTimeProvider 30 s).
  - `OpenMeteoWeatherSourceTests`: `RoundsTo2Decimals_InvariantCulture` (fake `HttpMessageHandler` captures the URI; with culture `cs-CZ`, query has `latitude=50.08&longitude=14.44`); `HttpError_ReturnsNull`; parses `{"current":{"temperature_2m":12.3}}` → 12.3.
  - `UnitsFormatterTests`: `Speed_Null_IsDash`; `Speed_Metric` (27.78 m/s → "100"); `Speed_Imperial` (26.82 m/s → "60").
  - `ShellViewModelTests`: `ArcadeLocked_WhenMoving`; `NoFix_ForcesParked`; `IsDark_FollowsSun_UnlessOverridden`.
  - `PhotoBoothViewModelTests`: `Moving_StopsCamera` (fake camera records `Stop()`, `Status==RequiresPark`); `Snap_StoresJpeg_AndPostcard`; `Start_Denied_SetsStatus`.
  - `CabinSoundViewModelTests`: `Moving_StopsMic`; `Challenge_RecordsPeakOver5s` (fake mic levels −40,−10,−20 → score −10; ends at 5 s).
  - `ArcadeViewModelTests`: `GamepadDisconnect_Pauses`; `HighScore_Persists` (fake store).
- [ ] **Step 2: Run** `dotnet test UnoCockpit.Core.Tests` → new tests FAIL.
- [ ] **Step 3: Implement.** Use CommunityToolkit.Mvvm source generators (`[ObservableProperty]` on partial properties, `[RelayCommand]`).
- [ ] **Step 4: Run** → all PASS.
- [ ] **Step 5: Commit** `feat(core): Add view models, source selection and weather`.

### Task 10: Browser sensor layer and SensorHub

**Files:**
- Create: `UnoCockpit/Sensors/Browser/*.cs` (all listed in File Structure), `UnoCockpit/Services/SensorHub.cs`, `SourceFactory.cs`
- Modify: `UnoCockpit/wwwroot/js/cockpit.js` (add sections `mic`, `motion`, `audio`, `fullscreen`, `speech`, `snap`), `Interop.cs`

**Interfaces:**
- Consumes: all Task 7 interfaces, Task 9 view models, Task 2 `Interop`.
- Produces: `SourceFactory.Create() -> Sources` record (`ILocationSource Primary, ILocationSource Demo, IGamepadSource Gamepad, ICameraSource Camera, IMicSource Mic, IMotionSource Motion, ICapabilityProbe Probe, IAudioOut Audio, IKeyValueStore Store`). Browser implementations when `OperatingSystem.IsBrowser()`, else Demo/simulated (desktop: keyboard arrows/space → `IGamepadSource` via `KeyboardGamepadSource` in the app). `public interface IAudioOut { void Play(string cue); }` (cues: `bolt`, `cone`, `start`, `over`, `tap`; synthesized in JS with Web Audio oscillators, no audio files). `SensorHub(ShellViewModel vm, Sources s, DispatcherQueue q)`: `Start()`/`Stop()`; 1 Hz location → `vm.OnLocation`; 1 Hz clock; 10-minute weather (only with a non-demo fix or with demo); network via `NetworkInformation.NetworkStatusChanged`; motion → `GForceEstimator.AddMeasured`; per-frame (`CompositionTarget.Rendering`) gamepad + arcade tick when the Arcade is visible, and mic tick when Cabin Sound is visible.
- Additional `Interop` methods: `Task<string> MediaStartMic()`, `void MediaStopMic()`, `double[] MicBands()` (32 log-spaced bands from an `AnalyserNode` fftSize 2048), `double MicLevelDb()`, `Task<string> SnapJpegBase64(string videoElementId)` (JPEG quality 0.9, max 1280 px wide), `string MotionLatestJson()`, `void AudioPlay(string cue)`, `void FullscreenToggle()`, `bool SpeechSupported()`, `void SpeechStart()` + a `[JSExport] OnSpeechCommand(string text)`.

- [ ] **Step 1: Implement** the sources: `BrowserLocationSource` (interop JSON → `LocationResult`, mapping error codes 1→Denied, 2→Unavailable, 3→Timeout) and `UnoGeolocatorSource` (`GetGeopositionAsync` only), with the default chosen from the Task 2 result. `BrowserGamepadSource` uses `Gamepad.Gamepads.FirstOrDefault()?.GetCurrentReading()` (left stick X or D-pad → `Steer`; A → `A`; Menu → `Start`). The rest wrap interop.
- [ ] **Step 2: Wire** in `App.xaml.cs`: build `Sources`, `ShellViewModel`, and `SensorHub`, set `ShellPage.ViewModel`, and call `hub.Start()`.
- [ ] **Step 3: Verify** with Playwright on `dotnet run -f net10.0-browserwasm` (geolocation granted at 50.0755,14.4378, fake media): the console has no interop errors; after 3 s a temporary debug `TextBlock` bound to `vm.Trip.SpeedText` shows "0". With geolocation **denied**, `vm.IsDemo` flips to true within 3 polls and the speed starts changing (demo track).
- [ ] **Step 4: Commit** `feat: Add browser sensor layer and hub`.

### Task 11: Theme, shell layout, status bar, dock, park overlay, auto theme

**Files:**
- Create: `Themes/Colors.xaml`, `Typography.xaml`, `Controls.xaml`, `Assets/Fonts/InterVariable.ttf`, `Views/StatusBar.xaml`, `Dock.xaml`, `ParkToPlayOverlay.xaml`, `Services/ThemeController.cs`
- Modify: `App.xaml`, `ShellPage.xaml(.cs)`

**Interfaces:**
- Consumes: `ShellViewModel`.
- Produces: brush keys used by all views: `CockpitBackgroundBrush`, `CockpitCardBrush`, `CockpitCardStrokeBrush`, `CockpitTextPrimaryBrush`, `CockpitTextSecondaryBrush`, `CockpitAccentBrush`, `CockpitWarningBrush`, `CockpitAvailableBrush`; text styles `CockpitHeroNumberStyle` (120, Light), `CockpitTitleStyle` (28, SemiBold), `CockpitLabelStyle` (14, SemiBold, secondary), `CockpitValueStyle` (32, Regular); button styles `CockpitDockButtonStyle` (88×72 hit area, icon 32, selected = accent underline 3 px), `CockpitPillButtonStyle` (min 64 high, corner radius 32). Starting values: Dark bg `#111214` / card `#1C1D20`; Light bg `#F4F4F4` / card `#FFFFFF`; accent `#3E6AE1`; warning `#E31937`; available `#2BA84A`. HighContrast maps to `SystemColor*` brushes only.

- [ ] **Step 1: Build `ShellPage`**: rows `48 / * / 96`, content columns `55* / 45*` with 24 px gutters. Status bar: clock left; temperature (hidden when `!HasTemperature`), GPS dot, network icon and "DEMO" pill right. The car panel hosts `CarCanvas` (Task 12, placeholder `Border` for now). The app panel swaps views by `ActiveApp` through `x:Bind` visibility functions (`ShellPage.Is(AppId active, AppId target) -> Visibility`). Dock: five apps plus a fullscreen toggle and a theme override flyout (Auto/Light/Dark). When Demo mode turned on automatically (not via `ForceDemo`), show a dismissible banner under the status bar ("Location unavailable, showing a demo drive"). `ParkToPlayOverlay` covers the app panel when `IsActiveAppLocked` ("Park to play", P icon, calm fade 200 ms).
- [ ] **Step 2: `ThemeController`** sets `((FrameworkElement)window.Content).RequestedTheme` from `vm.IsDark`, and the WASM page background colour via interop `document.body.style.background`.
- [ ] **Step 3: Verify** with Playwright screenshots at 1600×900 and 1920×1200, dark and light (override). Check that touch targets measure ≥ 64 px (`browser_evaluate` on the dock's bounding boxes; Uno renders to canvas, so measure via the accessibility tree / `AutomationId` semantic elements). Save to `docs/screenshots/shell-*.png`.
- [ ] **Step 4: Commit** `feat: Add cockpit theme and shell`.

### Task 12: Car render

**Files:**
- Create: `Views/Drawing/CarCanvas.cs`

**Interfaces:**
- Consumes: `ShellViewModel` (`IsDark`, `ParkState`, `Trip.HeadingDeg`, `Trip.SpeedText`).
- Produces: `CarCanvas : SKCanvasElement` with DPs `IsNight` (bool), `IsParked` (bool), `WheelAngleDeg` (double, −30..30), `SpeedText` (string); it handles `ManipulationDelta` (rotation + scale, `ManipulationMode = Rotate | Scale | TranslateInertia`), and double-tap resets.

- [ ] **Step 1: Draw** an original top-down generic EV silhouette with `SKPath` (body, glass, four wheels that rotate by `WheelAngleDeg`, headlight cones that fade in with `IsNight`, and a "P" glyph when parked). Draw the speed overlay above the car when moving. Derive `WheelAngleDeg` in `ShellPage` from the heading rate (clamped).
- [ ] **Step 2: Verify:** a Playwright screenshot of the parked/dark and moving/light states (demo track); a pinch gesture (two-pointer CDP touch via `browser_run_code_unsafe`) changes the scale.
- [ ] **Step 3: Commit** `feat: Add car render`.

### Task 13: Trip HUD

**Files:**
- Create: `Views/Apps/TripHudView.xaml(.cs)`, `Views/Drawing/CompassRose.cs`, `FrictionCircle.cs`, `BreadcrumbCanvas.cs`

**Interfaces:**
- Consumes: `TripViewModel`.

- [ ] **Step 1: Build** the layout: hero speed + unit label (tap toggles units) top; a row with the compass rose (rotating card, N marker, heading number) and the friction circle (rings at 0.25/0.5/1 g, trailing dot with a 2 s fade trail); a stat row (distance, time, average, altitude, an accuracy chip); the breadcrumb grid below (auto-fit bounds, 100 m grid). There is no max speed anywhere.
- [ ] **Step 2: Verify** with Playwright on the demo track: speed changes, the compass rotates, the breadcrumb draws the loop. Screenshot at 1600×900 to `docs/screenshots/trip.png`.
- [ ] **Step 3: Commit** `feat: Add Trip HUD`.

### Task 14: Arcade

**Files:**
- Create: `Views/Apps/ArcadeView.xaml(.cs)`, `Views/Drawing/ArcadeCanvas.cs`

**Interfaces:**
- Consumes: `ArcadeViewModel`, `IAudioOut` (via `ChargeRunGame.Collected` → `bolt`/`cone`; start/over cues).

- [ ] **Step 1: Build** `ArcadeCanvas` to render the road (neon lane lines scrolling with `SpeedFactor`), car, bolts and cones, and a battery bar. The view has a title/start state ("Press A or tap to start"), a pause state ("Controller disconnected"), a game-over state with the high score, and on-screen ◀ ▶ touch pads (64×64+) when no gamepad is connected.
- [ ] **Step 2: Verify** in Playwright: start by tap, hold the ▶ pad → the car moves right (compare two screenshots), the battery bar drains, and game over appears within about 40 s idle. Screenshot to `docs/screenshots/arcade.png`.
- [ ] **Step 3: Commit** `feat: Add Charge Run arcade`.

### Task 15: Photo Booth

**Files:**
- Create: `Views/Apps/PhotoBoothView.xaml(.cs)`, `Views/CameraHost.cs`

**Interfaces:**
- Consumes: `PhotoBoothViewModel`, `Interop.SnapJpegBase64`.
- Produces: `CameraHost : ContentControl` exposing `string? VideoElementId` (creates `BrowserHtmlElement.CreateHtmlElement("video")` with `autoplay playsinline muted`, `object-fit: cover`, mirrored; disposes on unload). A postcard renderer `PostcardCanvas : SKCanvasElement` draws the JPEG (`SKBitmap.Decode`) plus the HUD frame: rounded inner border, place text, local time, temperature, and a "Made with Uno Platform" footer with the Uno Platform logo, used per Uno's brand guidelines.

- [ ] **Step 1: Build** the states: unsupported/denied (precise explanation from `Status`, e.g. "Camera needs a parked AMD Ryzen car on software 2026.26 or newer"), preview + big shutter button, a 3-2-1 countdown, the postcard result + "Retake". `PlaceText` = coordinates "50.08° N, 14.44° E" (no reverse geocoding in v1).
- [ ] **Step 2: Verify** in Playwright with fake media: preview shows the fake pattern; snap → the postcard is visible; switching the demo track into motion (`ForceDemo` + move) stops the stream (no live `MediaStreamTrack`: `browser_evaluate` checks `window.__cockpitCameraActive === false`, a flag `cockpit.js` maintains).
- [ ] **Step 3: Commit** `feat: Add Photo Booth`.

### Task 16: Cabin Sound

**Files:**
- Create: `Views/Apps/CabinSoundView.xaml(.cs)`, `Views/Drawing/SpectrumCanvas.cs`

**Interfaces:**
- Consumes: `CabinSoundViewModel`.

- [ ] **Step 1: Build** a 32-bar mirrored spectrum (accent colour, peaks decay 1.5/s), a big dB-style number (relative, labelled "dBFS"), and a "Clap-o-meter" button running the 5 s challenge with a progress ring and the score/best.
- [ ] **Step 2: Verify:** Playwright with a fake mic (Chromium's fake device produces a beep tone) → bars move, and the level number changes. Moving → the mic stops (`window.__cockpitMicActive === false`).
- [ ] **Step 3: Commit** `feat: Add Cabin Sound`.

### Task 17: Sensor Lab, voice and fullscreen

**Files:**
- Create: `Views/Apps/SensorLabView.xaml(.cs)`
- Modify: `cockpit.js` `probe` section, `BrowserCapabilityProbe.cs`, `Dock.xaml` (mic button visible only when `SpeechSupported()`)

**Interfaces:**
- Consumes: `SensorLabViewModel`, `ICapabilityProbe`.
- Produces: probe ids and rows: `geolocation`, `gamepad`, `camera`, `microphone`, `motion`, `orientation`, `speech`, `network`, `battery`, `fullscreen`, `wakelock`, `webgl` (renderer string), `cpu` (`hardwareConcurrency`), `memory` (`deviceMemory`), `vehicle` (UA hint: matches `Tesla/(\S+)` → "Tesla software X"; the WebGL renderer containing "AMD" → "likely AMD Ryzen", "Intel" → "likely Intel Atom").

- [ ] **Step 1: Build** a two-column card list (name + status pill coloured Available/Active/Denied/Unsupported/RequiresPark, detail text) with live updates from the hub (GPS accuracy, gamepad id, mic level). Add a "Copy report" button that puts the plain-text report on the clipboard (`DataPackage` / Uno clipboard), for issue reports. Voice: "open trip|arcade|photo|sound|lab" → `OpenAppCommand`.
- [ ] **Step 2: Verify:** a Playwright screenshot with every row populated; with `navigator.getGamepads` absent (stubbed), the row reads Unsupported.
- [ ] **Step 3: Commit** `feat: Add Sensor Lab, voice and fullscreen`.

### Task 18: Polish, docs, measurements and release deploy

**Files:**
- Create/modify: `README.md`, `THIRD-PARTY-NOTICES.md`, `.github/ISSUE_TEMPLATE/car-report.yml`, `docs/measurements.md`, `docs/screenshots/*`
- Modify: remove the `?spike` route and `Spike/` once the car test passed

- [ ] **Step 1: Measure** release builds both ways (interpreter vs `-p:WasmShellMonoRuntimeExecutionMode=InterpreterAndAOT`): transferred MB and time-to-interactive from a Playwright cold load of the Pages URL with cache disabled. Pick the one within budget (prefer the faster interactive time). Write the table to `docs/measurements.md` and the README.
- [ ] **Step 2: README:** what it is, the URL (to type in the car), the sensor matrix (sensor → mini-app → API used: Uno or JS interop → requirement such as parked/Ryzen), screenshots, how to run locally, the architecture diagram (Core vs app vs `cockpit.js`), "report your car" issue link, a non-affiliation line ("Not affiliated with or endorsed by Tesla, Inc."), MIT licence. Third-party notices: Inter (OFL 1.1), icon set, CommunityToolkit.Mvvm (MIT), Open-Meteo attribution (CC BY 4.0).
- [ ] **Step 3: Full verification:** `dotnet test UnoCockpit.Core.Tests` → all pass. CI green on `main`. Pages deploy succeeded. A Playwright pass over every mini-app on the live URL with no console errors.
- [ ] **Step 4: Commit** `docs: Add README, notices and measurements`, push, and hand Martin the final URL plus the car checklist again.
