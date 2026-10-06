using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using TimerSala.Core.Models;

namespace TimerSala.Core.Wol;

/// <summary>
/// Estrae lo schema dell'adunanza infrasettimanale dalla "Guida per l'adunanza Vita e ministero"
/// pubblicata su wol.jw.org. Il parser si basa sulla struttura (titoli h2/h3, numerazione "1.",
/// durate "(10 min)") e non sul testo, così funziona in tutte le lingue.
/// </summary>
public static partial class WorkbookParser
{
    [GeneratedRegex(@"\(\s*(\d{1,3})\s*min", RegexOptions.IgnoreCase)]
    private static partial Regex DurationRx();

    [GeneratedRegex(@"^\s*(\d{1,2})\s*[\.\)]\s*(.+)$", RegexOptions.Singleline)]
    private static partial Regex NumberedRx();

    /// <summary>«video», «vídeo», «vidéo» in qualsiasi lingua dello schema.</summary>
    [GeneratedRegex(@"\bv[ií]d[eé]o", RegexOptions.IgnoreCase)]
    private static partial Regex VideoRx();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRx();

    [GeneratedRegex(@"\(\s*\d{1,3}\s*min[^\)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex DurationTextRx();

    [GeneratedRegex(@"(?<!\d)(\d{1,3})(?!\d)")]
    private static partial Regex NumberRx();

    static readonly HtmlParser Parser = new();

    public sealed record Result(Meeting Meeting, string? WeekLabel, string? BibleReading);

    public static IDocument ParseDocument(string html) => Parser.ParseDocument(html);

    /// <summary>Restituisce true se l'elemento contiene parti numerate con durata (cioè il contenuto della guida).</summary>
    public static bool LooksLikeWorkbook(IElement root) =>
        root.QuerySelectorAll("h3").Count(h => NumberedRx().IsMatch(Clean(h.TextContent))) >= 3;

    public static Result? Parse(string html)
    {
        var doc = ParseDocument(html);
        var root = FindWorkbookRoot(doc);
        return root is null ? null : Parse(root);
    }

    public static IElement? FindWorkbookRoot(IDocument doc)
    {
        IEnumerable<IElement> candidates =
        [
            .. doc.QuerySelectorAll(".pub-mwb"),
            .. doc.QuerySelectorAll("#article, article, .article"),
            doc.Body!,
        ];
        foreach (var c in candidates)
            if (c is not null && LooksLikeWorkbook(c))
                return c;
        return null;
    }

    public static Result Parse(IElement root)
    {
        var meeting = new Meeting { Kind = MeetingKind.Midweek, Title = "Vita e ministero cristiano" };
        string? weekLabel = Clean(root.QuerySelector("h1")?.TextContent);
        string? bibleReading = null;

        var blocks = root.QuerySelectorAll("h1, h2, h3, p").ToList();
        var section = PartSection.Opening;
        int sectionHeadersSeen = 0;
        bool numberedSeen = false;

        for (int i = 0; i < blocks.Count; i++)
        {
            var el = blocks[i];
            var text = Clean(el.TextContent);
            if (text.Length == 0) continue;

            switch (el.LocalName)
            {
                case "h2":
                {
                    var detected = DetectSection(el, text);
                    if (detected is null && !numberedSeen && sectionHeadersSeen == 0 && bibleReading is null)
                    {
                        bibleReading = text; // h2 dell'intestazione = lettura biblica settimanale
                        continue;
                    }
                    sectionHeadersSeen++;
                    section = detected ?? sectionHeadersSeen switch
                    {
                        1 => PartSection.Treasures,
                        2 => PartSection.Ministry,
                        _ => PartSection.Living,
                    };
                    break;
                }
                case "h3":
                {
                    int? minutes = DurationIn(text);
                    var numbered = NumberedRx().Match(text);
                    if (numbered.Success)
                    {
                        numberedSeen = true;
                        string? detail = null;
                        // la durata di solito è nel primo paragrafo dopo il titolo
                        for (int j = i + 1; j < blocks.Count && blocks[j].LocalName == "p"; j++)
                        {
                            var pText = Clean(blocks[j].TextContent);
                            if (minutes is null && DurationIn(pText) is { } m)
                            {
                                minutes = m;
                                detail = Trim(StripDuration(pText), 140);
                                break;
                            }
                            if (minutes is not null) break;
                        }
                        if (section == PartSection.Opening) section = PartSection.Treasures;
                        int number = int.Parse(numbered.Groups[1].Value);
                        // un video nel testo della parte (fino al titolo successivo): «Mostra il VIDEO», «Riproduci il video»…
                        bool video = false;
                        for (int j = i + 1; j < blocks.Count && blocks[j].LocalName == "p" && !video; j++)
                            video = VideoRx().IsMatch(blocks[j].TextContent);
                        meeting.Parts.Add(new MeetingPart
                        {
                            Title = $"{number}. {StripDuration(numbered.Groups[2].Value)}",
                            Section = section,
                            DurationSeconds = (minutes ?? 0) * 60,
                            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail,
                            HasCounsel = section == PartSection.Ministry || (section == PartSection.Treasures && number == 3),
                            HasVideo = video || VideoRx().IsMatch(numbered.Groups[2].Value),
                        });
                    }
                    else if (minutes is not null)
                    {
                        // commenti introduttivi / conclusivi (spesso insieme al cantico: "Cantico 1 e preghiera | Commenti introduttivi (1 min)")
                        meeting.Parts.Add(new MeetingPart
                        {
                            Title = StripDuration(text),
                            Section = numberedSeen ? PartSection.Closing : PartSection.Opening,
                            DurationSeconds = minutes.Value * 60,
                        });
                    }
                    else if (IsSong(el, text))
                    {
                        meeting.Parts.Add(new MeetingPart
                        {
                            Title = text,
                            Section = numberedSeen ? section : PartSection.Opening,
                            IsSong = true,
                        });
                    }
                    break;
                }
            }
        }

        // Le parti numerate senza durata (raro) restano visibili ma con 0 min: l'utente può correggerle.
        return new Result(meeting, NullIfEmpty(weekLabel), NullIfEmpty(bibleReading));
    }

    static PartSection? DetectSection(IElement h2, string text)
    {
        for (var e = h2; e is not null; e = e.ParentElement)
        {
            var cls = e.ClassName ?? "";
            if (cls.Contains("dc-icon--gem")) return PartSection.Treasures;
            if (cls.Contains("dc-icon--wheat")) return PartSection.Ministry;
            if (cls.Contains("dc-icon--sheep")) return PartSection.Living;
            if (e.LocalName is "article" or "body") break;
        }
        var t = text.ToUpperInvariant();
        if (t.Contains("TESORI") || t.Contains("TREASURES") || t.Contains("TESOROS") || t.Contains("JOYAUX") || t.Contains("SCHÄTZE") || t.Contains("TESOUROS"))
            return PartSection.Treasures;
        if (t.Contains("MINISTER") || t.Contains("MINISTRY") || t.Contains("PREDICACIÓN") || t.Contains("PREDICA") || t.Contains("DIENST"))
            return PartSection.Ministry;
        if (t.Contains("VITA CRISTIANA") || t.Contains("LIVING AS CHRISTIANS") || t.Contains("VIDA CRISTIANA") || t.Contains("VIE CHRÉTIENNE") || t.Contains("CHRISTEN") || t.Contains("VIVER COMO CRISTÃOS"))
            return PartSection.Living;
        return null;
    }

    static bool IsSong(IElement el, string text)
    {
        if (el.QuerySelector("a.pub-sjj, a[href*='sjj']") is not null) return true;
        return MeetingTemplates.SongWords.Any(w => text.StartsWith(w, StringComparison.OrdinalIgnoreCase))
               && NumberRx().IsMatch(text);
    }

    internal static int? DurationIn(string text)
    {
        var m = DurationRx().Match(text);
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    internal static string StripDuration(string text) =>
        Clean(DurationTextRx().Replace(text, " ")).Trim(' ', '|', '-', '–');

    internal static string Clean(string? s) =>
        s is null ? "" : SpaceRx().Replace(s.Replace(' ', ' '), " ").Trim();

    static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";

    static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>Estrae i numeri dei cantici da un testo (es. "CANTICO 12").</summary>
    internal static IEnumerable<int> SongNumbers(IElement root)
    {
        foreach (var a in root.QuerySelectorAll("a.pub-sjj, a[href*='sjj']"))
            if (NumberRx().Match(a.TextContent) is { Success: true } m)
                yield return int.Parse(m.Groups[1].Value);
    }

    internal static IEnumerable<int> SongNumbersFromText(string text)
    {
        foreach (var w in MeetingTemplates.SongWords)
        {
            var rx = new Regex($@"\b{Regex.Escape(w)}\s+(\d{{1,3}})\b", RegexOptions.IgnoreCase);
            foreach (Match m in rx.Matches(text))
                yield return int.Parse(m.Groups[1].Value);
        }
    }
}
