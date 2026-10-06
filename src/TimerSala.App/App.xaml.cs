using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TimerSala.App.ViewModels;
using TimerSala.App.Views;
using TimerSala.Core.Storage;

namespace TimerSala.App;

public partial class App : Application
{
    MainViewModel? _vm;

    // una sola copia per utente: una seconda apertura chiede alla prima di farsi vedere e si chiude
    const string InstanceName = @"Local\TimerSala.Instance";
    const string CommandEventPrefix = @"Local\TimerSala.";
    // comandi accettati dalla riga di comando (anche dal menu dell'icona): «mostra» porta solo in primo piano
    static readonly string[] Commands = ["mostra", "schermo", "mini", "telefono"];
    Mutex? _instance;
    EventWaitHandle[] _commandEvents = [];

    /// <summary>
    /// Per la prova delle finestre: WPF chiama OnStartup appena si crea l'applicazione, anche senza Run();
    /// la prova costruisce da sé controller e dati, quindi l'avvio vero va saltato.
    /// </summary>
    internal static bool SkipStartup;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (SkipStartup) return;
        _instance = new Mutex(true, InstanceName, out bool first);
        _commandEvents = Commands.Select(c => new EventWaitHandle(false, EventResetMode.AutoReset, CommandEventPrefix + c)).ToArray();
        if (!first)
        {
            // la copia già aperta esegue il comando (per esempio dal menu dell'icona) o si fa vedere
            var asked = e.Args.Select(a => a.TrimStart('-')).FirstOrDefault(a => Commands.Contains(a)) ?? "mostra";
            _commandEvents[Array.IndexOf(Commands, asked)].Set();
            _instance.Dispose();
            _instance = null;
            Shutdown();
            return;
        }
        DispatcherUnhandledException += OnUnhandled;

        // tutte le finestre: barra del titolo scura, angoli arrotondati e comparsa in dissolvenza
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));

        // congregazioni: si apre quella dell'adunanza di adesso, altrimenti l'ultima usata
        var catalog = new ProfileCatalog(DataStore.DefaultRoot);
        var profile = catalog.ChooseFor(DateTime.Now) ?? catalog.LastUsed;
        var store = catalog.Open(profile);
        _vm = new MainViewModel(store);
        _vm.UseProfiles(catalog, profile);
        Theming.UiTheme.Apply(_vm.Settings);
        var main = new MainWindow(_vm);
        MainWindow = main;
        main.Show();
        ListenForSecondInstance(main);
        SetJumpList();
        await _vm.StartWebServerAsync();
    }

    void ListenForSecondInstance(MainWindow main)
    {
        var events = _commandEvents;
        var thread = new Thread(() =>
        {
            try
            {
                while (true)
                {
                    int i = WaitHandle.WaitAny(events);
                    var command = Commands[i];
                    Dispatcher.BeginInvoke(() => main.RunShellCommand(command));
                }
            }
            catch (ObjectDisposedException) { }
        }) { IsBackground = true, Name = "TimerSala seconda apertura" };
        thread.Start();
    }

    /// <summary>Menu dell'icona sulla barra delle applicazioni (tasto destro).</summary>
    void SetJumpList()
    {
        if (Environment.ProcessPath is not { } exe) return;
        try
        {
            JumpTask Task(string command, string title, string description) => new()
            {
                ApplicationPath = exe, Arguments = "--" + command, Title = title, Description = description,
                IconResourcePath = exe, IconResourceIndex = 0,
            };
            var list = new JumpList([
                Task("schermo", "Mostra o nascondi lo schermo della sala", "Accende o spegne il timer sullo schermo della sala"),
                Task("mini", "Modalità mini", "Passa dal controller alla mini e viceversa"),
                Task("telefono", "Collega un telefono", "Mostra il codice QR per seguire o comandare il timer"),
            ], showFrequent: false, showRecent: false);
            JumpList.SetJumpList(this, list);
        }
        catch { /* il menu dell'icona è un di più */ }
    }

    static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window w || !ReferenceEquals(e.OriginalSource, w)) return;
        Theming.UiTheme.OnWindowLoaded(w);
        Interop.DarkTitleBar.Apply(w, roundCorners: w.WindowStyle == WindowStyle.None && w is not TimerWindow);
        if (w is TimerWindow || w.AllowsTransparency || Theming.Motion.Reduced || w.Content is not UIElement content) return;

        var duration = new Duration(TimeSpan.FromMilliseconds(180));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        content.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        var shift = new TranslateTransform(0, 8);
        content.RenderTransform = shift;
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration) { EasingFunction = ease });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_instance is not null)
        {
            try { _instance.ReleaseMutex(); } catch { }
            _instance.Dispose();
        }
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
