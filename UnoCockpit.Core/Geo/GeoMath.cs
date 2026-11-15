namespace UnoCockpit.Core.Geo;

public static class GeoMath
{
    public const double EarthRadiusM = 6_371_008.8;

    public static double ToRadians(double deg) => deg * Math.PI / 180;

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Pow(Math.Sin(dLat / 2), 2) +
            Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusM * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
    {
        double φ1 = ToRadians(lat1);
        double φ2 = ToRadians(lat2);
        double dLon = ToRadians(lon2 - lon1);
        double y = Math.Sin(dLon) * Math.Cos(φ2);
        double x = Math.Cos(φ1) * Math.Sin(φ2) - Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(dLon);
        return NormalizeDeg(Math.Atan2(y, x) * 180 / Math.PI);
    }

    /// <summary>Signed shortest turn from one heading to another, in [-180, 180).</summary>
    public static double HeadingDeltaDeg(double fromDeg, double toDeg) => NormalizeDeg(toDeg - fromDeg + 180) - 180;

    public static double NormalizeDeg(double deg)
    {
        double d = deg % 360;
        return d < 0 ? d + 360 : d;
    }
}
