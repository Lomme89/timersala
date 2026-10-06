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

/// <summary>Aspetto del conto alla rovescia prima dell'inizio dell'adunanza.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CountdownStyle>))]
public enum CountdownStyle
{
    /// <summary>Come una parte: titolo, cifre e barra.</summary>
    Classic,
    /// <summary>Blu notte, un anello che si chiude fino all'inizio.</summary>
    Ring,
    /// <summary>Cifre grandi e un blocco per ogni minuto.</summary>
    Blocks,
    /// <summary>L'ora attuale in grande, l'inizio sotto.</summary>
    Clock,
    /// <summary>Lo schermo si riempie dal basso fino all'inizio.</summary>
    Tide,
    /// <summary>I minuti al centro, i secondi sulle tacche di un quadrante.</summary>
    Dial,
    /// <summary>«Si comincia tra 4 minuti»; i secondi solo nell'ultimo minuto.</summary>
    Words,
}

/// <summary>Tema del controller (lo schermo della sala ha il suo).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ControllerTheme>))]
public enum ControllerTheme { Dark, Light, Auto }

/// <summary>Posizione e dimensioni di una finestra, in pixel dello schermo (anche su più monitor).</summary>
public sealed record WindowBounds(int Left, int Top, int Right, int Bottom, bool Maximized = false);

public sealed class AppSettings
{
    // ── Orari delle adunanze ──
    public DayOfWeek MidweekDay { get; set; } = DayOfWeek.Wednesday;
    public TimeOnly MidweekTime { get; set; } = new(19, 0);
    public DayOfWeek WeekendDay { get; set; } = DayOfWeek.Sunday;
    public TimeOnly WeekendTime { get; set; } = new(10, 0);
    public int MeetingLengthMinutes { get; set; } = 105;

    /// <summary>Lo studio biblico di congregazione si adatta al ritardo accumulato.</summary>
    public bool AdaptiveStudy { get; set; }

    /// <summary>Lo studio Torre di Guardia si adatta al ritardo accumulato.</summary>
    public bool AdaptiveWatchtower { get; set; }

    /// <summary>Minuti prima dell'inizio in cui compare il conto alla rovescia (0 = mai).</summary>
    public int CountdownMinutes { get; set; } = 5;

    public CountdownStyle CountdownStyle { get; set; } = CountdownStyle.Tide;

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

    /// <summary>Secondi a tutto schermo per i messaggi inviati «a tutto schermo», poi passano nella fascia.</summary>
    public int MessageFullScreenSeconds { get; set; } = 6;

    // ── Lista di controllo prima dell'adunanza ──
    public bool ChecklistEnabled { get; set; } = true;
    public List<string> ChecklistItems { get; set; } =
    [
        "Batterie dei microfoni cariche",
        "Zoom avviato, con l'audio della sala condiviso",
        "Cantici e video scaricati in JW Library",
        "Schermo della sala acceso",
    ];

    // ── Controllo remoto dalla pagina web ──
    public bool RemoteControlEnabled { get; set; } = true;
    public string RemotePin { get; set; } = Random.Shared.Next(1000, 10000).ToString();

    // ── Avvio con la voce (sperimentale) ──
    public bool VoiceStartEnabled { get; set; }

    /// <summary>Identificativo Windows dell'ingresso audio. Vuoto = ingresso predefinito.</summary>
    public string? VoiceInputDevice { get; set; }

    /// <summary>Livello minimo della voce, in dBFS.</summary>
    public double VoiceThresholdDb { get; set; } = -40;

    /// <summary>Silenzio che deve precedere l'inizio della parte.</summary>
    public double VoicePauseSeconds { get; set; } = 2;

    /// <summary>Durata minima del parlato per avviare la parte.</summary>
    public double VoiceMinSeconds { get; set; } = 0.4;

    /// <summary>Dopo Ferma, la parte successiva va in attesa della voce (se non c'è un cantico in mezzo).</summary>
    public bool VoiceAutoArmNext { get; set; }

    // ── Modalità mini ──
    public bool MiniMode { get; set; }
    public double? MiniLeft { get; set; }
    public double? MiniTop { get; set; }

    // ── Visita del sorvegliante di circoscrizione ──

    /// <summary>Settimane della visita (il lunedì di ciascuna), pianificabili con mesi di anticipo.</summary>
    public List<DateOnly> OverseerVisits { get; set; } = [];

    /// <summary>Giorno dell'infrasettimanale durante la visita (null = il solito giorno).</summary>
    public DayOfWeek? OverseerMidweekDay { get; set; }

    /// <summary>Orario dell'infrasettimanale durante la visita (null = il solito orario).</summary>
    public TimeOnly? OverseerMidweekTime { get; set; }

    public bool IsOverseerWeek(DateOnly monday) => OverseerVisits.Contains(monday);

    public (DayOfWeek Day, TimeOnly Time) ScheduleFor(Models.MeetingKind kind) =>
        kind == Models.MeetingKind.Midweek ? (MidweekDay, MidweekTime) : (WeekendDay, WeekendTime);

    /// <summary>Giorno e ora dell'adunanza in una certa settimana: nella settimana della visita l'infrasettimanale può spostarsi.</summary>
    public (DayOfWeek Day, TimeOnly Time) ScheduleFor(Models.MeetingKind kind, DateOnly monday)
    {
        var (day, time) = ScheduleFor(kind);
        if (kind == Models.MeetingKind.Midweek && IsOverseerWeek(monday))
            return (OverseerMidweekDay ?? day, OverseerMidweekTime ?? time);
        return (day, time);
    }

    /// <summary>Data e ora di inizio dell'adunanza nella settimana che inizia con <paramref name="monday"/>.</summary>
    public DateTime StartOf(Models.MeetingKind kind, DateOnly monday)
    {
        var (day, time) = ScheduleFor(kind, monday);
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

    /// <summary>Tema di controller, mini, editor e impostazioni: scuro, chiaro o come Windows.</summary>
    public ControllerTheme ControllerTheme { get; set; } = ControllerTheme.Dark;

    /// <summary>TimerSala si apre all'accesso a Windows.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>All'avvio della prima parte dell'adunanza il controller passa alla modalità mini.</summary>
    public bool AutoMiniOnFirstPart { get; set; }

    /// <summary>Scala dell'interfaccia in percento (90–150).</summary>
    public int UiScalePercent { get; set; } = 100;

    /// <summary>Finestre ricordate: «controller», «mini», «editor», «impostazioni».</summary>
    public Dictionary<string, WindowBounds> Windows { get; set; } = [];

    // posizione del controller fino alla 1.13 (letta solo se manca quella in Windows)
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
