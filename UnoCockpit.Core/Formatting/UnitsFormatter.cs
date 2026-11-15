using System.Globalization;

namespace UnoCockpit.Core.Formatting;

public sealed class UnitsFormatter(bool metric)
{
    private const double MetresPerMile = 1_609.344;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public bool Metric => metric;

    public string SpeedUnit => metric ? "km/h" : "mph";

    // Countries that drive in mph and read °F; the UK uses mph but °C, so it stays metric and can toggle.
    private static readonly HashSet<string> ImperialRegions = new(StringComparer.OrdinalIgnoreCase) { "US", "LR", "MM", "PR", "GU", "VI", "AS", "MP", "UM" };

    /// <summary>Default units from a BCP 47 language tag such as <c>navigator.language</c> ("en-US").</summary>
    public static bool DefaultMetricForLanguage(string? language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return true;
        }

        string[] parts = language.Split('-', '_');
        return parts.Length < 2 || !ImperialRegions.Contains(parts[^1]);
    }

    public string Speed(double? mps) => mps is { } v
        ? Math.Round(v * (metric ? 3.6 : 3600 / MetresPerMile)).ToString("0", Invariant)
        : "—";

    public string Distance(double metres)
    {
        if (!metric)
        {
            return (metres / MetresPerMile).ToString("0.0", Invariant) + " mi";
        }

        return metres < 1_000
            ? Math.Round(metres).ToString("0", Invariant) + " m"
            : (metres / 1_000).ToString("0.0", Invariant) + " km";
    }

    public string Altitude(double? metres) => metres is { } m
        ? (metric ? m : m * 3.28084).ToString("0", Invariant) + (metric ? " m" : " ft")
        : "—";

    public string Temperature(double? celsius) => celsius is { } c
        ? Math.Round(metric ? c : c * 9 / 5 + 32).ToString("0", Invariant) + (metric ? "°C" : "°F")
        : "";

    public static string Coordinates(double lat, double lon) => string.Create(Invariant,
        $"{Math.Abs(lat):0.00}° {(lat >= 0 ? 'N' : 'S')}, {Math.Abs(lon):0.00}° {(lon >= 0 ? 'E' : 'W')}");

    public static string Duration(TimeSpan t) => t.TotalHours >= 1
        ? t.ToString(@"h\:mm\:ss", Invariant)
        : t.ToString(@"mm\:ss", Invariant);
}
