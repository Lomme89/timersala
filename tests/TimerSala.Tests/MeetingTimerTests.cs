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
    public void Undo_stop_resumes_counting_the_seconds_in_between()
    {
        var (t, clock) = Create();
        t.Select(1);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(3));
        t.Stop();
        Assert.Equal(2, t.SelectedIndex);
        Assert.True(t.CanUndoStop);

        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.True(t.UndoStop());

        Assert.True(t.IsRunning);
        Assert.Equal(1, t.RunningIndex);
        Assert.Equal(1, t.SelectedIndex);
        Assert.Null(t.ActualFor(1));
        Assert.Equal("06:56", t.GetSnapshot().Display);
        Assert.False(t.CanUndoStop);
    }

    [Fact]
    public void Undo_stop_restores_time_of_a_resumed_part()
    {
        var (t, clock) = Create();
        t.Select(1);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(2));
        t.Stop();
        t.Select(1);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(1));
        t.Stop();
        Assert.Equal(TimeSpan.FromMinutes(3), t.ActualFor(1));

        t.UndoStop();
        Assert.Equal(TimeSpan.FromMinutes(2), t.ActualFor(1));
        clock.Advance(TimeSpan.FromMinutes(1));
        t.Stop();
        Assert.Equal(TimeSpan.FromMinutes(4), t.ActualFor(1));
    }

    [Fact]
    public void Undo_stop_expires_and_is_lost_after_another_start()
    {
        var (t, clock) = Create();
        t.Select(1);
        t.Start();
        t.Stop();
        clock.Advance(MeetingTimer.UndoStopWindow);
        Assert.False(t.CanUndoStop);
        Assert.False(t.UndoStop());

        t.Start();
        t.Stop();
        t.Start();
        Assert.False(t.CanUndoStop);
        Assert.False(t.UndoStop());
        Assert.Equal(3, t.RunningIndex);
    }

    [Fact]
    public void Undo_stop_works_for_counsel()
    {
        var (t, clock) = Create();
        t.StartCounsel();
        clock.Advance(TimeSpan.FromSeconds(20));
        t.Stop();
        clock.Advance(TimeSpan.FromSeconds(2));
        t.UndoStop();
        var s = t.GetSnapshot();
        Assert.Equal(TimerMode.Counsel, s.Mode);
        Assert.Equal("00:38", s.Display);
    }

    [Fact]
    public void Skip_counts_as_zero_and_moves_on()
    {
        var (t, _) = Create();
        t.Select(2);
        int duration = t.Meeting.Parts[2].DurationSeconds;
        Assert.True(t.Skip(2));
        Assert.True(t.IsSkipped(2));
        Assert.Equal(t.NextTimedAfter(2), t.SelectedIndex);
        Assert.Equal(-duration, t.GetSnapshot().DelaySeconds);
    }

    [Fact]
    public void Move_after_next_swaps_parts_and_their_times()
    {
        var (t, clock) = Create();
        int a = 1, b = t.NextTimedAfter(1);
        string first = t.Meeting.Parts[a].Title, second = t.Meeting.Parts[b].Title;
        t.Select(a);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(2));
        t.Stop();
        Assert.True(t.MoveAfterNext(a));
        Assert.Equal(second, t.Meeting.Parts[a].Title);
        Assert.Equal(first, t.Meeting.Parts[b].Title);
        Assert.Null(t.ActualFor(a));
        Assert.Equal(TimeSpan.FromMinutes(2), t.ActualFor(b));
    }

    [Fact]
    public void Running_part_can_be_edited_but_not_moved_or_skipped()
    {
        var (t, clock) = Create();
        t.Select(1);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(t.Skip(1));
        Assert.False(t.MoveAfterNext(1));
        Assert.True(t.EditPart(1, "Discorso dell'ospite", 12 * 60));
        Assert.Equal("Discorso dell'ospite", t.GetSnapshot().Title);
        Assert.Equal("11:00", t.GetSnapshot().Display);
    }

    [Fact]
    public void Restore_undoes_an_edit_without_losing_the_running_time()
    {
        var (t, clock) = Create();
        t.Select(1);
        t.Start();
        var snap = t.Snapshot();
        string title = t.Meeting.Parts[1].Title;
        t.EditPart(1, "Altro", 5 * 60);
        t.Skip(t.NextTimedAfter(1));
        clock.Advance(TimeSpan.FromSeconds(30));
        t.Restore(snap);
        Assert.Equal(title, t.Meeting.Parts[1].Title);
        Assert.True(t.IsRunning);
        Assert.Equal(1, t.RunningIndex);
        Assert.Null(t.ActualFor(t.NextTimedAfter(1)));
        Assert.Equal(30, (int)t.GetSnapshot().ElapsedSeconds);
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
    public void Countdown_reports_the_start_time()
    {
        var (t, clock) = Create();
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 19, 0, 0, TimeSpan.Zero), t.GetSnapshot().StartsAt);
    }

    [Fact]
    public void Preview_shows_a_countdown_for_a_while_then_goes_back()
    {
        var (t, clock) = Create();
        clock.Advance(TimeSpan.FromHours(2));                      // adunanza già iniziata: niente countdown vero
        Assert.Equal(TimerPhase.Idle, t.GetSnapshot().Phase);

        t.PreviewCountdown(TimeSpan.FromSeconds(272), TimeSpan.FromSeconds(12));
        var s = t.GetSnapshot();
        Assert.Equal(TimerMode.Countdown, s.Mode);
        Assert.Equal("04:32", s.Display);
        Assert.True(t.IsPreviewingCountdown);

        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal("04:27", t.GetSnapshot().Display);

        clock.Advance(TimeSpan.FromSeconds(8));
        Assert.Equal(TimerPhase.Idle, t.GetSnapshot().Phase);
        Assert.False(t.IsPreviewingCountdown);
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
