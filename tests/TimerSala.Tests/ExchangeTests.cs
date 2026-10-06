using TimerSala.Core.Models;
using TimerSala.Core.Storage;

namespace TimerSala.Tests;

public class ExchangeTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "timersala-exchange-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Backup_carries_settings_and_edited_weeks_to_another_pc()
    {
        var home = new DataStore(Path.Combine(_dir, "casa"));
        var edited = new WeekSchedule { WeekStart = new DateOnly(2026, 10, 12), Midweek = MeetingTemplates.DefaultMidweek(), EditedManually = true };
        edited.Midweek.Parts[1].Title = "Discorso speciale";
        home.SaveWeek(edited);
        home.SaveWeek(new WeekSchedule { WeekStart = new DateOnly(2026, 10, 19) }); // scaricata: non serve nel backup
        var settings = new AppSettings { MidweekDay = DayOfWeek.Tuesday, MessagePresets = ["Concludi"], TimerMonitor = @"\\.\DISPLAY9" };

        var path = Path.Combine(_dir, "backup" + ExchangeFile.Extension);
        Exchange.Write(path, Exchange.Backup(home, settings, "2.1.0"));
        var file = Exchange.Read(path);
        Assert.True(file.IsBackup);
        Assert.Single(file.Weeks);

        var hall = new DataStore(Path.Combine(_dir, "sala"));
        var hallSettings = new AppSettings { TimerMonitor = @"\\.\DISPLAY2", StartWithWindows = true };
        var merged = Exchange.MergeSettings(file.Settings!, hallSettings);
        Assert.Equal(1, Exchange.ImportWeeks(hall, file));

        Assert.Equal(DayOfWeek.Tuesday, merged.MidweekDay);
        Assert.Equal(["Concludi"], merged.MessagePresets);
        Assert.Equal(@"\\.\DISPLAY2", merged.TimerMonitor); // lo schermo della sala resta quello di questo PC
        Assert.True(merged.StartWithWindows);
        Assert.Equal("Discorso speciale", hall.LoadWeek(new DateOnly(2026, 10, 14))!.Midweek.Parts[1].Title);
    }

    [Fact]
    public void A_single_week_replaces_the_same_week()
    {
        var store = new DataStore(Path.Combine(_dir, "pc"));
        store.SaveWeek(new WeekSchedule { WeekStart = new DateOnly(2026, 10, 12) });
        var week = new WeekSchedule { WeekStart = new DateOnly(2026, 10, 12), Weekend = MeetingTemplates.DefaultWeekend("Prova"), EditedManually = true };
        var path = Path.Combine(_dir, "settimana" + ExchangeFile.Extension);
        Exchange.Write(path, Exchange.Week(week));

        var file = Exchange.Read(path);
        Assert.False(file.IsBackup);
        Exchange.ImportWeeks(store, file);
        Assert.True(store.LoadWeek(new DateOnly(2026, 10, 12))!.EditedManually);
    }

    [Fact]
    public void Other_files_are_refused_with_a_clear_message()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "altro.timersala");
        File.WriteAllText(path, "{ \"foo\": 1 }");
        var ex = Assert.Throws<InvalidDataException>(() => Exchange.Read(path));
        Assert.Contains("non è un backup", ex.Message);
        File.WriteAllText(path, "non è json");
        Assert.Throws<InvalidDataException>(() => Exchange.Read(path));
    }
}
