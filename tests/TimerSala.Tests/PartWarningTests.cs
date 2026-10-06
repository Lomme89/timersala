using TimerSala.Core.Models;
using TimerSala.Core.Storage;
using TimerSala.Core.Timing;

namespace TimerSala.Tests;

public class PartWarningTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 7, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parts_are_classified_as_students_talks_or_others()
    {
        var m = MeetingTemplates.DefaultMidweek();
        Assert.Equal(PartCategory.Talk, PartCategories.Classify(m.Parts[1]));      // 1. Discorso
        Assert.Equal(PartCategory.Other, PartCategories.Classify(m.Parts[2]));     // Gemme spirituali
        Assert.Equal(PartCategory.Student, PartCategories.Classify(m.Parts[3]));   // Lettura biblica
        Assert.Equal(PartCategory.Talk, PartCategories.Classify(MeetingTemplates.DefaultWeekend().Parts[1]));
    }

    [Fact]
    public void Each_kind_of_part_turns_yellow_at_its_own_time()
    {
        var s = new AppSettings { WarningSeconds = 60, WarningStudents = new PartWarning(30), WarningTalks = new PartWarning(10, Percent: true) };
        var clock = new FakeClock(T0);
        var t = new MeetingTimer(clock) { PartWarning = (p, target) => s.WarningFor(PartCategories.Classify(p)).SecondsFor(target) };
        t.LoadMeeting(MeetingTemplates.DefaultMidweek());

        // discorso di 10 minuti: giallo al 10 %, cioè a 60 secondi dalla fine
        t.Select(1);
        t.Start();
        clock.Advance(TimeSpan.FromSeconds(600 - 61));
        Assert.Equal(TimerPhase.Normal, t.GetSnapshot().Phase);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimerPhase.Warning, t.GetSnapshot().Phase);
        t.Stop();

        // lettura biblica (studente, 4 minuti): giallo a 30 secondi
        t.Select(3);
        t.Start();
        clock.Advance(TimeSpan.FromSeconds(240 - 45));
        Assert.Equal(TimerPhase.Normal, t.GetSnapshot().Phase);
        clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(TimerPhase.Warning, t.GetSnapshot().Phase);
    }

    [Fact]
    public void Without_own_rules_everything_follows_the_old_setting()
    {
        var s = new AppSettings { WarningSeconds = 90 };
        Assert.Equal(90, s.WarningFor(PartCategory.Student).SecondsFor(240));
        Assert.Equal(90, s.WarningFor(PartCategory.Talk).SecondsFor(1800));
    }
}
