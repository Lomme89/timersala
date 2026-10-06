namespace TimerSala.Core.Models;

/// <summary>Orario previsto d'inizio di ogni parte, secondo lo schema (senza i ritardi).</summary>
public static class MeetingPlan
{
    /// <summary>
    /// Le parti cronometrate durano quanto assegnato; il tempo che resta fino alla durata dell'adunanza
    /// si divide tra cantici e preghiere (che nello schema non hanno una durata).
    /// </summary>
    public static IReadOnlyList<DateTime> PlannedStarts(Meeting meeting, DateTime start, int lengthMinutes)
    {
        var parts = meeting.Parts;
        int timed = parts.Where(p => p.IsTimed).Sum(p => p.DurationSeconds);
        int others = parts.Count(p => !p.IsTimed);
        double gap = others == 0 ? 0 : Math.Max(0, lengthMinutes * 60 - timed) / (double)others;
        // al minuto, come nella guida
        gap = Math.Round(gap / 60) * 60;

        var result = new List<DateTime>(parts.Count);
        var t = start;
        foreach (var p in parts)
        {
            result.Add(t);
            t = t.AddSeconds(p.IsTimed ? p.DurationSeconds : gap);
        }
        return result;
    }

    /// <summary>Minuti stimati per ogni cantico (con la preghiera, quando c'è).</summary>
    public const int SongMinutes = 5;

    /// <summary>
    /// Durata stimata dello schema: parti cronometrate più <see cref="SongMinutes"/> per ogni cantico,
    /// compresi quelli scritti nel titolo di una parte («Cantico e preghiera | Commenti introduttivi»).
    /// </summary>
    public static int EstimatedSeconds(Meeting meeting)
    {
        int timed = meeting.Parts.Where(p => p.IsTimed).Sum(p => p.DurationSeconds);
        int songs = meeting.Parts.Count(p => !p.IsTimed || p.Title.Contains("cantico", StringComparison.OrdinalIgnoreCase));
        return timed + songs * SongMinutes * 60;
    }

    /// <summary>Minuti oltre la durata dell'adunanza (0 se lo schema ci sta).</summary>
    public static int OverrunMinutes(Meeting meeting, int lengthMinutes) =>
        Math.Max(0, (int)Math.Ceiling(EstimatedSeconds(meeting) / 60.0) - lengthMinutes);
}
