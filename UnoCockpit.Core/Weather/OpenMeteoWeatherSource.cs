using System.Globalization;
using System.Text.Json;

namespace UnoCockpit.Core.Weather;

/// <summary>Outside temperature from Open-Meteo (keyless). Coordinates are rounded to about 1 km before leaving the car.</summary>
public sealed class OpenMeteoWeatherSource(HttpClient http)
{
    public async Task<double?> GetTemperatureCAsync(double lat, double lon, CancellationToken ct)
    {
        string url = string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={lat:F2}&longitude={lon:F2}&current=temperature_2m");
        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(ct);
            using JsonDocument doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            return doc.RootElement.GetProperty("current").GetProperty("temperature_2m").GetDouble();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or TaskCanceledException)
        {
            return null;
        }
    }
}
