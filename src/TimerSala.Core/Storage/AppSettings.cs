using System.Text.Json.Serialization;
using TimerSala.Core.Wol;

namespace TimerSala.Core.Storage;

[JsonConverter(typeof(JsonStringEnumConverter<DisplayTheme>))]
public enum DisplayTheme { Dark, Light }

[JsonConverter(typeof(JsonStringEnumConverter<DisplayLayout>))]
public enum DisplayLayout
{
    /// <summary>Titolo, cifre, barra e piè di pagina.</summary>
    Classic,
    /// <summary>Lo sfondo si svuota da destra a sinistra come una clessidra orizzontale.</summary>
    Hourglass,
    /// <summary>Solo le cifre, il più grandi possibile.</summary>
    DigitsOnly,
}

public sealed class AppSettings
{
    // ── Orari delle adunanze ──
    public DayOfWeek MidweekDay { get; set; } = DayOfWeek.Wednesday;
    public TimeOnly MidweekTime { get; set; } = new(19, 0);
    public DayOfWeek WeekendDay { get; set; } = DayOfWeek.Sunday;
    public TimeOnly WeekendTime { get; set; } = new(10, 0);
    public int MeetingLengthMinutes { get; set; } = 105;

    /// <summary>Minuti prima dell'inizio in cui compare il conto alla rovescia (0 = mai).</summary>
    public int CountdownMinutes { get; set; } = 5;

    // ── Stile dello schermo del timer ──
    public DisplayTheme DisplayTheme { get; set; } = DisplayTheme.Dark;
    public DisplayLayout DisplayLayout { get; set; } = DisplayLayout.Classic;
    public string DisplayFont { get; set; } = "Bahnschrift SemiBold";
    public bool ColoredDigits { get; set; } = true;
    public bool ShowTitle { get; set; } = true;
    public bool ShowSection { get; set; } = true;
    public bool ShowProgressBar { get; set; } = true;
    public bool ShowNextPart { get; set; } = true;
    public bool ShowClockWhileRunning { get; set; } = true;
    public bool FlashOnOvertime { get; set; } = true;

    // ── Messaggi all'oratore ──
    public bool MessagesEnabled { get; set; } = true;
    public List<string> MessagePresets { get; set; } = ["Concludi", "Ultimo minuto", "Più forte", "Avvicinati al microfono", "Tempo scaduto"];

    /// <summary>Per quanti secondi resta il messaggio (0 = finché non viene tolto).</summary>
    public int MessageSeconds { get; set; } = 15;

    // ── Controllo remoto dalla pagina web ──
    public bool RemoteControlEnabled { get; set; } = true;
    public string RemotePin { get; set; } = Random.Shared.Next(1000, 10000).ToString();

    // ── Modalità mini ──
    public bool MiniMode { get; set; }
    public double? MiniLeft { get; set; }
    public double? MiniTop { get; set; }

    public (DayOfWeek Day, TimeOnly Time) ScheduleFor(Models.MeetingKind kind) =>
        kind == Models.MeetingKind.Midweek ? (MidweekDay, MidweekTime) : (WeekendDay, WeekendTime);

    /// <summary>Data e ora di inizio dell'adunanza nella settimana che inizia con <paramref name="monday"/>.</summary>
    public DateTime StartOf(Models.MeetingKind kind, DateOnly monday)
    {
        var (day, time) = ScheduleFor(kind);
        int offset = ((int)day + 6) % 7;
        return monday.AddDays(offset).ToDateTime(time);
    }

    public string WolCode { get; set; } = WolLanguage.Italian.Code;
    public string WolRsconf { get; set; } = WolLanguage.Italian.Rsconf;
    public string WolLib { get; set; } = WolLanguage.Italian.Lib;

    /// <summary>Nome dispositivo dello schermo del timer (es. \\.\DISPLAY3). Vuoto = nessuno.</summary>
    public string? TimerMonitor { get; set; }

    public bool TimerWindowVisible { get; set; } = true;

    public bool WebServerEnabled { get; set; } = true;
    public int WebServerPort { get; set; } = 8090;

    /// <summary>Indirizzo nei collegamenti e nei QR: "auto", "hostname" oppure un IP specifico.</summary>
    public string WebAddressMode { get; set; } = "auto";

    public int WarningSeconds { get; set; } = 60;
    public int CounselSeconds { get; set; } = 60;

    public bool AutoDownload { get; set; } = true;
    public bool ShowClockWhenIdle { get; set; } = true;
    public bool ShowNextPartWhenIdle { get; set; } = true;
    public bool ShowDelayOnDisplay { get; set; } = false;
    public bool ControllerTopmost { get; set; } = false;

    public double? ControllerLeft { get; set; }
    public double? ControllerTop { get; set; }
    public double? ControllerWidth { get; set; }
    public double? ControllerHeight { get; set; }

    [JsonIgnore]
    public WolLanguage Language
    {
        get => WolLanguage.Presets.FirstOrDefault(p => p.Code == WolCode && p.Rsconf == WolRsconf && p.Lib == WolLib)
               ?? new WolLanguage(WolCode, WolCode, WolRsconf, WolLib);
        set { WolCode = value.Code; WolRsconf = value.Rsconf; WolLib = value.Lib; }
    }
}
