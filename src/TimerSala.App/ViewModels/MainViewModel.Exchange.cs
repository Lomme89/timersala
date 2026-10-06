using TimerSala.Core.Storage;

namespace TimerSala.App.ViewModels;

/// <summary>Esporta e importa: backup completo o una settimana preparata altrove.</summary>
public sealed partial class MainViewModel
{
    public ExchangeFile CreateBackup() => Exchange.Backup(_store, Settings, AppInfo.Version);

    public ExchangeFile CreateWeekExport() => Exchange.Week(Week, AppInfo.Version);

    /// <summary>Importa un file già letto; restituisce cosa è stato fatto, o null se non si può adesso.</summary>
    public string? Import(ExchangeFile file)
    {
        if (Timer.IsRunning)
        {
            ShowStatus("Ferma il timer prima di importare.", error: true);
            return null;
        }
        int weeks = Exchange.ImportWeeks(_store, file);
        string weeksText = weeks == 1 ? "1 settimana" : $"{weeks} settimane";
        if (file.IsBackup)
        {
            ApplySettings(Exchange.MergeSettings(file.Settings!, Settings));
            LoadWeek(Week.WeekStart);
            var msg = $"Backup importato: impostazioni, frasi pronte e {weeksText} modificate a mano. Schermo della sala e finestre restano quelli di questo PC.";
            ShowStatus(msg);
            return msg;
        }
        LoadWeek(Week.WeekStart);
        var w = file.Weeks.FirstOrDefault();
        var label = w is null ? weeksText : $"la settimana {Core.Models.WeekMath.Label(w.WeekStart)}";
        var done = $"Importata {label}.";
        ShowStatus(done);
        return done;
    }
}
