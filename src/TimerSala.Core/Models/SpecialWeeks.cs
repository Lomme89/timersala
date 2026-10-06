using TimerSala.Core.Storage;

namespace TimerSala.Core.Models;

/// <summary>
/// Applica allo schema di una settimana le settimane particolari pianificate (oltre alla visita del sorvegliante,
/// che ha la sua logica): la Commemorazione al posto dell'adunanza della sua parte di settimana e il discorso speciale.
/// Toglie le modifiche se la settimana non è più particolare.
/// </summary>
public static class SpecialWeeks
{
    /// <summary>Restituisce true se lo schema è cambiato (va salvato).</summary>
    public static bool Apply(WeekSchedule week, AppSettings settings)
    {
        bool changed = false;
        var memorial = settings.MemorialIn(week.WeekStart);
        foreach (var kind in new[] { MeetingKind.Midweek, MeetingKind.Weekend })
        {
            bool wanted = memorial?.Replaces == kind;
            bool present = MeetingTemplates.IsMemorial(week.Get(kind));
            if (wanted && !present)
            {
                week.Set(kind, MeetingTemplates.Memorial(kind));
                changed = true;
            }
            else if (!wanted && present)
            {
                week.Set(kind, Restored(week, kind));
                changed = true;
            }
        }

        // discorso speciale: cambia solo il titolo del discorso pubblico
        var talk = week.Weekend.Parts.FirstOrDefault(p => p.Section == PartSection.PublicTalk);
        if (talk is not null)
        {
            bool special = settings.IsSpecialTalkWeek(week.WeekStart);
            if (special && talk.Title != MeetingTemplates.SpecialTalkTitle)
            {
                talk.Title = MeetingTemplates.SpecialTalkTitle;
                changed = true;
            }
            else if (!special && talk.Title == MeetingTemplates.SpecialTalkTitle)
            {
                talk.Title = week.DownloadedWeekend?.Parts.FirstOrDefault(p => p.Section == PartSection.PublicTalk)?.Title ?? "Discorso pubblico";
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>Lo schema normale di quella adunanza: lo scaricato (o il modello), con la visita se c'è.</summary>
    static Meeting Restored(WeekSchedule week, MeetingKind kind)
    {
        var m = (kind == MeetingKind.Midweek ? week.DownloadedMidweek : week.DownloadedWeekend)?.Clone()
                ?? (kind == MeetingKind.Midweek ? MeetingTemplates.DefaultMidweek() : MeetingTemplates.DefaultWeekend());
        return week.CircuitOverseerVisit ? MeetingTemplates.ApplyOverseerVisit(m) : m;
    }

    /// <summary>Testo per lo schermo e la pagina web quando in quella settimana non c'è adunanza.</summary>
    public static string? NoMeetingNotice(AppSettings settings, DateOnly monday) =>
        settings.IsAssemblyWeek(monday) ? "Settimana dell'assemblea · nessuna adunanza in sala" : null;
}
