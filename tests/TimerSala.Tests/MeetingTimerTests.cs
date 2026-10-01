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
