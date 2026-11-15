using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace UnoCockpit.Sensors.Browser;

/// <summary>Bindings to <c>Platforms/WebAssembly/WasmScripts/cockpit.js</c>.</summary>
[SupportedOSPlatform("browser")]
internal static partial class Interop
{
    [JSImport("globalThis.Cockpit.geoGetCurrentJson")]
    public static partial Task<string> GeoGetCurrentJson(int timeoutMs);

    [JSImport("globalThis.Cockpit.mediaStartCamera")]
    public static partial Task<string> MediaStartCamera(string videoElementId);

    [JSImport("globalThis.Cockpit.mediaStopCamera")]
    public static partial void MediaStopCamera();

    [JSImport("globalThis.Cockpit.snapJpegBase64")]
    public static partial Task<string> SnapJpegBase64(string videoElementId);

    [JSImport("globalThis.Cockpit.mediaStartMic")]
    public static partial Task<string> MediaStartMic();

    [JSImport("globalThis.Cockpit.mediaStopMic")]
    public static partial void MediaStopMic();

    [JSImport("globalThis.Cockpit.micBands")]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static partial double[] MicBands();

    [JSImport("globalThis.Cockpit.micLevelDb")]
    public static partial double MicLevelDb();

    [JSImport("globalThis.Cockpit.motionLatestJson")]
    public static partial string MotionLatestJson();

    [JSImport("globalThis.Cockpit.audioPlay")]
    public static partial void AudioPlay(string cue);

    [JSImport("globalThis.Cockpit.fullscreenToggle")]
    public static partial void FullscreenToggle();

    [JSImport("globalThis.Cockpit.setPageBackground")]
    public static partial void SetPageBackground(string color);

    [JSImport("globalThis.Cockpit.copyText")]
    public static partial Task<bool> CopyText(string text);

    [JSImport("globalThis.Cockpit.isOnline")]
    public static partial bool IsOnline();

    [JSImport("globalThis.Cockpit.speechSupported")]
    public static partial bool SpeechSupported();

    [JSImport("globalThis.Cockpit.speechStart")]
    public static partial void SpeechStart();

    [JSImport("globalThis.Cockpit.speechTake")]
    public static partial string SpeechTake();

    [JSImport("globalThis.Cockpit.probeJson")]
    public static partial string ProbeJson();

    [JSImport("globalThis.Cockpit.browserLanguage")]
    public static partial string BrowserLanguage();

    [JSImport("globalThis.Cockpit.locationSearch")]
    public static partial string LocationSearch();
}
