using System.Windows;
using System.Windows.Threading;
using TimerSala.App.ViewModels;
using TimerSala.App.Views;
using TimerSala.Core.Storage;

namespace TimerSala.App;

public partial class App : Application
{
    MainViewModel? _vm;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        var store = new DataStore();
        _vm = new MainViewModel(store);
        var main = new MainWindow(_vm);
        MainWindow = main;
        main.Show();
        await _vm.StartWebServerAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _vm?.SaveSettings();
        try { _vm?.StopWebServerAsync().Wait(TimeSpan.FromSeconds(2)); } catch { }
        base.OnExit(e);
    }

    void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // durante l'adunanza il programma non deve chiudersi per un errore imprevisto
        MessageBox.Show($"Si è verificato un errore:\n{e.Exception.Message}", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
