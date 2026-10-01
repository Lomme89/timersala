using TimerSala.Core.Models;
using TimerSala.Core.Storage;
using TimerSala.Core.Wol;

namespace TimerSala.Tests;

public class WeekSyncTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "timersala-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    // settimane 41, 42 e 43 del 2026 pubblicate, dalla 44 in poi no
    static FakeHandler Wol() => new(u =>
    {
        var path = u.AbsolutePath;
        if (!path.Contains("/wol/meetings/")) return null;
        int week = int.Parse(path.Split('/')[^1]);
        return week <= 43 ? Fixture.Read("meetings-it.html") : "<html><body>Non disponibile</body></html>";
    });

    [Fact]
    public async Task Downloads_until_weeks_are_not_published_and_overwrites()
    {
        var store = new DataStore(_dir);
        var old = new WeekSchedule { WeekStart = new DateOnly(2026, 10, 12), Midweek = MeetingTemplates.DefaultMidweek() };
        store.SaveWeek(old);

        using var wol = new WolClient(new HttpClient(Wol()));
        var r = await WeekSync.DownloadAheadAsync(wol, store, WolLanguage.Italian, new DateOnly(2026, 10, 7));

        Assert.Equal(3, r.Updated);
        Assert.Equal(0, r.KeptEdited);
        Assert.Null(r.Error);
        Assert.Equal(new DateOnly(2026, 10, 19), r.UpdatedWeeks[^1]);
        // la settimana già salvata (non modificata) è stata sovrascritta con lo schema scaricato
        Assert.NotNull(store.LoadWeek(new DateOnly(2026, 10, 12))!.FetchedAt);
        Assert.Null(store.LoadWeek(new DateOnly(2026, 10, 26)));
    }

    [Fact]
    public async Task Keeps_manually_edited_weeks_and_reapplies_overseer_visit()
    {
        var store = new DataStore(_dir);
        var edited = new WeekSchedule { WeekStart = new DateOnly(2026, 10, 5), Midweek = MeetingTemplates.DefaultMidweek(), EditedManually = true };
        edited.Midweek.Parts[1].Title = "Parte speciale";
        store.SaveWeek(edited);
        store.SaveWeek(new WeekSchedule { WeekStart = new DateOnly(2026, 10, 12), CircuitOverseerVisit = true });

        using var wol = new WolClient(new HttpClient(Wol()));
        var r = await WeekSync.DownloadAheadAsync(wol, store, WolLanguage.Italian, new DateOnly(2026, 10, 5));

        Assert.Equal(1, r.KeptEdited);
        Assert.Equal(2, r.Updated);
        Assert.Equal("Parte speciale", store.LoadWeek(new DateOnly(2026, 10, 5))!.Midweek.Parts[1].Title);
        var co = store.LoadWeek(new DateOnly(2026, 10, 12))!;
        Assert.True(co.CircuitOverseerVisit);
        Assert.Contains(co.Midweek.Parts, p => p.Title == MeetingTemplates.OverseerTalkTitle);
        Assert.Contains("modificata a mano", r.Summary);
    }

    [Fact]
    public async Task Stops_on_network_error()
    {
        var store = new DataStore(_dir);
        var handler = new FakeHandler(_ => throw new HttpRequestException("rete assente"));
        using var wol = new WolClient(new HttpClient(handler));
        var r = await WeekSync.DownloadAheadAsync(wol, store, WolLanguage.Italian, new DateOnly(2026, 10, 5));
        Assert.Equal(0, r.Updated);
        Assert.NotNull(r.Error);
        Assert.True(handler.Requested.Count <= 2);
    }
}
