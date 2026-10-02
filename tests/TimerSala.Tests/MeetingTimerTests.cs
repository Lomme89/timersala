using TimerSala.Core.Models;
using TimerSala.Core.Timing;

namespace TimerSala.Tests;

public class MeetingTimerTests
{
    static (MeetingTimer, FakeClock) Create()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 7, 19, 0, 0, TimeSpan.Zero));
        var t = new MeetingTimer(clock);
        t.LoadMeeting(MeetingTemplates.DefaultMidweek());
        return (t, clock);
    }

    [Fact]
    public void Idle_shows_first_timed_part_as_next()
    {
        var (t, _) = Create();
        var s = t.GetSnapshot();
        Assert.Equal(TimerPhase.Idle, s.Phase);
        Assert.Equal(0, t.SelectedIndex);
        Assert.StartsWith("Cantico e preghiera", s.NextTitle);
    }

    [Fact]
    public void Countdown_phases_and_overtime()
    {
        var (t, clock) = Create();
        t.Select(1); // 10 min
        t.Start();
        Assert.Equal("10:00", t.GetSnapshot().Display);

        clock.Advance(TimeSpan.FromSeconds(0.4));
        Assert.Equal("10:00", t.GetSnapshot().Display);

        clock.Advance(TimeSpan.FromMinutes(9) - TimeSpan.FromSeconds(0.4));
        var s = t.GetSnapshot();
        Assert.Equal(TimerPhase.Warning, s.Phase);
        Assert.Equal("01:00", s.Display);

        clock.Advance(TimeSpan.FromSeconds(75));
        s = t.GetSnapshot();
        Assert.Equal(TimerPhase.Overtime, s.Phase);
        Assert.Equal("+00:15", s.Display);
        Assert.Equal(15, s.DelaySeconds);
    }

    [Fact]
    public void Stop_records_time_and_advances_skipping_songs()
    {
        var (t, clock) = Create();
        int songIndex = t.Meeting.Parts.FindIndex(p => p.IsSong);
        t.Select(songIndex - 1);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(6));
        t.Stop();

        Assert.Equal(TimeSpan.FromMinutes(6), t.ActualFor(songIndex - 1));
        Assert.Equal(songIndex + 1, t.SelectedIndex);
        Assert.Equal(60, t.GetSnapshot().DelaySeconds);
    }

    [Fact]
    public void Restart_resumes_previous_elapsed_time()
    {
        var (t, clock) = Create();
        t.Select(1);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(2));
        t.Stop();
        t.Select(1);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal("07:00", t.GetSnapshot().Display);
    }

    [Fact]
    public void Counsel_timer_does_not_change_selection()
    {
        var (t, clock) = Create();
        t.Select(3);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(4));
        t.Stop();
        int selected = t.SelectedIndex;
        t.StartCounsel();
        var s = t.GetSnapshot();
        Assert.Equal(TimerMode.Counsel, s.Mode);
        Assert.Equal("01:00", s.Display);
        t.Stop();
        Assert.Equal(selected, t.SelectedIndex);
        Assert.Equal(TimerPhase.Idle, t.GetSnapshot().Phase);
    }

    [Fact]
    public void Adjust_while_running_does_not_change_plan()
    {
        var (t, _) = Create();
        t.Select(1);
        t.Start();
        t.AdjustTarget(60);
        Assert.Equal("11:00", t.GetSnapshot().Display);
        Assert.Equal(600, t.Meeting.Parts[1].DurationSeconds);
    }

    [Theory]
    [InlineData(65.0, "01:05")]
    [InlineData(0.2, "00:01")]
    [InlineData(0.0, "00:00")]
    [InlineData(-0.5, "+00:00")]
    [InlineData(-61.0, "+01:01")]
    public void Formats_remaining(double secs, string expected) =>
        Assert.Equal(expected, TimerSnapshot.FormatRemaining(secs));
}

public class CountdownTests
{
    static (MeetingTimer, FakeClock) Create()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 7, 18, 50, 0, TimeSpan.Zero));
        var t = new MeetingTimer(clock) { CountdownLeadSeconds = 300 };
        t.LoadMeeting(MeetingTemplates.DefaultMidweek());
        t.MeetingStart = new DateTimeOffset(2026, 10, 7, 19, 0, 0, TimeSpan.Zero);
        return (t, clock);
    }

    [Fact]
    public void Countdown_appears_only_in_the_last_minutes()
    {
        var (t, clock) = Create();
        Assert.Equal(TimerPhase.Idle, t.GetSnapshot().Phase);

        clock.Advance(TimeSpan.FromMinutes(5.5));
        var s = t.GetSnapshot();
        Assert.Equal(TimerMode.Countdown, s.Mode);
        Assert.Equal("04:30", s.Display);
        Assert.Equal(TimerPhase.Normal, s.Phase);

        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(TimerPhase.Warning, t.GetSnapshot().Phase);

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(TimerPhase.Idle, t.GetSnapshot().Phase);
    }

    [Fact]
    public void Forced_countdown_shows_early_and_ends_when_a_part_starts()
    {
        var (t, _) = Create();
        t.ForceCountdown = true;
        var s = t.GetSnapshot();
        Assert.Equal(TimerMode.Countdown, s.Mode);
        Assert.Equal("10:00", s.Display);

        t.Start();
        Assert.Equal(TimerMode.Part, t.GetSnapshot().Mode);
        t.Stop();
        Assert.False(t.ForceCountdown);
    }

    [Fact]
    public void Start_time_is_computed_from_settings()
    {
        var s = new TimerSala.Core.Storage.AppSettings { MidweekDay = DayOfWeek.Thursday, MidweekTime = new TimeOnly(19, 30) };
        Assert.Equal(new DateTime(2026, 10, 8, 19, 30, 0), s.StartOf(MeetingKind.Midweek, new DateOnly(2026, 10, 5)));
        Assert.Equal(new DateTime(2026, 10, 11, 10, 0, 0), s.StartOf(MeetingKind.Weekend, new DateOnly(2026, 10, 5)));
    }
}

public class AdaptiveAndRestoreTests
{
    static (MeetingTimer, FakeClock, int cbs) Create()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 7, 19, 0, 0, TimeSpan.Zero));
        var t = new MeetingTimer(clock);
        var m = MeetingTemplates.DefaultMidweek();
        t.LoadMeeting(m);
        int cbs = m.Parts.FindIndex(p => p.Title.Contains("Studio biblico"));
        return (t, clock, cbs);
    }

    [Fact]
    public void Adaptive_study_absorbs_accumulated_delay()
    {
        var (t, clock, cbs) = Create();
        t.SetAdaptiveParts([cbs]);
        t.Select(1); t.Start(); clock.Advance(TimeSpan.FromMinutes(13)); t.Stop(); // +3 min di ritardo

        Assert.Equal(27 * 60, t.AdaptedTargetFor(cbs));
        t.Select(cbs);
        Assert.Equal("27:00", t.GetSnapshot().Display);
        t.Start();
        Assert.Equal("27:00", t.GetSnapshot().Display);
        clock.Advance(TimeSpan.FromMinutes(27));
        t.Stop();
        Assert.Equal(0, t.GetSnapshot().DelaySeconds); // l'adunanza torna in orario
    }

    [Fact]
    public void Adaptive_limits_and_rounding()
    {
        Assert.Equal(15 * 60, MeetingTimer.Adapt(30 * 60, 40 * 60));   // al massimo metà
        Assert.Equal(30 * 60, MeetingTimer.Adapt(30 * 60, -20 * 60));  // in anticipo: mai più lunga del previsto
        Assert.Equal(1800 - 75, MeetingTimer.Adapt(1800, 70));          // arrotondato a 15 s
    }

    [Fact]
    public void Non_adaptive_parts_keep_planned_duration()
    {
        var (t, clock, cbs) = Create();
        t.Select(1); t.Start(); clock.Advance(TimeSpan.FromMinutes(13)); t.Stop();
        Assert.Null(t.AdaptedTargetFor(cbs));
    }

    [Fact]
    public void Restore_counting_downtime_or_not()
    {
        var (t, clock, _) = Create();
        t.Select(1); t.Start(); clock.Advance(TimeSpan.FromMinutes(4)); t.Stop();
        t.Select(2); t.Start(); clock.Advance(TimeSpan.FromMinutes(2));
        var saved = t.ExportState();

        clock.Advance(TimeSpan.FromMinutes(3)); // programma chiuso per 3 minuti

        var a = new MeetingTimer(clock); a.LoadMeeting(MeetingTemplates.DefaultMidweek());
        a.ImportState(saved, countDowntime: true);
        Assert.True(a.IsRunning);
        Assert.Equal(2, a.RunningIndex);
        Assert.Equal("05:00", a.GetSnapshot().Display);           // 10 - (2 + 3)
        Assert.Equal(TimeSpan.FromMinutes(4), a.ActualFor(1));

        var b = new MeetingTimer(clock); b.LoadMeeting(MeetingTemplates.DefaultMidweek());
        b.ImportState(saved, countDowntime: false);
        Assert.Equal("08:00", b.GetSnapshot().Display);           // riparte da dove era
    }
}
