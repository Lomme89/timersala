using System.Text.Json;
using TimerSala.Core.Models;

namespace TimerSala.Core.Storage;

/// <summary>Contenuto di un file .timersala: un backup completo oppure una sola settimana.</summary>
public sealed class ExchangeFile
{
    public const string Extension = ".timersala";
    public const string BackupContent = "backup";
    public const string WeekContent = "settimana";

    public const string FormatName = "TimerSala";

    // senza valori predefiniti: un JSON qualsiasi non deve sembrare un file di TimerSala
    public string? Format { get; set; }
    public int FormatVersion { get; set; }
    public string? Content { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Solo nel backup.</summary>
    public AppSettings? Settings { get; set; }

    public List<WeekSchedule> Weeks { get; set; } = [];

    public bool IsBackup => Content == BackupContent;
}

/// <summary>Esportazione e importazione: un solo formato per il backup, per una settimana preparata a casa e (in futuro) per un profilo.</summary>
public static class Exchange
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>Impostazioni, frasi pronte e schemi modificati a mano.</summary>
    public static ExchangeFile Backup(DataStore store, AppSettings settings, string? appVersion = null) => new()
    {
        Format = ExchangeFile.FormatName,
        FormatVersion = 1,
        Content = ExchangeFile.BackupContent,
        AppVersion = appVersion,
        Settings = settings,
        Weeks = store.AllWeeks().Where(w => w.EditedManually).OrderBy(w => w.WeekStart).ToList(),
    };

    public static ExchangeFile Week(WeekSchedule week, string? appVersion = null) => new()
    {
        Format = ExchangeFile.FormatName,
        FormatVersion = 1,
        Content = ExchangeFile.WeekContent,
        AppVersion = appVersion,
        Weeks = [week],
    };

    public static void Write(string path, ExchangeFile file) => File.WriteAllText(path, JsonSerializer.Serialize(file, Json));

    /// <summary>Legge un file .timersala; se non è valido spiega perché.</summary>
    public static ExchangeFile Read(string path)
    {
        ExchangeFile? file;
        try { file = JsonSerializer.Deserialize<ExchangeFile>(File.ReadAllText(path), Json); }
        catch (JsonException) { throw new InvalidDataException("Il file non è un backup o una settimana di TimerSala."); }
        if (file is null || file.Format != ExchangeFile.FormatName)
            throw new InvalidDataException("Il file non è un backup o una settimana di TimerSala.");
        if (file.FormatVersion > 1)
            throw new InvalidDataException("Il file è stato creato da una versione più recente di TimerSala: aggiorna il programma.");
        if (file.Content is not (ExchangeFile.BackupContent or ExchangeFile.WeekContent) || (file.IsBackup && file.Settings is null))
            throw new InvalidDataException("Il file è incompleto.");
        return file;
    }

    /// <summary>
    /// Le impostazioni del backup, tenendo quelle legate a questo PC: schermo della sala, posizione delle finestre,
    /// ingresso audio e avvio con Windows.
    /// </summary>
    public static AppSettings MergeSettings(AppSettings fromBackup, AppSettings current)
    {
        var s = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(fromBackup, Json), Json)!;
        s.TimerMonitor = current.TimerMonitor;
        s.TimerWindowVisible = current.TimerWindowVisible;
        s.Windows = current.Windows;
        s.ControllerLeft = current.ControllerLeft;
        s.ControllerTop = current.ControllerTop;
        s.ControllerWidth = current.ControllerWidth;
        s.ControllerHeight = current.ControllerHeight;
        s.MiniLeft = current.MiniLeft;
        s.MiniTop = current.MiniTop;
        s.MiniMode = current.MiniMode;
        s.VoiceInputDevice = current.VoiceInputDevice;
        s.StartWithWindows = current.StartWithWindows;
        // i telefoni autorizzati sono quelli che hanno usato questo PC
        s.TrustedDevices = current.TrustedDevices;
        s.LastWebAddress = current.LastWebAddress;
        s.ClickerDevice = current.ClickerDevice;
        return s;
    }

    /// <summary>
    /// Passando a un altro profilo (congregazione) restano quelle di questo PC e quelle della sala, uguali per tutti:
    /// schermo, finestre, microfono, telecomando, rete e telefoni, aspetto del controller.
    /// </summary>
    public static AppSettings ForProfile(AppSettings profile, AppSettings current)
    {
        var s = MergeSettings(profile, current);
        s.WebServerEnabled = current.WebServerEnabled;
        s.WebServerPort = current.WebServerPort;
        s.WebAddressMode = current.WebAddressMode;
        s.RemoteControlEnabled = current.RemoteControlEnabled;
        s.RemotePin = current.RemotePin;
        s.ClickerEnabled = current.ClickerEnabled;
        s.ControllerTheme = current.ControllerTheme;
        s.UiScalePercent = current.UiScalePercent;
        s.ControllerTopmost = current.ControllerTopmost;
        s.AutoMiniOnFirstPart = current.AutoMiniOnFirstPart;
        s.OnboardingDone = current.OnboardingDone;
        s.LastSeenVersion = current.LastSeenVersion;
        return s;
    }

    /// <summary>Salva le settimane del file (sostituendo quelle con la stessa data). Restituisce quante sono.</summary>
    public static int ImportWeeks(DataStore store, ExchangeFile file)
    {
        foreach (var w in file.Weeks)
        {
            w.WeekStart = WeekMath.MondayOf(w.WeekStart);
            store.SaveWeek(w);
        }
        return file.Weeks.Count;
    }
}
