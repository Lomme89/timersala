using TimerSala.Core.Models;
using TimerSala.Core.Wol;

namespace TimerSala.Tests;

public class WorkbookParserTests
{
    [Fact]
    public void Parses_italian_meetings_page()
    {
        var r = WorkbookParser.Parse(Fixture.Read("meetings-it.html"));
        Assert.NotNull(r);
        Assert.Equal("5-11 OTTOBRE", r.WeekLabel);
        Assert.Equal("ISAIA 58-59", r.BibleReading);

        var parts = r.Meeting.Parts;
        var timed = parts.Where(p => p.IsTimed).ToList();
        Assert.Equal(10, timed.Count);

        Assert.Equal("Cantico 80 e preghiera | Commenti introduttivi", timed[0].Title);
        Assert.Equal(PartSection.Opening, timed[0].Section);
        Assert.Equal(60, timed[0].DurationSeconds);

        Assert.Equal("1. “Chiama il sabato una vera delizia”", timed[1].Title);
        Assert.Equal(PartSection.Treasures, timed[1].Section);
        Assert.Equal(600, timed[1].DurationSeconds);

        Assert.Equal("3. Lettura biblica", timed[3].Title);
        Assert.Equal(240, timed[3].DurationSeconds);
        Assert.True(timed[3].HasCounsel);
        Assert.StartsWith("Isa 58:1-14", timed[3].Detail);

        Assert.All(timed.Where(p => p.Section == PartSection.Ministry), p => Assert.True(p.HasCounsel));
        Assert.Equal([180, 240, 300], timed.Where(p => p.Section == PartSection.Ministry).Select(p => p.DurationSeconds));

        var song = Assert.Single(parts, p => p.IsSong);
        Assert.Equal("Cantico 34", song.Title);
        Assert.Equal(PartSection.Living, song.Section);

        Assert.Equal("8. Studio biblico di congregazione", timed[8].Title);
        Assert.Equal(1800, timed[8].DurationSeconds);
        Assert.False(timed[8].HasCounsel);

        Assert.Equal("Commenti conclusivi | Cantico 31 e preghiera", timed[9].Title);
        Assert.Equal(PartSection.Closing, timed[9].Section);
        Assert.Equal(180, timed[9].DurationSeconds);

        Assert.Equal(85 * 60, r.Meeting.TotalTimedSeconds);
    }

    [Fact]
    public void Parses_english_article_with_duration_in_heading()
    {
        var r = WorkbookParser.Parse(Fixture.Read("mwb-en-article.html"));
        Assert.NotNull(r);
        var timed = r.Meeting.Parts.Where(p => p.IsTimed).ToList();
        Assert.Equal(10, timed.Count);
        Assert.Equal("1. “Call the Sabbath an Exquisite Delight”", timed[1].Title);
        Assert.Equal(600, timed[1].DurationSeconds);
        Assert.Equal(PartSection.Living, timed[7].Section);
    }

    [Fact]
    public void Returns_null_when_page_has_no_workbook()
    {
        Assert.Null(WorkbookParser.Parse("<html><body><h1>Nessun contenuto</h1></body></html>"));
    }

    [Fact]
    public void Section_falls_back_to_order_when_no_icons_or_known_words()
    {
        var html = """
            <article><h1>X</h1><h2>R</h2>
            <h3>Song 1 | Opening Comments (1 min)</h3>
            <h2>AAA</h2><h3>1. A</h3><p>(10 min)</p><h3>2. B</h3><p>(10 min)</p><h3>3. C</h3><p>(4 min)</p>
            <h2>BBB</h2><h3>4. D</h3><p>(3 min)</p>
            <h2>CCC</h2><h3>5. E</h3><p>(15 min)</p>
            </article>
            """;
        var r = WorkbookParser.Parse(html)!;
        var timed = r.Meeting.Parts.Where(p => p.IsTimed).ToList();
        Assert.Equal(PartSection.Treasures, timed[1].Section);
        Assert.Equal(PartSection.Ministry, timed[4].Section);
        Assert.Equal(PartSection.Living, timed[5].Section);
    }
}
