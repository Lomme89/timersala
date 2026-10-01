using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

        // tutte le finestre: barra del titolo scura, angoli arrotondati e comparsa in dissolvenza
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));

        var store = new DataStore();
        _vm = new MainViewModel(store);
        var main = new MainWindow(_vm);
        MainWindow = main;
        main.Show();
        await _vm.StartWebServerAsync();
    }

    static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window w || !ReferenceEquals(e.OriginalSource, w)) return;
        Interop.DarkTitleBar.Apply(w, roundCorners: w.WindowStyle == WindowStyle.None && w is not TimerWindow);
        if (w is TimerWindow || w.Content is not UIElement content) return;

        var duration = new Duration(TimeSpan.FromMilliseconds(180));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        content.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        var shift = new TranslateTransform(0, 8);
        content.RenderTransform = shift;
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration) { EasingFunction = ease });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _vm?.SaveSettings();
        try { _vm?.StopWebServerAsync().Wait(TimeSpan.FromSeconds(2)); } catch { }
        base.OnExit(e);
    }

    static void LogError(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimerSala", "diagnostica");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "errori.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { }
    }

    void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // durante l'adunanza il programma non deve chiudersi per un errore imprevisto
        var messages = new List<string>();
        for (Exception? ex = e.Exception; ex is not null; ex = ex.InnerException)
            messages.Add(ex.Message);
        LogError(e.Exception);
        MessageBox.Show($"Si è verificato un errore:\n{string.Join("\n", messages)}", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
