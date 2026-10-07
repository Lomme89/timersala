using System.Diagnostics;
using System.IO;
using System.Windows;
using TimerSala.App.ViewModels;
using TimerSala.Core.Info;

namespace TimerSala.App.Views;

/// <summary>Guida, novità e segnalazioni: dal pulsante «?» del controller e da Impostazioni → Programma.</summary>
public static class HelpActions
{
    public const string SiteUrl = "https://lomme89.github.io/timersala/";

    public static void ShowHelp(Window owner, MainViewModel vm)
    {
        var w = DocWindow.Help();
        w.Owner = owner;
        w.AddAction("Novità", () => ShowNews(w))
         .AddAction("Addestramento", () => { w.Close(); StartTraining(owner); })
         .AddAction("Qualcosa non funziona", () => Report(w, vm, isProblem: true))
         .AddAction("Ho un'idea", () => Report(w, vm, isProblem: false))
         .AddAction("Sito", () => Open(SiteUrl));
        w.Show();
    }

    /// <summary>Modalità addestramento: un'adunanza di prova, veloce, che non lascia tracce.</summary>
    public static void StartTraining(Window owner)
    {
        if (Application.Current.MainWindow is MainWindow main) main.StartTraining(owner);
    }

    public static void ShowNews(Window owner)
    {
        var w = DocWindow.News(DocWindow.ChangelogEntries().Take(6).ToList(), afterUpdate: false);
        w.Owner = owner;
        w.Show();
    }

    /// <summary>Dopo un aggiornamento mostra le novità una volta sola. Al primo avvio assoluto no: c'è la guida iniziale.</summary>
    public static void ShowNewsIfUpdated(Window owner, MainViewModel vm)
    {
        var current = AppInfo.Version;
        if (current is "" || vm.Settings.LastSeenVersion == current) return;
        var last = vm.Settings.LastSeenVersion;
        vm.Settings.LastSeenVersion = current;
        vm.SaveSettings();
        if (vm.IsFirstRun) return;
        var news = Changelog.Since(DocWindow.ChangelogEntries(), last, current);
        if (news.Count == 0) return;
        var w = DocWindow.News(news, afterUpdate: true);
        w.Owner = owner;
        w.Show();
    }

    public static void Report(Window owner, MainViewModel vm, bool isProblem)
    {
        var ask = (isProblem
                      ? "Si apre GitHub con una segnalazione già preparata: versione, sistema, impostazioni principali ed errori recenti (mai il PIN).\n\n"
                      : "Si apre GitHub con un suggerimento da completare.\n\n")
                  + "Serve un account GitHub (gratuito) e quello che scrivi sarà pubblico. Continuare?";
        if (ThemedDialog.Show(owner, ask, "TimerSala", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        string? log = null;
        try
        {
            var path = Path.Combine(vm.DiagnosticsFolder, "errori.log");
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                log = text.Length > 20000 ? text[^20000..] : text;
            }
        }
        catch (IOException) { }
        var system = $"{Environment.OSVersion.VersionString} · {(Environment.Is64BitOperatingSystem ? "64 bit" : "32 bit")}";
        Open(ProblemReport.BuildUrl(isProblem, AppInfo.Version, system, vm.Settings, log));
    }

    public static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
