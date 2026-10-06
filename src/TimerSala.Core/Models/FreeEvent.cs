namespace TimerSala.Core.Models;

/// <summary>Una parte di un evento fuori programma.</summary>
public sealed record FreeEventPart(string Title, int Minutes);

/// <summary>
/// Evento fuori programma (discorso di matrimonio o funerale, adunanza per il servizio…): poche parti con la loro
/// durata, un orario d'inizio. Non tocca lo schema della settimana.
/// </summary>
public sealed record FreeEvent(string Title, List<FreeEventPart> Parts)
{
    public const int MaxRecent = 6;

    public Meeting ToMeeting(MeetingKind kind) => new()
    {
        Kind = kind,
        Title = Title,
        Parts = Parts.Where(p => p.Minutes > 0)
            .Select(p => new MeetingPart { Title = p.Title.Trim().Length > 0 ? p.Title.Trim() : Title, Section = PartSection.Other, DurationSeconds = p.Minutes * 60 })
            .ToList(),
    };

    /// <summary>Gli ultimi eventi, il più recente prima, senza doppioni (per titolo).</summary>
    public static List<FreeEvent> Remember(IEnumerable<FreeEvent> recent, FreeEvent added) =>
        [added, .. recent.Where(e => !string.Equals(e.Title, added.Title, StringComparison.OrdinalIgnoreCase)).Take(MaxRecent - 1)];
}
