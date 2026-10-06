using System.Net.Http.Headers;

namespace TimerSala.Core.Timing;

/// <summary>
/// Confronta l'ora del PC con quella di internet (intestazione «Date» di un sito affidabile):
/// countdown d'inizio e fine prevista dipendono dall'orologio del PC.
/// </summary>
public static class ClockCheck
{
    /// <summary>Oltre questo scarto l'orologio del PC è considerato sbagliato.</summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(1);

    static readonly Uri[] Sources = [new("https://wol.jw.org/"), new("https://www.microsoft.com/")];

    /// <summary>
    /// Di quanto l'orologio del PC è avanti (positivo) o indietro (negativo) rispetto a internet;
    /// null se nessun sito risponde (per esempio senza connessione).
    /// </summary>
    public static async Task<TimeSpan?> MeasureAsync(HttpClient http, TimeProvider? clock = null, CancellationToken ct = default)
    {
        clock ??= TimeProvider.System;
        foreach (var uri in Sources)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, uri);
                req.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                var sent = clock.GetUtcNow();
                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                var received = clock.GetUtcNow();
                if (Offset(resp.Headers.Date, sent, received) is { } offset) return offset;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { /* prova il sito successivo */ }
        }
        return null;
    }

    /// <summary>
    /// Scarto tra l'orologio locale e quello del server: l'ora del server si confronta con il momento
    /// a metà tra invio e risposta. Null se la risposta è arrivata troppo lenta per essere attendibile.
    /// </summary>
    public static TimeSpan? Offset(DateTimeOffset? serverDate, DateTimeOffset sent, DateTimeOffset received)
    {
        if (serverDate is not { } server) return null;
        var roundTrip = received - sent;
        if (roundTrip < TimeSpan.Zero || roundTrip > TimeSpan.FromSeconds(20)) return null;
        // «Date» è troncata al secondo: in media è mezzo secondo indietro
        var local = sent + roundTrip / 2;
        return local - (server + TimeSpan.FromMilliseconds(500));
    }

    /// <summary>Testo dell'avviso, o null se l'orologio è giusto.</summary>
    public static string? Warning(TimeSpan? offset)
    {
        if (offset is not { } o || o.Duration() <= Tolerance) return null;
        int minutes = (int)Math.Round(o.Duration().TotalMinutes);
        int hours = (int)Math.Round(minutes / 60.0);
        string amount = minutes >= 60 ? hours == 1 ? "1 ora" : $"{hours} ore"
            : minutes == 1 ? "1 minuto" : $"{minutes} minuti";
        string way = o > TimeSpan.Zero ? "avanti" : "indietro";
        return $"L'orologio del PC è {way} di {amount}: countdown d'inizio e fine prevista saranno sbagliati. " +
               "Correggilo in Impostazioni di Windows → Data e ora → Sincronizza ora.";
    }
}
