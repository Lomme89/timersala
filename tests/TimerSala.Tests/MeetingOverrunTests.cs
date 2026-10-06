using TimerSala.Core.Models;
using TimerSala.Core.Wol;

namespace TimerSala.Tests;

public class MeetingOverrunTests
{
    [Fact]
    public void Standard_meetings_fit_in_105_minutes()
    {
        Assert.Equal(0, MeetingPlan.OverrunMinutes(MeetingTemplates.DefaultMidweek(), 105));
        Assert.Equal(0, MeetingPlan.OverrunMinutes(MeetingTemplates.DefaultWeekend(), 105));
        var downloaded = WorkbookParser.Parse(Fixture.Read("meetings-it.html"))!.Meeting;
        Assert.Equal(0, MeetingPlan.OverrunMinutes(downloaded, 105));
    }

    [Fact]
    public void Longer_parts_are_reported_with_the_extra_minutes()
    {
        var m = MeetingTemplates.DefaultWeekend();
        m.Parts[1].DurationSeconds = 33 * 60; // discorso pubblico di 33 minuti
        Assert.Equal(3, MeetingPlan.OverrunMinutes(m, 105));
        // le canzoni scritte nel titolo di una parte contano come cantici
        Assert.Equal(105 * 60 + 3 * 60, MeetingPlan.EstimatedSeconds(m));
    }
}

public class TrainingMeetingTests
{
    [Fact]
    public void Training_meeting_is_ten_times_shorter_and_leaves_the_original_alone()
    {
        var original = MeetingTemplates.DefaultMidweek();
        var training = TrainingMeeting.From(original);

        Assert.Equal(original.Parts.Count, training.Parts.Count);
        Assert.Equal(60, training.Parts[1].DurationSeconds);        // discorso di 10 minuti → 1 minuto
        Assert.Equal(15, training.Parts[0].DurationSeconds);        // 1 minuto → minimo 15 secondi
        Assert.Equal(180, training.Parts[^2].DurationSeconds);      // studio di 30 minuti → 3 minuti
        Assert.StartsWith("Prova", training.Title);
        Assert.Equal(600, original.Parts[1].DurationSeconds);
    }
}
