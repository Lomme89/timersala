using System.Net;
using TimerSala.Core.Models;
using TimerSala.Core.Storage;

namespace TimerSala.Core.Wol;

public sealed record SyncResult(int Updated, int KeptEdited, IReadOnlyList<DateOnly> UpdatedWeeks, IReadOnlyList<DateOnly> KeptWeeks, string? Error)
{
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            parts.Add(Updated switch
            {
                0 => "Nessuna settimana scaricata",
                1 => "1 settimana aggiornata",
                _ => $"{Updated} settimane aggiornate",
            });
            if (UpdatedWeeks.Count > 0) parts[0] += $" (fino al {UpdatedWeeks[^1].AddDays(6):dd/MM})";
            if (KeptEdited > 0)
                parts.Add(KeptEdited == 1 ? "1 lasciata com'era perché modificata a mano" : $"{KeptEdited} lasciate com'erano perché modificate a mano");
            var text = string.Join(", ", parts) + ".";
            return Error is null ? text : $"{text} {Error}";
        }
    }
}

/// <summary>Scarica in blocco gli schemi delle prossime settimane da wol.jw.org.</summary>
public static class WeekSync
{
    /// <summary>Settimane consecutive non ancora pubblicate dopo le quali ci si ferma.</summary>
    public const int StopAfterMissing = 2;

    /// <summary>
    /// Scarica le settimane a partire da <paramref name="fromDay"/>. Gli schemi già salvati vengono sovrascritti,
    /// tranne quelli modificati a mano; la visita del sorvegliante viene riapplicata allo schema nuovo.
    /// </summary>
    public static async Task<SyncResult> DownloadAheadAsync(WolClient wol, DataStore store, WolLanguage lang, DateOnly fromDay,
        int maxWeeks = 16, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var updated = new List<DateOnly>();
        var kept = new List<DateOnly>();
        int missing = 0;
        string? error = null;
        var monday = WeekMath.MondayOf(fromDay);

        for (int i = 0; i < maxWeeks && missing < StopAfterMissing; i++, monday = monday.AddDays(7))
        {
            ct.ThrowIfCancellationRequested();
            var existing = store.LoadWeek(monday);
            if (existing?.EditedManually == true)
            {
                kept.Add(monday);
                continue;
            }

            progress?.Report($"Download settimana {WeekMath.Label(monday)}…");
            try
            {
                var week = await wol.FetchWeekAsync(monday, lang, ct);
                if (existing?.CircuitOverseerVisit == true)
                {
                    week.CircuitOverseerVisit = true;
                    week.Midweek = MeetingTemplates.ApplyOverseerVisit(week.Midweek);
                    week.Weekend = MeetingTemplates.ApplyOverseerVisit(week.Weekend);
                }
                store.SaveWeek(week);
                updated.Add(monday);
                missing = 0;
            }
            catch (WolFetchException ex) when (IsNetworkError(ex))
            {
                error = ex.Message;
                break;
            }
            catch (WolFetchException)
            {
                missing++; // settimana non ancora pubblicata
            }
        }
        return new SyncResult(updated.Count, kept.Count, updated, kept, error);
    }

    static bool IsNetworkError(WolFetchException ex) =>
        ex.InnerException is HttpRequestException { StatusCode: not HttpStatusCode.NotFound } or TaskCanceledException;
}
