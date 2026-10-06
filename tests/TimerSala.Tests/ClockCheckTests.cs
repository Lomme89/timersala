using System.Net;
using TimerSala.Core.Timing;

namespace TimerSala.Tests;

public class ClockCheckTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 7, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Offset_compares_with_the_middle_of_the_request()
    {
        // server alle 18:00:00 (cioè tra 18:00:00 e 18:00:01), richiesta partita 3 minuti avanti e durata 1 s
        var offset = ClockCheck.Offset(T0, T0.AddMinutes(3), T0.AddMinutes(3).AddSeconds(1));
        Assert.Equal(TimeSpan.FromMinutes(3), offset);
        Assert.Null(ClockCheck.Offset(null, T0, T0));
        Assert.Null(ClockCheck.Offset(T0, T0, T0.AddMinutes(1)));
    }

    [Fact]
    public void Warning_only_beyond_a_minute()
    {
        Assert.Null(ClockCheck.Warning(null));
        Assert.Null(ClockCheck.Warning(TimeSpan.FromSeconds(-50)));
        Assert.StartsWith("L'orologio del PC è indietro di 4 minuti", ClockCheck.Warning(TimeSpan.FromMinutes(-4)));
        Assert.StartsWith("L'orologio del PC è avanti di 1 ora", ClockCheck.Warning(TimeSpan.FromMinutes(62)));
        Assert.StartsWith("L'orologio del PC è avanti di 2 ore", ClockCheck.Warning(TimeSpan.FromHours(2)));
    }

    [Fact]
    public async Task Measure_reads_the_date_header_and_falls_back()
    {
        var clock = new FakeClock(T0.AddMinutes(-5));
        int calls = 0;
        var handler = new DateHandler(uri =>
        {
            calls++;
            if (uri.Host == "wol.jw.org") throw new HttpRequestException("offline");
            return T0;
        });
        var offset = await ClockCheck.MeasureAsync(new HttpClient(handler), clock);
        Assert.Equal(2, calls);
        Assert.Equal(TimeSpan.FromMinutes(-5) - TimeSpan.FromMilliseconds(500), offset);
    }

    [Fact]
    public async Task Measure_is_null_when_offline()
    {
        var handler = new DateHandler(_ => throw new HttpRequestException("offline"));
        Assert.Null(await ClockCheck.MeasureAsync(new HttpClient(handler), new FakeClock(T0)));
    }

    sealed class DateHandler(Func<Uri, DateTimeOffset> date) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Headers.Date = date(request.RequestUri!);
            return Task.FromResult(resp);
        }
    }
}
