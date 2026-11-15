using UnoCockpit.Core.Sensors;

namespace UnoCockpit.Core.Tests.Sensors;

[TestClass]
public sealed class BrowserJsonTests
{
    [TestMethod]
    public void Location_Ok_ParsesFix()
    {
        LocationResult r = BrowserJson.ParseLocation("""{"ok":true,"lat":50.1,"lon":14.4,"alt":null,"acc":8,"speed":null,"heading":null,"ts":1794734160830}""");

        Assert.AreEqual(LocationStatus.Ok, r.Status);
        Assert.AreEqual(50.1, r.Sample!.Latitude);
        Assert.AreEqual(8, r.Sample.AccuracyM);
        Assert.IsNull(r.Sample.SpeedMps);
        Assert.IsNull(r.Sample.AltitudeM);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1794734160830), r.Sample.Timestamp);
    }

    [TestMethod]
    public void Location_WithSpeedAndHeading()
    {
        LocationResult r = BrowserJson.ParseLocation("""{"ok":true,"lat":1,"lon":2,"alt":250.5,"acc":4,"speed":12.5,"heading":270,"ts":0}""");

        Assert.AreEqual(12.5, r.Sample!.SpeedMps);
        Assert.AreEqual(270, r.Sample.HeadingDeg);
        Assert.AreEqual(250.5, r.Sample.AltitudeM);
    }

    [TestMethod]
    [DataRow("""{"ok":false,"code":1}""", LocationStatus.Denied)]
    [DataRow("""{"ok":false,"code":2}""", LocationStatus.Unavailable)]
    [DataRow("""{"ok":false,"code":3}""", LocationStatus.Timeout)]
    [DataRow("""{"ok":false,"code":"unsupported"}""", LocationStatus.Unsupported)]
    [DataRow("garbage", LocationStatus.Unavailable)]
    public void Location_Errors(string json, LocationStatus expected) => Assert.AreEqual(expected, BrowserJson.ParseLocation(json).Status);

    [TestMethod]
    public void Motion_NotSeen_IsNull() => Assert.IsNull(BrowserJson.ParseMotion("""{"seen":false,"lon":0,"lat":0,"ts":0}"""));

    [TestMethod]
    public void Motion_Seen_Parses()
    {
        MotionSample? m = BrowserJson.ParseMotion("""{"seen":true,"lon":-1.5,"lat":0.5,"ts":1}""");

        Assert.AreEqual(new MotionSample(-1.5, 0.5), m);
    }

    private const string TeslaProbe = """
        {"userAgent":"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0 Safari/537.36 Tesla/2026.32.5-abc","secureContext":true,
         "geolocation":true,"gamepad":true,"mediaDevices":true,"deviceMotion":false,"deviceOrientation":false,"speech":false,"online":true,
         "connectionType":"4g","battery":false,"fullscreen":true,"wakeLock":true,"hardwareConcurrency":4,"deviceMemory":8,
         "webgl":"AMD Radeon Graphics (navi23)","sinceNavigationMs":5000}
        """;

    private static CapabilityReport Row(IReadOnlyList<CapabilityReport> rows, string id) => rows.Single(r => r.Id == id);

    [TestMethod]
    public void Probe_Vehicle_FromUserAgentAndGpu()
    {
        CapabilityReport v = Row(BrowserJson.ParseProbe(TeslaProbe), "vehicle");

        StringAssert.Contains(v.Detail, "2026.32.5");
        StringAssert.Contains(v.Detail, "AMD Ryzen");
        Assert.AreEqual(CapabilityStatus.Available, v.Status);
    }

    [TestMethod]
    public void Probe_Statuses()
    {
        IReadOnlyList<CapabilityReport> rows = BrowserJson.ParseProbe(TeslaProbe);

        Assert.AreEqual(CapabilityStatus.Available, Row(rows, "geolocation").Status);
        Assert.AreEqual(CapabilityStatus.Available, Row(rows, "camera").Status);
        Assert.AreEqual(CapabilityStatus.Unsupported, Row(rows, "motion").Status);
        Assert.AreEqual(CapabilityStatus.Unsupported, Row(rows, "speech").Status);
        Assert.AreEqual(CapabilityStatus.Unsupported, Row(rows, "battery").Status);
        StringAssert.Contains(Row(rows, "cpu").Detail, "4");
        StringAssert.Contains(Row(rows, "webgl").Detail, "AMD Radeon");
    }

    [TestMethod]
    public void Probe_Intel_NoCameraHint()
    {
        string json = TeslaProbe.Replace("AMD Radeon Graphics (navi23)", "Mesa Intel(R) HD Graphics 505").Replace("\"mediaDevices\":true", "\"mediaDevices\":false");
        IReadOnlyList<CapabilityReport> rows = BrowserJson.ParseProbe(json);

        StringAssert.Contains(Row(rows, "vehicle").Detail, "Intel Atom");
        Assert.AreEqual(CapabilityStatus.Unsupported, Row(rows, "camera").Status);
        StringAssert.Contains(Row(rows, "camera").Detail, "Ryzen");
    }

    [TestMethod]
    public void Probe_NotATesla()
    {
        string json = TeslaProbe.Replace(" Tesla/2026.32.5-abc", "");

        Assert.AreEqual(CapabilityStatus.Unsupported, Row(BrowserJson.ParseProbe(json), "vehicle").Status);
    }
}
