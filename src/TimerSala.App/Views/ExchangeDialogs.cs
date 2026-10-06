using System.IO;
using System.Windows;
using Microsoft.Win32;
using TimerSala.App.ViewModels;
using TimerSala.Core.Models;
using TimerSala.Core.Storage;

namespace TimerSala.App.Views;

/// <summary>Finestre per salvare e aprire i file .timersala.</summary>
public static class ExchangeDialogs
{
    const string Filter = "File di TimerSala (*.timersala)|*.timersala|Tutti i file|*.*";

    public static void ExportBackup(Window owner, MainViewModel vm) =>
        Save(owner, $"TimerSala backup {DateTime.Today:yyyy-MM-dd}", () => vm.CreateBackup(),
            "Backup salvato. Contiene anche il PIN del controllo remoto: conservalo con cura.");

    public static void ExportWeek(Window owner, MainViewModel vm) =>
        Save(owner, $"TimerSala settimana {vm.Week.WeekStart:yyyy-MM-dd}", () => vm.CreateWeekExport(),
            $"Settimana {WeekMath.Label(vm.Week.WeekStart)} salvata. Sul PC della sala aprila con «Importa da file…» (menu del download).");

    static void Save(Window owner, string name, Func<ExchangeFile> create, string done)
    {
        var dlg = new SaveFileDialog { FileName = name + ExchangeFile.Extension, Filter = Filter, DefaultExt = ExchangeFile.Extension, AddExtension = true };
        if (dlg.ShowDialog(owner) != true) return;
        try
        {
            Exchange.Write(dlg.FileName, create());
            MessageBox.Show(owner, done, "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, "Non è stato possibile salvare il file: " + ex.Message, "TimerSala", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Apre un backup o una settimana. Restituisce true se ha importato qualcosa.</summary>
    public static bool Import(Window owner, MainViewModel vm)
    {
        var dlg = new OpenFileDialog { Filter = Filter };
        if (dlg.ShowDialog(owner) != true) return false;
        ExchangeFile file;
        try { file = Exchange.Read(dlg.FileName); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, ex.Message, "TimerSala", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var question = file.IsBackup
            ? "Importare questo backup? Impostazioni e frasi pronte di questo PC verranno sostituite, e gli schemi delle stesse settimane sovrascritti. "
              + "Schermo della sala, finestre e microfono restano quelli di questo PC."
            : $"Importare la settimana {WeekMath.Label(file.Weeks[0].WeekStart)}? Se è già salvata verrà sostituita.";
        if (MessageBox.Show(owner, question, "TimerSala", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;

        if (vm.Import(file) is not { } done)
        {
            MessageBox.Show(owner, "Ferma il timer prima di importare.", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        MessageBox.Show(owner, done, "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }
}
