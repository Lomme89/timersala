using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using TimerSala.App.ViewModels;
using TimerSala.Core.Storage;
using TimerSala.Core.Wol;

namespace TimerSala.App.Views;

public partial class SettingsWindow : Window
{
    readonly MainViewModel _vm;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        var s = vm.Settings;

        var langs = WolLanguage.Presets.ToList();
        if (!langs.Contains(s.Language)) langs.Add(s.Language);
        LanguageBox.ItemsSource = langs;
        LanguageBox.SelectedItem = s.Language;

        AutoDownload.IsChecked = s.AutoDownload;
        WarningSeconds.Text = s.WarningSeconds.ToString();
        CounselSeconds.Text = s.CounselSeconds.ToString();
        ShowClock.IsChecked = s.ShowClockWhenIdle;
        ShowNext.IsChecked = s.ShowNextPartWhenIdle;
        ShowDelay.IsChecked = s.ShowDelayOnDisplay;
        WebEnabled.IsChecked = s.WebServerEnabled;
        WebPort.Text = s.WebServerPort.ToString();
        TopmostBox.IsChecked = s.ControllerTopmost;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WarningSeconds.Text, out var warn) || warn < 0 || warn > 600)
        {
            Error("Avviso giallo: inserisci un numero di secondi tra 0 e 600.");
            return;
        }
        if (!int.TryParse(CounselSeconds.Text, out var counsel) || counsel < 10 || counsel > 600)
        {
            Error("Consiglio: inserisci un numero di secondi tra 10 e 600.");
            return;
        }
        if (!int.TryParse(WebPort.Text, out var port) || port < 1024 || port > 65535)
        {
            Error("Porta: inserisci un numero tra 1024 e 65535.");
            return;
        }

        // copia profonda per non modificare le impostazioni correnti finché non si salva
        var s = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_vm.Settings))!;
        if (LanguageBox.SelectedItem is WolLanguage lang) s.Language = lang;
        s.AutoDownload = AutoDownload.IsChecked == true;
        s.WarningSeconds = warn;
        s.CounselSeconds = counsel;
        s.ShowClockWhenIdle = ShowClock.IsChecked == true;
        s.ShowNextPartWhenIdle = ShowNext.IsChecked == true;
        s.ShowDelayOnDisplay = ShowDelay.IsChecked == true;
        s.WebServerEnabled = WebEnabled.IsChecked == true;
        s.WebServerPort = port;
        s.ControllerTopmost = TopmostBox.IsChecked == true;
        _vm.ApplySettings(s);
        DialogResult = true;
    }

    void Error(string msg) => MessageBox.Show(this, msg, "Impostazioni", MessageBoxButton.OK, MessageBoxImage.Warning);

    void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(_vm.DataFolder) { UseShellExecute = true }); } catch { }
    }
}
