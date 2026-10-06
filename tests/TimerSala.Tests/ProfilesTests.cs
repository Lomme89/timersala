using TimerSala.Core.Storage;

namespace TimerSala.Tests;

public class ProfilesTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "timersala-profili-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Each_congregation_has_its_own_settings_and_the_right_one_is_chosen_by_time()
    {
        // prima congregazione: quella di sempre, nella cartella principale
        new DataStore(_dir).SaveSettings(new AppSettings { MidweekDay = DayOfWeek.Tuesday, MidweekTime = new(19, 0), WeekendDay = DayOfWeek.Sunday, WeekendTime = new(10, 0) });
        var catalog = new ProfileCatalog(_dir);
        Assert.False(catalog.HasSeveral);

        var second = catalog.Add("Congregazione rumena", new AppSettings { MidweekDay = DayOfWeek.Thursday, MidweekTime = new(19, 30), WeekendDay = DayOfWeek.Sunday, WeekendTime = new(16, 0), AssemblyWeeks = [new DateOnly(2026, 10, 12)] });
        Assert.True(catalog.HasSeveral);
        Assert.Empty(catalog.Open(second).LoadSettings().AssemblyWeeks); // le settimane particolari non si copiano

        // ricaricato dal disco
        catalog = new ProfileCatalog(_dir);
        Assert.Equal(["Congregazione", "Congregazione rumena"], catalog.Profiles.Select(p => p.Name));

        // martedì 6 ottobre 2026 alle 18:40 → la prima; giovedì 8 alle 19:00 → la seconda; domenica 11 alle 15:30 → la seconda
        Assert.Equal("", catalog.ChooseFor(new DateTime(2026, 10, 6, 18, 40, 0))!.Id);
        Assert.Equal(second.Id, catalog.ChooseFor(new DateTime(2026, 10, 8, 19, 0, 0))!.Id);
        Assert.Equal(second.Id, catalog.ChooseFor(new DateTime(2026, 10, 11, 15, 30, 0))!.Id);
        Assert.Equal("", catalog.ChooseFor(new DateTime(2026, 10, 11, 9, 0, 0))!.Id);
        Assert.Null(catalog.ChooseFor(new DateTime(2026, 10, 7, 9, 0, 0))); // nessuna adunanza vicina

        Assert.True(catalog.Remove(catalog.Find(second.Id)!));
        Assert.False(catalog.Remove(catalog.Profiles[0]));
        Assert.False(Directory.Exists(catalog.FolderOf(second)));
    }

    [Fact]
    public void Switching_profile_keeps_this_pc_and_the_hall()
    {
        var current = new AppSettings { TimerMonitor = @"\\.\DISPLAY2", RemotePin = "4321", WebServerPort = 8091, MidweekDay = DayOfWeek.Tuesday };
        var other = new AppSettings { TimerMonitor = @"\\.\DISPLAY9", RemotePin = "1111", WebServerPort = 9000, MidweekDay = DayOfWeek.Thursday };
        var s = Exchange.ForProfile(other, current);
        Assert.Equal(@"\\.\DISPLAY2", s.TimerMonitor);
        Assert.Equal("4321", s.RemotePin);
        Assert.Equal(8091, s.WebServerPort);
        Assert.Equal(DayOfWeek.Thursday, s.MidweekDay);
    }
}
