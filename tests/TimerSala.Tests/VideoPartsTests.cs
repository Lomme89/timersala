using TimerSala.Core.Wol;

namespace TimerSala.Tests;

public class VideoPartsTests
{
    [Fact]
    public void Parts_with_a_video_are_marked()
    {
        const string html = """
            <html><body><article>
            <h1>6-12 OTTOBRE</h1><h2>ISAIA 1-2</h2>
            <h2>TESORI DELLA PAROLA DI DIO</h2>
            <h3>1. Un discorso</h3><p>(10 min)</p>
            <h3>2. Gemme spirituali</h3><p>(10 min) Domande.</p>
            <h2>EFFICACI NEL MINISTERO</h2>
            <h3>3. Iniziare una conversazione</h3><p>(3 min) TESTIMONIANZA INFORMALE.</p>
            <h2>VITA CRISTIANA</h2>
            <h3>4. Bisogni locali</h3><p>(15 min) Discussione.</p><p>Mostra il VIDEO «Restiamo leali».</p>
            <h3>5. Studio biblico di congregazione</h3><p>(30 min)</p>
            </article></body></html>
            """;
        var parts = WorkbookParser.Parse(html)!.Meeting.Parts;
        Assert.True(parts.Single(p => p.Title.StartsWith("4.")).HasVideo);
        Assert.False(parts.Single(p => p.Title.StartsWith("2.")).HasVideo);
        Assert.False(parts.Single(p => p.Title.StartsWith("5.")).HasVideo);
    }

    [Fact]
    public void The_real_schedule_has_no_false_videos()
    {
        var parts = WorkbookParser.Parse(Fixture.Read("meetings-it.html"))!.Meeting.Parts;
        Assert.DoesNotContain(parts, p => p.HasVideo && !p.Title.Contains("ideo") && !(p.Detail ?? "").Contains("ideo"));
    }
}
