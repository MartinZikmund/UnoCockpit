using UnoCockpit.Core.Formatting;

namespace UnoCockpit.Core.Tests.Formatting;

[TestClass]
public sealed class UnitsFormatterTests
{
    [TestMethod]
    public void Speed_Null_IsDash() => Assert.AreEqual("—", new UnitsFormatter(true).Speed(null));

    [TestMethod]
    public void Speed_Metric() => Assert.AreEqual("100", new UnitsFormatter(true).Speed(27.78));

    [TestMethod]
    public void Speed_Imperial() => Assert.AreEqual("60", new UnitsFormatter(false).Speed(26.82));

    [TestMethod]
    public void Distance_Metric()
    {
        UnitsFormatter u = new(true);
        Assert.AreEqual("850 m", u.Distance(850));
        Assert.AreEqual("12.3 km", u.Distance(12_340));
    }

    [TestMethod]
    public void Distance_Imperial() => Assert.AreEqual("1.0 mi", new UnitsFormatter(false).Distance(1_609.344));

    [TestMethod]
    public void Temperature()
    {
        Assert.AreEqual("", new UnitsFormatter(true).Temperature(null));
        Assert.AreEqual("12°C", new UnitsFormatter(true).Temperature(12.3));
        Assert.AreEqual("54°F", new UnitsFormatter(false).Temperature(12.3));
    }

    [TestMethod]
    public void Coordinates_HemisphereLetters() =>
        Assert.AreEqual("33.87° S, 151.21° E", UnitsFormatter.Coordinates(-33.8688, 151.2093));

    [TestMethod]
    [DataRow("en-US", false)]
    [DataRow("es-US", false)]
    [DataRow("en-GB", true)]
    [DataRow("cs-CZ", true)]
    [DataRow("de", true)]
    [DataRow("", true)]
    [DataRow(null, true)]
    public void DefaultMetric_FromBrowserLanguage(string? language, bool metric) =>
        Assert.AreEqual(metric, UnitsFormatter.DefaultMetricForLanguage(language));
}
