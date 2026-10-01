using System.Net;

namespace TimerSala.Tests;

sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public void Advance(TimeSpan t) => _now += t;
}

sealed class FakeHandler(Func<Uri, string?> pages) : HttpMessageHandler
{
    public List<string> Requested { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requested.Add(request.RequestUri!.ToString());
        var html = pages(request.RequestUri!);
        return Task.FromResult(html is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
    }
}

static class Fixture
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
