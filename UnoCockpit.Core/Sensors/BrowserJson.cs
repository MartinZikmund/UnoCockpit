using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnoCockpit.Core.Geo;

namespace UnoCockpit.Core.Sensors;

/// <summary>Parses the JSON that <c>cockpit.js</c> hands back, so the interop layer stays a thin pass-through.</summary>
public static partial class BrowserJson
{
    public const string CameraRequirement = "Needs a parked car with the AMD Ryzen computer on software 2026.26 or newer";

    public static LocationResult ParseLocation(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (!root.GetProperty("ok").GetBoolean())
            {
                JsonElement code = root.GetProperty("code");
                return LocationResult.Failed(code.ValueKind == JsonValueKind.Number
                    ? code.GetInt32() switch
                    {
                        1 => LocationStatus.Denied,
                        3 => LocationStatus.Timeout,
                        _ => LocationStatus.Unavailable,
                    }
                    : LocationStatus.Unsupported);
            }

            return new(LocationStatus.Ok, new GeoSample(
                DateTimeOffset.FromUnixTimeMilliseconds((long)root.GetProperty("ts").GetDouble()),
                root.GetProperty("lat").GetDouble(),
                root.GetProperty("lon").GetDouble(),
                NullableDouble(root, "alt"),
                root.GetProperty("acc").GetDouble(),
                NullableDouble(root, "speed"),
                NullableDouble(root, "heading")));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return LocationResult.Failed(LocationStatus.Unavailable);
        }
    }

    public static MotionSample? ParseMotion(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        return root.GetProperty("seen").GetBoolean()
            ? new MotionSample(root.GetProperty("lon").GetDouble(), root.GetProperty("lat").GetDouble())
            : null;
    }

    public static IReadOnlyList<CapabilityReport> ParseProbe(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement p = doc.RootElement;
        string userAgent = Str(p, "userAgent") ?? "";
        string? webgl = Str(p, "webgl");

        return
        [
            new("vehicle", "Vehicle", VehicleStatus(userAgent), VehicleDetail(userAgent, webgl)),
            Flag(p, "geolocation", "Location (GPS)", "Speed, heading, altitude and trip", "Browser has no geolocation"),
            Flag(p, "gamepad", "Gamepad", "Plug in a USB controller and press a button", "Browser has no Gamepad API"),
            Flag(p, "mediaDevices", "camera", "Camera", CameraRequirement, CameraRequirement),
            Flag(p, "mediaDevices", "microphone", "Microphone", CameraRequirement, CameraRequirement),
            Flag(p, "deviceMotion", "motion", "Motion sensors", "Waiting for motion data", "No accelerometer exposed; G-forces come from GPS"),
            Flag(p, "speech", "speech", "Voice commands", "Say \"open arcade\"", "No speech recognition in this browser"),
            new("network", "Network", Bool(p, "online") ? CapabilityStatus.Available : CapabilityStatus.Unsupported,
                Str(p, "connectionType") is { } type ? $"Online · {type}" : "Online"),
            Flag(p, "battery", "battery", "Battery API", "Exposed (reports the computer, not the car)", "Not exposed"),
            Flag(p, "fullscreen", "fullscreen", "Fullscreen", "Tap the expand button in the dock", "Not available"),
            Flag(p, "wakeLock", "wakelock", "Screen wake lock", "Available", "Not available"),
            new("webgl", "Graphics", webgl is null ? CapabilityStatus.Unsupported : CapabilityStatus.Available, webgl ?? "No WebGL"),
            new("cpu", "CPU threads", CapabilityStatus.Available, Num(p, "hardwareConcurrency") ?? "unknown"),
            new("memory", "Memory", CapabilityStatus.Available, Num(p, "deviceMemory") is { } gb ? $"≥ {gb} GB" : "unknown"),
        ];
    }

    private static CapabilityStatus VehicleStatus(string userAgent) =>
        TeslaVersion().IsMatch(userAgent) ? CapabilityStatus.Available : CapabilityStatus.Unsupported;

    private static string VehicleDetail(string userAgent, string? webgl)
    {
        Match m = TeslaVersion().Match(userAgent);
        string software = m.Success ? $"Tesla software {m.Groups[1].Value}" : "Not running in a car browser";
        string? computer = webgl switch
        {
            null => null,
            _ when webgl.Contains("AMD", StringComparison.OrdinalIgnoreCase) => "likely AMD Ryzen",
            _ when webgl.Contains("Intel", StringComparison.OrdinalIgnoreCase) => "likely Intel Atom",
            _ => null,
        };
        return computer is null || !m.Success ? software : $"{software} · {computer}";
    }

    private static CapabilityReport Flag(JsonElement p, string id, string name, string yes, string no) => Flag(p, id, id, name, yes, no);

    private static CapabilityReport Flag(JsonElement p, string key, string id, string name, string yes, string no) =>
        Bool(p, key) ? new(id, name, CapabilityStatus.Available, yes) : new(id, name, CapabilityStatus.Unsupported, no);

    private static bool Bool(JsonElement p, string key) => p.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.True;

    private static string? Str(JsonElement p, string key) =>
        p.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Num(JsonElement p, string key) =>
        p.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble().ToString(CultureInfo.InvariantCulture) : null;

    private static double? NullableDouble(JsonElement root, string key) =>
        root.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    [GeneratedRegex(@"Tesla/(\S+)")]
    private static partial Regex TeslaVersion();
}
