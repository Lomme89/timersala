using TimerSala.Core.Models;
using TimerSala.Core.Wol;

namespace TimerSala.Tests;

public class WolClientTests
{
    [Fact]
    public async Task Fetches_week_from_meetings_page_with_inline_workbook()
    {
        var handler = new FakeHandler(u => u.AbsolutePath.Contains("/wol/meetings/") ? Fixture.Read("meetings-it.html") : null);
        using var client = new WolClient(new HttpClient(handler));

        var week = await client.FetchWeekAsync(new DateOnly(2026, 10, 8), WolLanguage.Italian);

        Assert.Equal("https://wol.jw.org/it/wol/meetings/r6/lp-i/2026/41", handler.Requested[0]);
        Assert.Equal(new DateOnly(2026, 10, 5), week.WeekStart);
        Assert.Equal(10, week.Midweek.Parts.Count(p => p.IsTimed));
        Assert.NotNull(week.DownloadedMidweek);

        var wt = week.Weekend.Parts.Single(p => p.Section == PartSection.Watchtower && p.IsTimed);
        Assert.Equal("Studio Torre di Guardia: Come possiamo trovare vero riposo?", wt.Title);
        Assert.Contains(week.Weekend.Parts, p => p.IsSong && p.Title == "Cantico 12");
        Assert.Contains(week.Weekend.Parts, p => p.IsSong && p.Title == "Cantico 140 e preghiera");
    }

    [Fact]
    public async Task Follows_link_to_workbook_article()
    {
        var lang = WolLanguage.Presets.Single(l => l.Code == "en");
        var handler = new FakeHandler(u =>
            u.AbsolutePath.Contains("/wol/meetings/") ? Fixture.Read("meetings-en-linked.html")
            : u.AbsolutePath.EndsWith("/202026361") ? Fixture.Read("mwb-en-article.html")
            : null);
        using var client = new WolClient(new HttpClient(handler));

        var week = await client.FetchWeekAsync(new DateOnly(2026, 10, 5), lang);

        Assert.Equal(10, week.Midweek.Parts.Count(p => p.IsTimed));
        Assert.Equal("https://wol.jw.org/en/wol/d/r1/lp-e/202026361", week.SourceUrl);
    }

    [Fact]
    public async Task Throws_readable_error_when_not_found()
    {
        using var client = new WolClient(new HttpClient(new FakeHandler(_ => "<html><body>vuoto</body></html>")));
        var ex = await Assert.ThrowsAsync<WolFetchException>(() => client.FetchWeekAsync(new DateOnly(2026, 10, 5), WolLanguage.Italian));
        Assert.Contains("non è stato trovato", ex.Message);
    }
}
