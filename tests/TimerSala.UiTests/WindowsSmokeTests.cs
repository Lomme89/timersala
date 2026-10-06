using System.IO;
using System.Windows;
using System.Windows.Threading;
using TimerSala.App.Theming;
using TimerSala.App.ViewModels;
using TimerSala.App.Views;
using TimerSala.Core.Storage;

namespace TimerSala.UiTests;

/// <summary>
/// Prova di fumo dell'interfaccia (solo su Windows): apre davvero le finestre, così un errore nello XAML
/// (per esempio una risorsa che manca) si vede nella build e non all'avvio in sala.
/// </summary>
public class WindowsSmokeTests
{
    [Fact]
    public void All_windows_open_render_and_close()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            _nativeThread = GetCurrentThreadId();
            try { Run(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(2)))
            Assert.Fail($"le finestre non hanno finito in tempo: fermo a «{_stage}»; finestre aperte: {string.Join(" | ", OpenWindows(_nativeThread))}");
        if (failure is not null) throw new Exception("Errore nell'interfaccia: " + failure, failure);
    }

    // dove è arrivata la prova, per capire un blocco (per esempio una finestra di messaggio inattesa)
    static volatile string _stage = "inizio";
    static uint _nativeThread;

    static void Stage(string name) => _stage = name;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    delegate bool EnumProc(IntPtr hwnd, IntPtr data);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool EnumThreadWindows(uint threadId, EnumProc proc, IntPtr data);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int max);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hwnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr data);

    /// <summary>Titoli (e testi, per le finestre di messaggio) delle finestre visibili del thread della prova.</summary>
    static List<string> OpenWindows(uint threadId)
    {
        var list = new List<string>();
        EnumThreadWindows(threadId, (h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            var sb = new System.Text.StringBuilder(512);
            GetWindowText(h, sb, sb.Capacity);
            var texts = new List<string>();
            EnumChildWindows(h, (c, _) =>
            {
                var cs = new System.Text.StringBuilder(512);
                if (GetWindowText(c, cs, cs.Capacity) > 0) texts.Add(cs.ToString());
                return true;
            }, IntPtr.Zero);
            list.Add(sb + (texts.Count > 0 ? " [" + string.Join(" / ", texts) + "]" : ""));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    static void Pump(int ms = 300)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    static void Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "timersala-ui-" + Guid.NewGuid().ToString("N"));
        // niente rete né microfono durante la prova; impostazioni già salvate, così non è un primo avvio
        // (che aprirebbe la configurazione guidata, una finestra modale)
        Stage("dati della prova");
        new DataStore(dir).SaveSettings(new AppSettings { AutoDownload = false, WebServerEnabled = false, VoiceStartEnabled = false, CountdownMinutes = 0, OnboardingDone = true });
        var store = new DataStore(dir);
        Assert.False(store.IsNew);

        // gli indirizzi come /Assets/timersala.ico vanno cercati nel programma, non nel progetto di prova
        // (WPF lo fissa all'assembly d'ingresso, qui il runner dei test: si corregge il campo interno)
        Stage("apertura del controller");
        typeof(Application).GetField("_resourceAssembly", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(null, typeof(TimerSala.App.App).Assembly);
        // senza questo WPF farebbe partire anche il programma vero, con i dati di questo PC
        TimerSala.App.App.SkipStartup = true;
        var app = new TimerSala.App.App();
        app.InitializeComponent();
        Exception? unhandled = null;
        app.DispatcherUnhandledException += (_, e) => { unhandled ??= e.Exception; e.Handled = true; };

        var vm = new MainViewModel(store);
        Assert.False(vm.IsFirstRun);
        Assert.True(vm.Settings.OnboardingDone);
        UiTheme.Apply(vm.Settings);
        var main = new MainWindow(vm);
        app.MainWindow = main;
        main.Show();
        Pump();

        // il timer parte e si ferma, con «Annulla»
        Stage("il timer parte e si ferma, con «Annulla»");
        vm.ToggleStartCommand.Execute(null);
        Pump();
        vm.ToggleStartCommand.Execute(null);
        Pump();
        Assert.True(vm.CanUndoAction);
        vm.UndoActionCommand.Execute(null);
        Pump();
        Assert.True(vm.IsRunning);
        vm.ToggleStartCommand.Execute(null);

        // addestramento: parti più brevi, poi tutto torna com'era
        Stage("addestramento: parti più brevi, poi tutto torna com'era");
        var realDuration = vm.Timer.Meeting.Parts.First(p => p.IsTimed && p.DurationSeconds >= 600).DurationSeconds;
        Assert.Null(vm.StartTraining());
        Pump();
        Assert.True(vm.IsTraining);
        Assert.True(vm.Timer.Meeting.Parts.All(p => !p.IsTimed || p.DurationSeconds < realDuration));
        vm.ToggleStartCommand.Execute(null);
        Pump();
        vm.ToggleStartCommand.Execute(null);
        Pump();
        vm.StopTraining();
        Pump();
        Assert.False(vm.IsTraining);
        Assert.Contains(vm.Timer.Meeting.Parts, p => p.DurationSeconds == realDuration);
        Assert.False(vm.Timer.IsRunning);

        // evento fuori programma: finestra, poi l'evento e il ritorno all'adunanza
        Stage("evento fuori programma");
        var ev = new FreeEventWindow(vm) { Owner = main };
        ev.Show();
        Pump();
        ev.Close();
        Assert.Null(vm.StartFreeEvent(new TimerSala.Core.Models.FreeEvent("Matrimonio", [new("Discorso", 30)]), new TimeOnly(16, 0)));
        Pump();
        Assert.Single(vm.Timer.Meeting.Parts);
        vm.StopFreeEvent();
        Pump();
        Assert.False(vm.IsFreeEvent);
        Assert.Contains(vm.Timer.Meeting.Parts, p => p.DurationSeconds == realDuration);

        // congregazioni: un secondo profilo, il passaggio e il ritorno
        Stage("congregazioni");
        var catalog = new ProfileCatalog(dir);
        vm.UseProfiles(catalog, catalog.Profiles[0]);
        var second = vm.AddProfile("Seconda congregazione")!;
        Assert.True(vm.HasProfiles);
        Assert.Null(vm.SwitchProfile(second));
        Pump();
        Assert.Equal("Seconda congregazione", vm.ProfileName);
        Assert.Null(vm.SwitchProfile(catalog.Profiles[0]));
        Pump();

        // lista di controllo aperta con il pulsante, una voce spuntata
        Stage("lista di controllo aperta con il pulsante, una voce spuntata");
        vm.ChecklistOpen = true;
        Pump();
        Assert.True(vm.ChecklistVisible);
        vm.Checklist[0].Done = true;
        Pump();
        vm.ChecklistOpen = false;

        // telecomando: l'associazione registra l'input da tutte le tastiere, poi si annulla
        vm.StartClickerAssociation();
        Pump();
        Assert.Contains("Premi un tasto", vm.ClickerStatus);
        vm.ForgetClicker();
        Assert.False(vm.ClickerAssociated);

        // riquadro della settimana, modalità mini e ritorno
        Stage("riquadro della settimana, modalità mini e ritorno");
        vm.WeekPanelOpen = true;
        Pump();
        vm.IsMiniMode = true;
        Pump(600);
        vm.IsMiniMode = false;
        Pump(600);

        // impostazioni: tutte le sezioni
        Stage("impostazioni: tutte le sezioni");
        var settings = new SettingsWindow(vm) { Owner = main };
        settings.Show();
        Pump();
        for (int i = 0; i < settings.Nav.Items.Count; i++)
        {
            settings.Nav.SelectedIndex = i;
            Pump(200);
        }
        settings.Close();
        Pump();

        // editor e modifica di una parte
        Stage("editor e modifica di una parte");
        var editor = new EditorWindow(vm) { Owner = main };
        editor.Show();
        Pump();
        // Annulla e Ripeti: una parte eliminata torna, e se ne va di nuovo
        Stage("Annulla e Ripeti: una parte eliminata torna, e se ne va di nuovo");
        var evm = (EditorViewModel)editor.DataContext;
        int count = evm.Parts.Count;
        evm.RemoveCommand.Execute(null);
        Assert.Equal(count - 1, evm.Parts.Count);
        evm.Undo();
        Assert.Equal(count, evm.Parts.Count);
        evm.Redo();
        Assert.Equal(count - 1, evm.Parts.Count);
        Pump();
        editor.Close();
        var part = vm.Parts.First(p => p.IsTimed);
        var edit = new PartEditWindow(part) { Owner = main };
        edit.Show();
        Pump();
        edit.Close();

        // configurazione guidata: tutti i passi, poi chiusa senza finire
        Stage("configurazione guidata: tutti i passi, poi chiusa senza finire");
        var welcome = new WelcomeWindow(vm) { Owner = main };
        welcome.Show();
        Pump();
        for (int i = 0; i < WelcomeViewModel.LastStep; i++)
        {
            welcome.ViewModel.Step++;
            Pump(200);
        }
        welcome.Close();
        Pump();
        Assert.True(vm.Settings.OnboardingDone);

        // guida e novità (dal changelog incorporato nel programma)
        Stage("guida e novità (dal changelog incorporato nel programma)");
        var help = DocWindow.Help();
        help.Owner = main;
        help.Show();
        Pump();
        Assert.True(help.Body.Children.Count > 10, "la guida è vuota");
        help.Close();
        var entries = DocWindow.ChangelogEntries();
        Assert.True(entries.Count > 10, "il changelog non è incorporato");
        var news = DocWindow.News(entries.Take(3).ToList(), afterUpdate: true);
        news.Owner = main;
        news.Show();
        Pump();
        news.Close();

        // tema chiaro e interfaccia più grande, poi di nuovo come prima
        Stage("tema chiaro e interfaccia più grande, poi di nuovo come prima");
        UiTheme.Apply(ControllerTheme.Light, 1.3);
        Pump();
        UiTheme.Apply(ControllerTheme.Dark, 1.0);
        Pump();

        Stage("chiusura");
        main.Close();
        Pump();
        try { Directory.Delete(dir, recursive: true); } catch { }
        if (unhandled is not null) throw new Exception("Errore durante la prova", unhandled);
    }
}
