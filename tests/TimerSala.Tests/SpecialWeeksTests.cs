using TimerSala.Core.Models;
using TimerSala.Core.Storage;

namespace TimerSala.Tests;

public class SpecialWeeksTests
{
    static readonly DateOnly Monday = new(2027, 3, 15);

    static WeekSchedule Week() => new()
    {
        WeekStart = Monday,
        Midweek = MeetingTemplates.DefaultMidweek(),
        Weekend = MeetingTemplates.DefaultWeekend(),
        DownloadedMidweek = MeetingTemplates.DefaultMidweek(),
        DownloadedWeekend = MeetingTemplates.DefaultWeekend(),
    };

    [Fact]
    public void Memorial_on_a_weekday_replaces_the_midweek_meeting_with_its_time_and_goes_away_when_removed()
    {
        var s = new AppSettings { MidweekDay = DayOfWeek.Wednesday, MidweekTime = new(19, 0) };
        s.Memorials.Add(new MemorialDate(new DateOnly(2027, 3, 18), new TimeOnly(19, 30))); // giovedì
        var week = Week();

        Assert.True(SpecialWeeks.Apply(week, s));
        Assert.True(MeetingTemplates.IsMemorial(week.Midweek));
        Assert.False(MeetingTemplates.IsMemorial(week.Weekend));
        Assert.Equal(new DateTime(2027, 3, 18, 19, 30, 0), s.StartOf(MeetingKind.Midweek, Monday));
        Assert.False(SpecialWeeks.Apply(week, s)); // già applicata

        s.Memorials.Clear();
        Assert.True(SpecialWeeks.Apply(week, s));
        Assert.False(MeetingTemplates.IsMemorial(week.Midweek));
        Assert.Equal(new DateTime(2027, 3, 17, 19, 0, 0), s.StartOf(MeetingKind.Midweek, Monday));
    }

    [Fact]
    public void Memorial_on_saturday_replaces_the_weekend_meeting()
    {
        var s = new AppSettings();
        s.Memorials.Add(new MemorialDate(new DateOnly(2027, 3, 20), new TimeOnly(18, 0)));
        var week = Week();
        SpecialWeeks.Apply(week, s);
        Assert.True(MeetingTemplates.IsMemorial(week.Weekend));
        Assert.False(MeetingTemplates.IsMemorial(week.Midweek));
    }

    [Fact]
    public void Assembly_week_has_no_meetings_and_special_talk_renames_the_public_talk()
    {
        var s = new AppSettings { AssemblyWeeks = [Monday], SpecialTalkWeeks = [Monday] };
        Assert.False(s.HasMeeting(MeetingKind.Midweek, Monday));
        Assert.NotNull(SpecialWeeks.NoMeetingNotice(s, Monday));

        var week = Week();
        SpecialWeeks.Apply(week, s);
        Assert.Equal(MeetingTemplates.SpecialTalkTitle, week.Weekend.Parts[1].Title);
        s.SpecialTalkWeeks.Clear();
        SpecialWeeks.Apply(week, s);
        Assert.Equal("Discorso pubblico", week.Weekend.Parts[1].Title);
    }
}

public class FreeEventTests
{
    [Fact]
    public void Event_becomes_a_meeting_and_recent_ones_are_remembered_without_duplicates()
    {
        var wedding = new FreeEvent("Discorso di matrimonio", [new("Discorso", 30), new("", 0)]);
        var m = wedding.ToMeeting(MeetingKind.Weekend);
        Assert.Single(m.Parts);
        Assert.Equal(1800, m.Parts[0].DurationSeconds);

        var recent = FreeEvent.Remember([], wedding);
        recent = FreeEvent.Remember(recent, new FreeEvent("Funerale", [new("Discorso", 20)]));
        recent = FreeEvent.Remember(recent, wedding with { Title = "discorso di matrimonio" });
        Assert.Equal(2, recent.Count);
        Assert.Equal("discorso di matrimonio", recent[0].Title);
    }
}
