using AngleSharp.Dom;

namespace TimerSala.Core.Wol;

/// <summary>Estrae titolo e cantici dell'articolo di studio della Torre di Guardia.</summary>
public static class WatchtowerParser
{
    public sealed record Result(string? Title, int? OpeningSong, int? ClosingSong);

    public static Result Parse(IElement root)
    {
        var title = WorkbookParser.Clean(root.QuerySelector("h1")?.TextContent);
        if (title.Length == 0)
            title = WorkbookParser.Clean(root.QuerySelector("h2, h3, a")?.TextContent);

        var songs = WorkbookParser.SongNumbers(root).ToList();
        if (songs.Count == 0)
            songs = WorkbookParser.SongNumbersFromText(WorkbookParser.Clean(root.TextContent)).ToList();

        return new Result(
            title.Length == 0 ? null : title,
            songs.Count > 0 ? songs[0] : null,
            songs.Count > 1 ? songs[^1] : null);
    }
}
