using TimerSala.Core.Models;
using TimerSala.Core.Wol;

namespace TimerSala.Tests;

public class TemplatesTests
{
    [Fact]
    public void Overseer_visit_replaces_congregation_bible_study()
    {
        var mw = WorkbookParser.Parse(Fixture.Read("meetings-it.html"))!.Meeting;
        var co = MeetingTemplates.ApplyOverseerVisit(mw);

        Assert.DoesNotContain(co.Parts, p => p.Title.Contains("Studio biblico di congregazione"));
        var timed = co.Parts.Where(p => p.IsTimed).ToList();
        Assert.Equal("Commenti conclusivi", timed[^2].Title);
        Assert.Equal(180, timed[^2].DurationSeconds);
        Assert.Equal(MeetingTemplates.OverseerTalkTitle, timed[^1].Title);
        Assert.Equal(1800, timed[^1].DurationSeconds);
        Assert.True(co.Parts[^1].IsSong);

        // l'originale non viene modificato e l'operazione è idempotente
        Assert.Contains(mw.Parts, p => p.Title.Contains("Studio biblico di congregazione"));
        Assert.Equal(co.Parts.Count, MeetingTemplates.ApplyOverseerVisit(co).Parts.Count);
    }

    [Fact]
    public void Overseer_visit_shortens_watchtower_study()
    {
        var co = MeetingTemplates.ApplyOverseerVisit(MeetingTemplates.DefaultWeekend("Titolo", 1, 2));
        var timed = co.Parts.Where(p => p.IsTimed).ToList();
        Assert.Equal(3, timed.Count);
        Assert.Equal(1800, timed[1].DurationSeconds);
        Assert.Equal(MeetingTemplates.OverseerTalkTitle, timed[2].Title);
        Assert.Equal("Cantico 2 e preghiera", co.Parts[^1].Title);
    }

    [Theory]
    [InlineData(2026, 10, 5, 2026, 10, 5)]
    [InlineData(2026, 10, 11, 2026, 10, 5)]
    [InlineData(2026, 10, 8, 2026, 10, 5)]
    public void Monday_of_week(int y, int m, int d, int ey, int em, int ed) =>
        Assert.Equal(new DateOnly(ey, em, ed), WeekMath.MondayOf(new DateOnly(y, m, d)));

    [Fact]
    public void Iso_week_number() => Assert.Equal((2026, 41), WeekMath.IsoWeek(new DateOnly(2026, 10, 5)));
}

public class MeetingPlanTests
{
    [Fact]
    public void Songs_share_the_time_left_over()
    {
        var m = new TimerSala.Core.Models.Meeting
        {
            Parts =
            [
                new() { Title = "Cantico e preghiera", IsSong = true },
                new() { Title = "Discorso pubblico", DurationSeconds = 30 * 60 },
                new() { Title = "Cantico 79", IsSong = true },
                new() { Title = "Studio Torre di Guardia", DurationSeconds = 60 * 60 },
                new() { Title = "Cantico 84 e preghiera", IsSong = true },
            ],
        };
        var starts = TimerSala.Core.Models.MeetingPlan.PlannedStarts(m, new DateTime(2026, 10, 4, 10, 0, 0), 105);
        Assert.Equal(["10:00", "10:05", "10:35", "10:40", "11:40"], starts.Select(s => s.ToString("HH:mm")));
    }
}
