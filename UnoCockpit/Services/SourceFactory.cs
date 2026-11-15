using UnoCockpit.Core.Demo;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.Storage;
using UnoCockpit.Sensors.Browser;
using UnoCockpit.Sensors.Local;
using Windows.Storage;

namespace UnoCockpit.Services;

public sealed record Sources(
    SourceSelector Location,
    IGamepadSource Gamepad,
    SimulatedGamepadSource VirtualGamepad,
    ICameraSource Camera,
    IMicSource Mic,
    IMotionSource Motion,
    ICapabilityProbe Probe,
    IAudioOut Audio,
    IKeyValueStore Store,
    bool SpeechSupported);

/// <summary>Real browser sensors on WebAssembly; the demo drive and keyboard everywhere else.</summary>
public static class SourceFactory
{
    public static async Task<Sources> CreateAsync()
    {
        DemoLocationSource demo = new(await LoadDemoTrackAsync(), TimeProvider.System);
        SimulatedGamepadSource virtualPad = new();
        LocalSettingsStore store = new();

        if (OperatingSystem.IsBrowser())
        {
            // ?geo=uno switches to Uno's Geolocator; the default polls getCurrentPosition directly (see cockpit.js).
            bool useUnoGeolocator = Interop.LocationSearch().Contains("geo=uno", StringComparison.OrdinalIgnoreCase);
            ILocationSource primary = useUnoGeolocator ? new UnoGeolocatorSource() : new BrowserLocationSource();
            return new(
                new SourceSelector(primary, demo, TimeProvider.System),
                new CompositeGamepadSource(new UnoGamepadSource(), virtualPad),
                virtualPad,
                new BrowserCameraSource(),
                new BrowserMicSource(),
                new BrowserMotionSource(),
                new BrowserCapabilityProbe(),
                new BrowserAudioOut(),
                store,
                Interop.SpeechSupported());
        }

        return new(
            new SourceSelector(demo, demo, TimeProvider.System) { ForceDemo = true },
            virtualPad,
            virtualPad,
            new UnavailableCameraSource(),
            new UnavailableMicSource(),
            new NoMotionSource(),
            new DesktopCapabilityProbe(),
            new SilentAudioOut(),
            store,
            SpeechSupported: false);
    }

    private static async Task<DemoTrack> LoadDemoTrackAsync()
    {
        StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/demo-track.json"));
        using Stream stream = await file.OpenStreamForReadAsync();
        return DemoTrack.Load(stream);
    }
}
