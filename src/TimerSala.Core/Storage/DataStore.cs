using System.Text.Json;
using TimerSala.Core.Models;

namespace TimerSala.Core.Storage;

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

    public DataStore(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimerSala");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(WeeksFolder);
    }

    public AppSettings LoadSettings() => Read<AppSettings>(SettingsPath) ?? new AppSettings();

    public void SaveSettings(AppSettings settings) => Write(SettingsPath, settings);

    string WeekPath(DateOnly monday) => Path.Combine(WeeksFolder, $"{monday:yyyy-MM-dd}.json");

    public WeekSchedule? LoadWeek(DateOnly monday) => Read<WeekSchedule>(WeekPath(WeekMath.MondayOf(monday)));

    public void SaveWeek(WeekSchedule week) => Write(WeekPath(week.WeekStart), week);

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
