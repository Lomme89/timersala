using System.Text.Json;
using TimerSala.Core.Models;

namespace TimerSala.Core.Storage;

public sealed class SessionState
{
    public DateOnly WeekStart { get; set; }
    public MeetingKind Kind { get; set; }
    public Timing.TimerState Timer { get; set; } = new();
}

/// <summary>Salvataggio di impostazioni e schemi in %AppData%\TimerSala.</summary>
public sealed class DataStore
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string Root { get; }
    public string WeeksFolder => Path.Combine(Root, "settimane");
    public string DiagnosticsFolder => Path.Combine(Root, "diagnostica");
    string SettingsPath => Path.Combine(Root, "impostazioni.json");

    /// <summary>%AppData%\TimerSala: la cartella dei dati (e del primo profilo).</summary>
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimerSala");

    public DataStore(string? root = null)
    {
        Root = root ?? DefaultRoot;
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(WeeksFolder);
        IsNew = !File.Exists(SettingsPath);
    }

    /// <summary>All'apertura non c'erano impostazioni salvate: è la prima volta che TimerSala gira su questo PC.</summary>
    public bool IsNew { get; }

    public AppSettings LoadSettings() => Read<AppSettings>(SettingsPath) ?? new AppSettings();

    public void SaveSettings(AppSettings settings) => Write(SettingsPath, settings);

    string WeekPath(DateOnly monday) => Path.Combine(WeeksFolder, $"{monday:yyyy-MM-dd}.json");

    public WeekSchedule? LoadWeek(DateOnly monday) => Read<WeekSchedule>(WeekPath(WeekMath.MondayOf(monday)));

    /// <summary>Tutte le settimane salvate.</summary>
    public IEnumerable<WeekSchedule> AllWeeks()
    {
        foreach (var f in Directory.EnumerateFiles(WeeksFolder, "*.json"))
            if (Read<WeekSchedule>(f) is { } w) yield return w;
    }

    public void SaveWeek(WeekSchedule week) => Write(WeekPath(week.WeekStart), week);

    string SessionPath => Path.Combine(Root, "adunanza-in-corso.json");

    /// <summary>Stato dell'adunanza in corso, per riprendere dopo un riavvio.</summary>
    public SessionState? LoadSession() => Read<SessionState>(SessionPath);

    public void SaveSession(SessionState state)
    {
        try { Write(SessionPath, state); } catch (IOException) { /* il salvataggio riproverà al prossimo giro */ }
    }

    public void ClearSession()
    {
        try { if (File.Exists(SessionPath)) File.Delete(SessionPath); } catch (IOException) { }
    }

    public void DeleteWeek(DateOnly monday)
    {
        var p = WeekPath(monday);
        if (File.Exists(p)) File.Delete(p);
    }

    static T? Read<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception)
        {
            return null; // file corrotto: si riparte dai valori predefiniti
        }
    }

    static void Write<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
        File.Move(tmp, path, overwrite: true);
    }
}
