using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace UnoCockpit.Sensors.Browser;

/// <summary>Bindings to <c>WasmScripts/cockpit.js</c>.</summary>
[SupportedOSPlatform("browser")]
internal static partial class Interop
{
    [JSImport("globalThis.Cockpit.geoGetCurrentJson")]
    public static partial Task<string> GeoGetCurrentJson(int timeoutMs);

    [JSImport("globalThis.Cockpit.mediaStartCamera")]
    public static partial Task<string> MediaStartCamera(string videoElementId);

    [JSImport("globalThis.Cockpit.mediaStopCamera")]
    public static partial void MediaStopCamera();

    [JSImport("globalThis.Cockpit.probeJson")]
    public static partial string ProbeJson();

    [JSImport("globalThis.Cockpit.locationSearch")]
    public static partial string LocationSearch();
}
