using System.Globalization;
using System.Net;
using UnoCockpit.Core.Weather;

namespace UnoCockpit.Core.Tests.Weather;

[TestClass]
public sealed class OpenMeteoWeatherSourceTests
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    [TestMethod]
    public async Task RoundsTo2Decimals_InvariantCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("cs-CZ");
        Handler h = new(HttpStatusCode.OK, """{"current":{"temperature_2m":12.3}}""");
        OpenMeteoWeatherSource w = new(new HttpClient(h));

        double? t;
        try
        {
            t = await w.GetTemperatureCAsync(50.0755, 14.4378, CancellationToken.None);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.AreEqual(12.3, t);
        StringAssert.Contains(h.LastUri!.Query, "latitude=50.08&longitude=14.44");
        StringAssert.Contains(h.LastUri.Query, "current=temperature_2m");
    }

    [TestMethod]
    public async Task HttpError_ReturnsNull()
    {
        OpenMeteoWeatherSource w = new(new HttpClient(new Handler(HttpStatusCode.InternalServerError, "")));

        Assert.IsNull(await w.GetTemperatureCAsync(50, 14, CancellationToken.None));
    }

    [TestMethod]
    public async Task MalformedJson_ReturnsNull()
    {
        OpenMeteoWeatherSource w = new(new HttpClient(new Handler(HttpStatusCode.OK, "{oops")));

        Assert.IsNull(await w.GetTemperatureCAsync(50, 14, CancellationToken.None));
    }
}
