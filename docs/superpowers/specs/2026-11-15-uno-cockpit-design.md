# Uno Cockpit: design

**Date:** 2026-11-15 · **Status:** approved 2026-11-15

## Intent

A fun Uno Platform 7.0 WebAssembly app built to run in a Tesla's built-in browser. It is a social media post plus a public GitHub repo people can browse and try in their own car. It should:

- look like it belongs on the car's touchscreen (the car's onboard design language), and
- use as many of the browser's sensors as possible, each in a way that makes sense, not as a checklist.

Success means someone opens the URL in their car, finds that the app "knows" it's in a car (speed, heading, location, gamepad, camera, mic), has fun with it, and wants to read the code. The repo should double as a clean reference for using device APIs from Uno on WebAssembly.

## Goals

- A car-style home screen: a car render on the left and a panel of sensor-powered mini-apps on the right, with a status bar on top and an app dock on the bottom.
- Every sensor is checked for at runtime. A tile only lights up when its sensor works, and otherwise explains why not (for example "Camera needs a parked AMD Ryzen car on 2026.26+").
- Uses Uno/WinRT APIs where Uno implements them on WASM, and `[JSImport]`/`[JSExport]` interop where it doesn't.
- A **Demo mode** that replays a recorded drive, so it works and looks alive on a laptop, in the GitHub Pages preview, and for recording the social video.
- Hosted on GitHub Pages over HTTPS (needed for geolocation, camera and mic), deployed from GitHub Actions.
- A README showing the sensor matrix, screenshots and a "try it in your car" URL.

## Non-goals

- Any vehicle data the browser can't see (battery state of charge, gear, door states, real tire pressures). No fake car telemetry pretending to be real.
- Getting photos out of the car (download, share or upload). The Photo Booth result stays on screen; people photograph it with their phone.
- Mobile, iOS, Android and Windows heads. Only `browserwasm` (product) and `desktop` (dev convenience).
- Map tiles or turn-by-turn navigation.

## Anti-goals (failure even if it "works")

- **Distracting the driver.** Interactive mini-apps (Arcade, Photo Booth, Cabin Sound) lock while the car is moving. Only the glanceable Trip HUD stays usable when moving.
- **Looking like a knockoff, or creating trademark trouble.** No Tesla logo, wordmark, model renders or Gotham font, and no "Tesla" in type or resource names. "Tesla" shows up only descriptively in the README ("runs in the Tesla browser"). The car render is an original, generic top-down vector car.
- **Encouraging reckless driving.** No 0–60 timers, lap timers, speed challenges or top-speed bragging.
- **A splash screen that never ends** on older Intel (MCU2) cars. Startup size and time are measured and kept small.
- Recreating an existing game. The Arcade game is original.

## Constraints and known platform facts

| Fact | Source / confidence | Consequence |
|---|---|---|
| Tesla browser is Chromium-based | multiple reports | Modern WASM, WebGL and Web Audio are OK |
| `navigator.geolocation.getCurrentPosition` prompts and works; `watchPosition` has errored in the past | Leaflet #7157 (2020), conflicting TMC threads | **Poll `getCurrentPosition` about once a second.** Never rely on `watchPosition` |
| Uno's WASM `Geolocator.RequestAccessAsync` uses `watchPosition` internally (`Geolocator.ts`); `GetGeopositionAsync` uses `getCurrentPosition` | Uno source | Don't call `RequestAccessAsync` on the car path; treat the first successful `GetGeopositionAsync` as "allowed". If Uno's path misbehaves, fall back to a small JS interop poller. Record any Uno bug found in the user's uno-issues store |
| Camera + mic via `getUserMedia`: **parked only**, **AMD Ryzen only**, firmware 2026.26+ | Not a Tesla App, 2026.26 release coverage | Detect support, show a precise "why not" message, stop the streams when moving |
| USB gamepads work with Uno WASM `Windows.Gaming.Input.Gamepad` in a Model 3 | Uno YouTube demo (older) | Use the Uno API; touch controls as fallback |
| Motion sensors (`DeviceMotion`/`DeviceOrientation`) | no evidence they exist | Detect; use real accelerometer data if present, else derive G-forces from GPS |
| Web Speech API | likely missing (needs Google keys) | Optional voice commands, only if detected |
| Screen: Model 3/Y about 1920×1200 physical, Model S/X about 2200×1300; browser chrome takes some height | general knowledge, unverified viewport | Responsive landscape layout, designed at 1600×900 CSS px, must hold from 1280×720 to 2200×1300 |
| Older Intel Atom (MCU2) cars are slow | general knowledge | Keep payload small; measure first load |
| Latest stable Uno.Sdk is **7.0.22** | nuget.org, 2026-11-13 | Pin in `global.json` |

## Approaches considered

1. **Home screen of sensor-powered mini-apps.** *Picked.* Every sensor gets a meaningful home, it screenshots well, and the repo reads like a sampler.
2. One game fusing all sensors. Most viral, but the sensors feel forced and it's a weaker code reference.
3. Driving dashboard only. Simplest, but leaves camera, mic and gamepad unused and gives you nothing to do while parked.

Separately, the user chose to **start a fresh repo and ignore the earlier `UnoTesla` attempt entirely**. Nothing from it is ported.

## What the app contains

### Shell (always visible)

- **Status bar (top):** clock, outside temperature (Open-Meteo, keyless, fetched from GPS about every 10 minutes), connectivity icon (`NetworkInformation`), GPS fix indicator, Demo-mode badge.
- **Car panel (left, about 55%):** an original top-down vector car. Headlights glow at night (sun position). A "P" badge or moving indicator comes from park detection. Pinch and rotate with multi-touch spins and zooms the car (manipulation events) as a playful touch demo. The current speed is overlaid large when moving.
- **App panel (right, about 45%):** the active mini-app.
- **Dock (bottom):** large icon buttons for each mini-app, plus a fullscreen toggle (Fullscreen API via interop).

### Mini-apps

1. **Trip HUD** (GPS; usable while moving; glanceable, no interaction needed)
   - Large speed readout with units toggle (km/h or mph, default from locale), heading compass rose, altitude, GPS accuracy ring.
   - G-force "friction circle": longitudinal from Δspeed/Δt, lateral from heading-rate × speed, smoothed. Uses real `DeviceMotion` data when present.
   - Trip odometer, trip time, average speed. No max-speed bragging.
   - Breadcrumb trace of the current trip drawn on a plain grid (no map tiles).
2. **Arcade** (gamepad + touch + Web Audio; parked only)
   - An original top-down game. Working concept: *"Charge Run"*. You steer a small car through a scrolling neon lot, collect charge bolts, dodge cones, and battery drains over time. Gamepad stick/D-pad or on-screen touch controls. Sound effects through Web Audio (car speakers). Local high score in `localStorage` (via `ApplicationData.LocalSettings`).
   - Drawn with `SKCanvasElement` (Uno.WinUI.Graphics2DSK) for a steady frame rate.
3. **Photo Booth** (camera; parked + supported hardware only)
   - Live preview through an embedded native HTML `<video>` element (Uno's WASM native-element embedding).
   - "Snap" grabs a frame in JS and passes JPEG bytes to C#. C# draws it with Skia under a HUD-style frame stamped with real data: place name (reverse geocode is optional, otherwise coordinates), local time, outside temperature, "Made with Uno Platform". The result is shown full-panel as a postcard.
4. **Cabin Sound** (mic; parked + supported hardware only)
   - A real-time spectrum visualizer and a dB-style loudness meter from a Web Audio `AnalyserNode` in JS. C# gets compact frequency bins (about 32 bands) each frame or every other frame.
   - A "Clap-o-meter" mini-challenge: make the cabin as loud as you can in 5 seconds, with a peak score.
5. **Sensor Lab** (diagnostics; always available)
   - A live table of every API: available / denied / unsupported / active, with the latest raw values. Covers geolocation, gamepads, camera, mic, motion, Web Speech, network, battery, fullscreen, wake lock, WebGL renderer string, `hardwareConcurrency`, `deviceMemory`, and a user-agent hint (for example "Tesla firmware 2026.32, likely AMD Ryzen").
   - This is also what the README screenshot and community bug reports point to.

### Cross-cutting behavior

- **Park detection:** a small state machine. "Parked" means GPS speed is under 2 km/h for 5 s (or no fix plus Demo mode parked). "Moving" means over 5 km/h. When moving, parked-only apps show a calm "Park to play" overlay and release the camera and mic.
- **Auto day/night theme:** sunrise and sunset computed locally (NOAA algorithm) from GPS position and time. Light theme by day and dark at night, like the car. Manual override in a small settings flyout.
- **Voice (optional):** if Web Speech exists, a mic button in the dock accepts "open arcade / trip / photo / sound / lab".
- **Demo mode:** a bundled, recorded GPS track (JSON, a few minutes, ending parked) replayed at 1×. Simulated network and a "virtual gamepad" hint. It turns on automatically when geolocation is unavailable or denied (with a banner), and can be toggled manually.

## Architecture (as decided)

- **Repo:** `D:\Personal\UnoCockpit`, public GitHub repo **`MartinZikmund/UnoCockpit`** (user's personal account; repo name is the agent's call). The agent creates the repo, pushes, and deploys to GitHub Pages at `https://martinzikmund.github.io/UnoCockpit/` (base path `/UnoCockpit/`), so Martin can test builds in his car.
- **SDK:** `Uno.Sdk` 7.0.22 pinned in `global.json`. App is a single Uno project (`UnoSingleProject`, TFMs `net10.0-browserwasm;net10.0-desktop`, Skia renderer) plus a plain `net10.0` class library **`UnoCockpit.Core`** holding all pure logic, referenced by both the app and the tests. *(Agent's call, from builder check.)*
- **UnoFeatures:** keep minimal: `Mvvm` (CommunityToolkit.Mvvm), `SkiaRenderer`, the Graphics2DSK add-in. No Extensions/Navigation/Hosting unless clearly needed (payload size).
- **Pattern:** MVVM with `x:Bind`. One view model per mini-app plus a shell view model.
- **Sensor layer:** one interface per capability (`ILocationSource`, `IGamepadSource`, `ICameraSource`, `IMicSource`, `IMotionSource`, `INetworkSource`, `ICapabilityProbe`). Each has a browser implementation and a Demo/simulated implementation. Pure logic (trip math, G-force derivation, park state machine, sun calculator, game rules) lives in plain classes with no UI or interop dependency.
- **Interop:** `[JSImport]`/`[JSExport]` against a small, readable `wwwroot/js/cockpit.js` (or a TS module if the template favors it). One JS file section per sensor, commented for repo readers.
- **Tests (TDD):** a plain `net10.0` test project against `UnoCockpit.Core` (MSTest, matching the user's Uno habits) covering the pure logic: haversine/trip distance, G-force from speed/heading samples, smoothing, park state machine transitions, sunrise/sunset vs known NOAA values, demo track replay, game collision and scoring. UI is verified with a Playwright smoke run in Chromium with emulated geolocation and permissions, and Chromium's fake media flags (`--use-fake-device-for-media-stream`, `--use-fake-ui-for-media-stream`) for camera and mic, plus screenshots at 1600×900 and 1920×1200. Gamepad and motion are verified through the simulated implementations only. Real-car checks are manual (see Phase 0). *(Agent's call.)*
- **Deploy:** GitHub Actions workflow publishing the WASM output to GitHub Pages, with the correct base path for a project site. A CI build plus unit tests run on every PR.

- **Phase 0 spike (gates everything else):** an Uno 7.0.22 Skia WASM hello-world deployed to Pages with a canvas animation, a `getCurrentPosition` poll, a gamepad readout and an embedded `<video>` element. It proves: (a) the renderer runs in the car, (b) native `<video>` embedding layers and receives touch correctly in 7.0 Skia WASM. If (b) fails, Photo Booth falls back to copying frames from a JS canvas into C# at a low rate (about 10 fps, 640×360). **Budget:** first load ≤ 12 MB transferred (compressed) and interactive in ≤ 15 s on a Ryzen car. Over budget means trim features or switch the AOT/interpreter choice before building more. *(Agent's call.)*
- **Licence:** MIT for the repo. Inter (OFL) and icon-set notices in `THIRD-PARTY-NOTICES.md`. *(Agent's call.)*
- **Privacy:** coordinates sent to Open-Meteo (and any reverse geocoder) are rounded to 2 decimals (about 1 km). If the weather call fails or the app is offline, the temperature is hidden, not shown as blank. Nothing else leaves the browser, and there is no analytics. *(Agent's call.)*

## Visual design language

The car's onboard UI, adapted with the winui-design rules:

- **Surfaces:** near-black dark theme (about `#111214` background, `#1C1D20` cards) and a clean light theme (about `#F4F4F4` / `#FFFFFF`). Lots of empty space and no visible chrome; the car render is the hero.
- **Accents:** a single blue for active/selected (about `#3E6AE1`), red only for warnings and recording (about `#E31937`), green for "available". Exact values are the builder's call, tuned for contrast.
- **Type:** Inter (SIL OFL), embedded. Light/regular weights for big numerals (the speed readout is about 120 px light), semibold for labels. Never Bold. Text is mostly sentence case.
- **Geometry:** 4 px grid. Large rounded cards (12–16 px radius) and pill toggles. **Minimum touch target 64×64** (it's a car screen, used at arm's length).
- **Motion:** short, calm transitions (150–250 ms ease-out). The car render subtly reacts (wheels turn with heading change, lights fade at dusk). No bouncy animation.
- **Iconography:** simple line icons (Fluent System Icons via the Uno icon font, or a small original SVG set). No car-maker icons.
- **WinUI rules kept:** `{ThemeResource}` brushes with Light/Dark/HighContrast dictionaries, built-in controls restyled with styles (no custom ControlTemplate-heavy controls unless needed), `x:Bind` with functions instead of converters, `AutomationProperties.Name`/`AutomationId` on icon-only buttons.

## Left to the builder

- Exact palette values, icon set, the car render artwork, game tuning and art.
- Whether the JS module is plain JS or TS.
- AOT vs interpreter for WASM, decided from **measured** first-load size and time (report both in the README).
- Demo track content (record or synthesize a plausible route; avoid any real personal location).
- Reverse geocoding provider for the Photo Booth caption (or coordinates only), keeping the key-free, privacy-respecting option.
- Folder layout inside the project.

## Open risks

- Real-car behavior can't be verified from the dev machine. The Sensor Lab plus a "report your car" issue template turns early users into testers.
- If the Uno WASM `Geolocator` or `Gamepad` breaks on Skia WASM (see Uno #24515 class of interop issues), switch that sensor to direct interop and record an Uno issue.
