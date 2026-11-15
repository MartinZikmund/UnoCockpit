namespace UnoCockpit.Core.Sun;

public enum PolarCondition
{
    None,
    PolarDay,
    PolarNight,
}

public sealed record SunTimes(DateTimeOffset? SunriseUtc, DateTimeOffset? SunsetUtc, PolarCondition Polar);

/// <summary>Sunrise and sunset from the NOAA solar calculator equations, used for the automatic day/night theme.</summary>
public static class SunCalculator
{
    // Includes atmospheric refraction and the sun's apparent radius.
    private const double ZenithDeg = 90.833;

    public static SunTimes GetSunTimes(DateOnly date, double lat, double lon)
    {
        DateTimeOffset midnightUtc = new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        double julianDay = 2440587.5 + (midnightUtc.ToUnixTimeSeconds() / 86400.0) + 0.5 - lon / 360;
        double t = (julianDay - 2451545.0) / 36525.0;

        double meanLong = Normalize(280.46646 + t * (36000.76983 + t * 0.0003032));
        double meanAnomaly = 357.52911 + t * (35999.05029 - 0.0001537 * t);
        double eccentricity = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
        double center = SinD(meanAnomaly) * (1.914602 - t * (0.004817 + 0.000014 * t))
            + SinD(2 * meanAnomaly) * (0.019993 - 0.000101 * t)
            + SinD(3 * meanAnomaly) * 0.000289;
        double omega = 125.04 - 1934.136 * t;
        double apparentLong = meanLong + center - 0.00569 - 0.00478 * SinD(omega);
        double meanObliquity = 23 + (26 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60) / 60;
        double obliquity = meanObliquity + 0.00256 * CosD(omega);
        double declination = AsinD(SinD(obliquity) * SinD(apparentLong));

        double y = Math.Pow(Math.Tan(Rad(obliquity / 2)), 2);
        double equationOfTimeMin = 4 * Deg(
            y * SinD(2 * meanLong)
            - 2 * eccentricity * SinD(meanAnomaly)
            + 4 * eccentricity * y * SinD(meanAnomaly) * CosD(2 * meanLong)
            - 0.5 * y * y * SinD(4 * meanLong)
            - 1.25 * eccentricity * eccentricity * SinD(2 * meanAnomaly));

        double cosHourAngle = CosD(ZenithDeg) / (CosD(lat) * CosD(declination)) - TanD(lat) * TanD(declination);
        if (cosHourAngle > 1)
        {
            return new(null, null, PolarCondition.PolarNight);
        }

        if (cosHourAngle < -1)
        {
            return new(null, null, PolarCondition.PolarDay);
        }

        double hourAngle = Deg(Math.Acos(cosHourAngle));
        double solarNoonMin = 720 - 4 * lon - equationOfTimeMin;
        return new(
            midnightUtc.AddMinutes(solarNoonMin - 4 * hourAngle),
            midnightUtc.AddMinutes(solarNoonMin + 4 * hourAngle),
            PolarCondition.None);
    }

    public static bool IsDaylight(DateTimeOffset utcNow, double lat, double lon)
    {
        // Use the local solar date, so places near the date line pick the right day.
        DateOnly solarDate = DateOnly.FromDateTime(utcNow.UtcDateTime.AddHours(lon / 15));
        SunTimes s = GetSunTimes(solarDate, lat, lon);
        return s.Polar switch
        {
            PolarCondition.PolarDay => true,
            PolarCondition.PolarNight => false,
            _ => utcNow >= s.SunriseUtc && utcNow < s.SunsetUtc,
        };
    }

    private static double Rad(double deg) => deg * Math.PI / 180;

    private static double Deg(double rad) => rad * 180 / Math.PI;

    private static double SinD(double deg) => Math.Sin(Rad(deg));

    private static double CosD(double deg) => Math.Cos(Rad(deg));

    private static double TanD(double deg) => Math.Tan(Rad(deg));

    private static double AsinD(double x) => Deg(Math.Asin(x));

    private static double Normalize(double deg) => ((deg % 360) + 360) % 360;
}
