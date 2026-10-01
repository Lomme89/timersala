using System.Net;
using AngleSharp.Dom;
using TimerSala.Core.Models;

namespace TimerSala.Core.Wol;

public sealed class WolFetchException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Scarica gli schemi delle adunanze dalla Biblioteca online (wol.jw.org).</summary>
public sealed class WolClient : IDisposable
{
    readonly HttpClient _http;
    readonly bool _ownsClient;

    /// <summary>Se impostata, l'ultima pagina scaricata viene salvata qui (utile per diagnosticare cambi del sito).</summary>
    public string? DiagnosticsFolder { get; set; }

    public WolClient(HttpClient? http = null)
    {
        _ownsClient = http is null;
        _http = http ?? CreateHttpClient();
    }

    static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36 TimerSala/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        return http;
    }

    public async Task<WeekSchedule> FetchWeekAsync(DateOnly anyDayOfWeek, WolLanguage lang, CancellationToken ct = default)
    {
        var monday = WeekMath.MondayOf(anyDayOfWeek);
        var (year, week) = WeekMath.IsoWeek(monday);

        var urls = new[] { lang.MeetingsUrl(year, week), lang.DailyUrl(monday) };
        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                var schedule = await TryFetchFromPageAsync(url, monday, lang, ct);
                if (schedule is not null) return schedule;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { last = ex; }
        }
        throw new WolFetchException(
            last is HttpRequestException
                ? $"Impossibile contattare wol.jw.org: {last.Message}"
                : "Lo schema della settimana non è stato trovato su wol.jw.org (forse non è ancora pubblicato o il sito è cambiato).",
            last);
    }

    async Task<WeekSchedule?> TryFetchFromPageAsync(string url, DateOnly monday, WolLanguage lang, CancellationToken ct)
    {
        var html = await GetStringAsync(url, "meetings", ct);
        var doc = WorkbookParser.ParseDocument(html);
        var baseUri = new Uri(url);

        // 1) Guida Vita e ministero: inclusa nella pagina oppure collegata
        WorkbookParser.Result? mwb = null;
        string? mwbUrl = null;
        var root = WorkbookParser.FindWorkbookRoot(doc);
        if (root is not null)
        {
            mwb = WorkbookParser.Parse(root);
            mwbUrl = url;
        }
        else
        {
            foreach (var link in DocLinks(doc, ".pub-mwb", baseUri))
            {
                var articleHtml = await GetStringAsync(link.ToString(), "mwb", ct);
                var article = WorkbookParser.ParseDocument(articleHtml);
                var r = WorkbookParser.FindWorkbookRoot(article);
                if (r is null) continue;
                mwb = WorkbookParser.Parse(r);
                mwbUrl = link.ToString();
                break;
            }
        }
        if (mwb is null || mwb.Meeting.Parts.Count(p => p.IsTimed) < 4) return null;

        // 2) Torre di Guardia (facoltativa: in caso di errore si usa lo schema standard)
        WatchtowerParser.Result? wt = null;
        try
        {
            var wtBlock = doc.QuerySelector(".pub-w");
            if (wtBlock is not null)
            {
                wt = WatchtowerParser.Parse(wtBlock);
                if (wt.OpeningSong is null || wt.Title is null || wt.Title.Length < 6)
                {
                    var link = DocLinks(doc, ".pub-w", baseUri).FirstOrDefault();
                    if (link is not null)
                    {
                        var wtDoc = WorkbookParser.ParseDocument(await GetStringAsync(link.ToString(), "w", ct));
                        var wtRoot = wtDoc.QuerySelector("#article, article") ?? wtDoc.Body!;
                        var full = WatchtowerParser.Parse(wtRoot);
                        wt = new WatchtowerParser.Result(full.Title ?? wt.Title, full.OpeningSong, full.ClosingSong);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* il fine settimana resta con lo schema standard */ }

        var weekend = MeetingTemplates.DefaultWeekend(wt?.Title, wt?.OpeningSong, wt?.ClosingSong);
        return new WeekSchedule
        {
            WeekStart = monday,
            WeekLabel = mwb.WeekLabel,
            BibleReading = mwb.BibleReading,
            Midweek = mwb.Meeting,
            Weekend = weekend,
            DownloadedMidweek = mwb.Meeting.Clone(),
            DownloadedWeekend = weekend.Clone(),
            SourceUrl = mwbUrl,
            FetchedAt = DateTimeOffset.Now,
        };
    }

    static IEnumerable<Uri> DocLinks(IDocument doc, string containerSelector, Uri baseUri)
    {
        var seen = new HashSet<string>();
        foreach (var container in doc.QuerySelectorAll(containerSelector))
        {
            IEnumerable<IElement> anchors = container.LocalName == "a" ? [container] : container.QuerySelectorAll("a[href]");
            foreach (var a in anchors)
            {
                var href = a.GetAttribute("href");
                if (string.IsNullOrEmpty(href) || !href.Contains("/wol/d/")) continue;
                if (!Uri.TryCreate(baseUri, href, out var uri)) continue;
                var clean = uri.GetLeftPart(UriPartial.Path);
                if (seen.Add(clean)) yield return new Uri(clean);
            }
        }
    }

    async Task<string> GetStringAsync(string url, string tag, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync(ct);
        if (DiagnosticsFolder is not null)
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsFolder);
                await File.WriteAllTextAsync(Path.Combine(DiagnosticsFolder, $"ultimo-{tag}.html"), html, ct);
            }
            catch { /* la diagnostica non deve mai bloccare il download */ }
        }
        return html;
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}
