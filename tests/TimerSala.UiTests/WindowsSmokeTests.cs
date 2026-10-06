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
            try { Run(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromMinutes(2)), "le finestre non hanno finito in tempo");
        if (failure is not null) throw new Exception("Errore nell'interfaccia: " + failure, failure);
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
        var store = new DataStore(dir);
        // niente rete né microfono durante la prova
        store.SaveSettings(new AppSettings { AutoDownload = false, WebServerEnabled = false, VoiceStartEnabled = false, CountdownMinutes = 0 });

        // gli indirizzi come /Assets/timersala.ico vanno cercati nel programma, non nel progetto di prova
        // (WPF lo fissa all'assembly d'ingresso, qui il runner dei test: si corregge il campo interno)
        typeof(Application).GetField("_resourceAssembly", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(null, typeof(TimerSala.App.App).Assembly);
        var app = new TimerSala.App.App();
        app.InitializeComponent();
        Exception? unhandled = null;
        app.DispatcherUnhandledException += (_, e) => { unhandled ??= e.Exception; e.Handled = true; };

        var vm = new MainViewModel(store);
        UiTheme.Apply(vm.Settings);
        var main = new MainWindow(vm);
        app.MainWindow = main;
        main.Show();
        Pump();

        // il timer parte e si ferma, con «Annulla»
        vm.ToggleStartCommand.Execute(null);
        Pump();
        vm.ToggleStartCommand.Execute(null);
        Pump();
        Assert.True(vm.CanUndoAction);
        vm.UndoActionCommand.Execute(null);
        Pump();
        Assert.True(vm.IsRunning);
        vm.ToggleStartCommand.Execute(null);

        // riquadro della settimana, modalità mini e ritorno
        vm.WeekPanelOpen = true;
        Pump();
        vm.IsMiniMode = true;
        Pump(600);
        vm.IsMiniMode = false;
        Pump(600);

        // impostazioni: tutte le sezioni
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
        var editor = new EditorWindow(vm) { Owner = main };
        editor.Show();
        Pump();
        editor.Close();
        var part = vm.Parts.First(p => p.IsTimed);
        var edit = new PartEditWindow(part) { Owner = main };
        edit.Show();
        Pump();
        edit.Close();

        // tema chiaro e interfaccia più grande, poi di nuovo come prima
        UiTheme.Apply(ControllerTheme.Light, 1.3);
        Pump();
        UiTheme.Apply(ControllerTheme.Dark, 1.0);
        Pump();

        main.Close();
        Pump();
        try { Directory.Delete(dir, recursive: true); } catch { }
        if (unhandled is not null) throw new Exception("Errore durante la prova", unhandled);
    }
}
