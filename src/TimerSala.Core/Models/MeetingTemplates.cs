namespace TimerSala.Core.Models;

/// <summary>Schemi predefiniti e regole per la settimana della visita del sorvegliante.</summary>
public static class MeetingTemplates
{
    public const string OverseerTalkTitle = "Discorso di servizio del sorvegliante di circoscrizione";

    static MeetingPart P(string title, PartSection section, int minutes, bool counsel = false) =>
        new() { Title = title, Section = section, DurationSeconds = minutes * 60, HasCounsel = counsel };

    static MeetingPart Song(string title, PartSection section) =>
        new() { Title = title, Section = section, IsSong = true };

    public static Meeting DefaultMidweek() => new()
    {
        Kind = MeetingKind.Midweek,
        Title = "Vita e ministero cristiano",
        Parts =
        [
            P("Cantico e preghiera | Commenti introduttivi", PartSection.Opening, 1),
            P("1. Discorso", PartSection.Treasures, 10),
            P("2. Gemme spirituali", PartSection.Treasures, 10),
            P("3. Lettura biblica", PartSection.Treasures, 4, counsel: true),
            P("4. Iniziare una conversazione", PartSection.Ministry, 3, counsel: true),
            P("5. Coltivare l’interesse", PartSection.Ministry, 4, counsel: true),
            P("6. Fare discepoli", PartSection.Ministry, 5, counsel: true),
            Song("Cantico", PartSection.Living),
            P("7. Parte locale", PartSection.Living, 15),
            P("8. Studio biblico di congregazione", PartSection.Living, 30),
            P("Commenti conclusivi | Cantico e preghiera", PartSection.Closing, 3),
        ],
    };

    public const string MemorialTitle = "Commemorazione della morte di Cristo";
    public const string SpecialTalkTitle = "Discorso speciale";

    /// <summary>Schema della Commemorazione: si sistema nell'editor come ogni altro.</summary>
    public static Meeting Memorial(MeetingKind kind) => new()
    {
        Kind = kind,
        Title = MemorialTitle,
        Parts =
        [
            Song("Cantico e preghiera", PartSection.Opening),
            P("Discorso della Commemorazione", PartSection.Other, 45),
            Song("Cantico e preghiera", PartSection.Closing),
        ],
    };

    public static bool IsMemorial(Meeting m) => m.Title == MemorialTitle;

    public static Meeting DefaultWeekend(string? watchtowerTitle = null, int? wtOpeningSong = null, int? wtClosingSong = null) => new()
    {
        Kind = MeetingKind.Weekend,
        Title = "Adunanza del fine settimana",
        Parts =
        [
            Song("Cantico e preghiera", PartSection.Opening),
            P("Discorso pubblico", PartSection.PublicTalk, 30),
            Song(wtOpeningSong is { } s1 ? $"Cantico {s1}" : "Cantico", PartSection.Watchtower),
            P(string.IsNullOrWhiteSpace(watchtowerTitle) ? "Studio Torre di Guardia" : $"Studio Torre di Guardia: {watchtowerTitle}", PartSection.Watchtower, 60),
            Song(wtClosingSong is { } s2 ? $"Cantico {s2} e preghiera" : "Cantico e preghiera", PartSection.Closing),
        ],
    };

    /// <summary>
    /// Adatta le adunanze alla visita del sorvegliante di circoscrizione:
    /// infrasettimanale — lo studio biblico di congregazione è sostituito dal discorso di servizio (30 min)
    /// che segue i commenti conclusivi; fine settimana — lo studio Torre di Guardia dura 30 min ed è seguito dal
    /// discorso di servizio (30 min).
    /// </summary>
    public static Meeting ApplyOverseerVisit(Meeting source)
    {
        var m = source.Clone();
        if (m.Parts.Any(p => p.Title.Contains("sorvegliante", StringComparison.OrdinalIgnoreCase)))
            return m; // già adattata

        if (m.Kind == MeetingKind.Midweek)
        {
            int cbs = m.Parts.FindLastIndex(p => p.IsTimed && p.Section == PartSection.Living);
            if (cbs >= 0) m.Parts.RemoveAt(cbs);

            int closingIdx = m.Parts.FindLastIndex(p => p.Section == PartSection.Closing && p.IsTimed);
            MeetingPart closing;
            if (closingIdx >= 0)
            {
                closing = m.Parts[closingIdx];
                m.Parts.RemoveAt(closingIdx);
            }
            else
            {
                closing = P("Commenti conclusivi", PartSection.Closing, 3);
            }
            // I commenti conclusivi precedono il discorso; il cantico finale lo sceglie il sorvegliante.
            closing.Title = StripSong(closing.Title, "Commenti conclusivi");
            closing.Section = PartSection.Living;
            m.Parts.Add(closing);
            m.Parts.Add(P(OverseerTalkTitle, PartSection.Closing, 30));
            m.Parts.Add(Song("Cantico e preghiera", PartSection.Closing));
        }
        else
        {
            var wt = m.Parts.FindLastIndex(p => p.IsTimed && p.Section == PartSection.Watchtower);
            if (wt >= 0)
            {
                m.Parts[wt].DurationSeconds = 30 * 60;
                int insertAt = wt + 1;
                // il cantico conclusivo dello studio viene dopo il discorso del sorvegliante
                m.Parts.Insert(insertAt, P(OverseerTalkTitle, PartSection.Closing, 30));
            }
            else
            {
                m.Parts.Add(P(OverseerTalkTitle, PartSection.Closing, 30));
            }
        }
        return m;
    }

    static string StripSong(string title, string fallback)
    {
        var pieces = title.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var keep = pieces.FirstOrDefault(p => !SongWords.Any(w => p.StartsWith(w, StringComparison.OrdinalIgnoreCase)));
        return string.IsNullOrWhiteSpace(keep) ? fallback : keep;
    }

    internal static readonly string[] SongWords =
        ["Cantico", "Song", "Cántico", "Canción", "Cantique", "Lied", "Cântico", "Cântarea", "Pieśń", "Lied"];
}
