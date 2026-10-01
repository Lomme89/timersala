using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using TimerSala.App.ViewModels;
using TimerSala.Core.Storage;
using TimerSala.Core.Web;
using TimerSala.Core.Wol;

namespace TimerSala.App.Views;

public partial class SettingsWindow : Window
{
    sealed record Option<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    readonly MainViewModel _vm;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        var s = vm.Settings;

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Select(d => new Option<DayOfWeek>(d, It.TextInfo.ToTitleCase(It.DateTimeFormat.GetDayName(d)))).ToList();
        MidweekDay.ItemsSource = days;
        WeekendDay.ItemsSource = days;
        MidweekDay.SelectedItem = days.First(d => d.Value == s.MidweekDay);
        WeekendDay.SelectedItem = days.First(d => d.Value == s.WeekendDay);
        MidweekTime.Text = s.MidweekTime.ToString("HH:mm");
        WeekendTime.Text = s.WeekendTime.ToString("HH:mm");
        MeetingLength.Text = s.MeetingLengthMinutes.ToString();
        CountdownMinutes.Text = s.CountdownMinutes.ToString();

        var langs = WolLanguage.Presets.ToList();
        if (!langs.Contains(s.Language)) langs.Add(s.Language);
        LanguageBox.ItemsSource = langs;
        LanguageBox.SelectedItem = s.Language;
        AutoDownload.IsChecked = s.AutoDownload;

        WarningSeconds.Text = s.WarningSeconds.ToString();
        TopmostBox.IsChecked = s.ControllerTopmost;

        var themes = new List<Option<DisplayTheme>> { new(DisplayTheme.Dark, "Scuro (consigliato in sala)"), new(DisplayTheme.Light, "Chiaro") };
        ThemeBox.ItemsSource = themes;
        ThemeBox.SelectedItem = themes.First(t => t.Value == s.DisplayTheme);
        var layouts = new List<Option<DisplayLayout>>
        {
            new(DisplayLayout.Classic, "Classico"),
            new(DisplayLayout.Hourglass, "Clessidra orizzontale"),
            new(DisplayLayout.DigitsOnly, "Solo cifre"),
        };
        LayoutBox.ItemsSource = layouts;
        LayoutBox.SelectedItem = layouts.First(l => l.Value == s.DisplayLayout);

        var suggested = new[] { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Variable Display Semibold", "Segoe UI", "Arial", "Verdana", "Consolas" };
        var installed = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToList();
        FontBox.ItemsSource = suggested.Concat(installed.Except(suggested)).ToList();
        FontBox.Text = s.DisplayFont;

        ColoredDigits.IsChecked = s.ColoredDigits;
        FlashOvertime.IsChecked = s.FlashOnOvertime;
        ShowTitle.IsChecked = s.ShowTitle;
        ShowSection.IsChecked = s.ShowSection;
        ShowBar.IsChecked = s.ShowProgressBar;
        ShowNextPart.IsChecked = s.ShowNextPart;
        ShowClockRunning.IsChecked = s.ShowClockWhileRunning;
        ShowDelay.IsChecked = s.ShowDelayOnDisplay;
        ShowClock.IsChecked = s.ShowClockWhenIdle;
        ShowNext.IsChecked = s.ShowNextPartWhenIdle;

        MessagesEnabled.IsChecked = s.MessagesEnabled;
        Presets.Text = string.Join(Environment.NewLine, s.MessagePresets);
        MessageSeconds.Text = s.MessageSeconds.ToString();
        RemoteEnabled.IsChecked = s.RemoteControlEnabled;
        RemotePin.Text = s.RemotePin;
        WebEnabled.IsChecked = s.WebServerEnabled;

        var addresses = new List<Option<string>>
        {
            new("auto", $"Automatico (consigliato) — {NetworkInfo.ResolveHost("auto")}"),
            new("hostname", $"Nome del PC — {Environment.MachineName}"),
        };
        addresses.AddRange(NetworkInfo.Addresses().Select(a => new Option<string>(a.Ip, $"{a.Ip} — {a.Adapter}{(a.IsVirtual ? " (virtuale)" : "")}")));
        AddressBox.ItemsSource = addresses;
        AddressBox.SelectedItem = addresses.FirstOrDefault(a => a.Value == s.WebAddressMode) ?? addresses[0];

        MonitorBox.ItemsSource = vm.AvailableMonitors;
        MonitorBox.SelectedItem = vm.SelectedMonitor;
        ShowTimerWindow.IsChecked = vm.TimerWindowVisible;
        WebPort.Text = s.WebServerPort.ToString();
    }

    bool TryApply()
    {
        if (!TryTime(MidweekTime.Text, out var midTime) || !TryTime(WeekendTime.Text, out var wkTime))
            return Error("Orari: scrivi l'ora come 19:00.");
        if (!int.TryParse(MeetingLength.Text, out var length) || length < 30 || length > 300)
            return Error("Durata adunanza: inserisci i minuti (tra 30 e 300).");
        if (!int.TryParse(CountdownMinutes.Text, out var countdown) || countdown < 0 || countdown > 60)
            return Error("Conto alla rovescia: inserisci i minuti (tra 0 e 60).");
        if (!int.TryParse(WarningSeconds.Text, out var warn) || warn < 0 || warn > 600)
            return Error("Avviso giallo: inserisci un numero di secondi tra 0 e 600.");
        if (!int.TryParse(WebPort.Text, out var port) || port < 1024 || port > 65535)
            return Error("Porta: inserisci un numero tra 1024 e 65535.");

        if (!int.TryParse(MessageSeconds.Text, out var msgSeconds) || msgSeconds < 0 || msgSeconds > 600)
            return Error("Messaggi: inserisci la durata in secondi (tra 0 e 600).");
        var pin = RemotePin.Text.Trim();
        if (RemoteEnabled.IsChecked == true && (pin.Length < 4 || pin.Length > 12 || !pin.All(char.IsDigit)))
            return Error("PIN: usa da 4 a 12 cifre.");

        // copia: le impostazioni correnti cambiano solo se tutto è valido
        var s = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_vm.Settings))!;
        s.MidweekDay = ((Option<DayOfWeek>)MidweekDay.SelectedItem).Value;
        s.WeekendDay = ((Option<DayOfWeek>)WeekendDay.SelectedItem).Value;
        s.MidweekTime = midTime;
        s.WeekendTime = wkTime;
        s.MeetingLengthMinutes = length;
        s.CountdownMinutes = countdown;
        if (LanguageBox.SelectedItem is WolLanguage lang) s.Language = lang;
        s.AutoDownload = AutoDownload.IsChecked == true;

        s.WarningSeconds = warn;
        s.ControllerTopmost = TopmostBox.IsChecked == true;

        s.DisplayTheme = ((Option<DisplayTheme>)ThemeBox.SelectedItem).Value;
        s.DisplayLayout = ((Option<DisplayLayout>)LayoutBox.SelectedItem).Value;
        s.DisplayFont = string.IsNullOrWhiteSpace(FontBox.Text) ? "Bahnschrift SemiBold" : FontBox.Text.Trim();
        s.ColoredDigits = ColoredDigits.IsChecked == true;
        s.FlashOnOvertime = FlashOvertime.IsChecked == true;
        s.ShowTitle = ShowTitle.IsChecked == true;
        s.ShowSection = ShowSection.IsChecked == true;
        s.ShowProgressBar = ShowBar.IsChecked == true;
        s.ShowNextPart = ShowNextPart.IsChecked == true;
        s.ShowClockWhileRunning = ShowClockRunning.IsChecked == true;
        s.ShowDelayOnDisplay = ShowDelay.IsChecked == true;
        s.ShowClockWhenIdle = ShowClock.IsChecked == true;
        s.ShowNextPartWhenIdle = ShowNext.IsChecked == true;

        s.MessagePresets = Presets.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(20).ToList();
        s.MessageSeconds = msgSeconds;
        s.MessagesEnabled = MessagesEnabled.IsChecked == true;
        s.RemoteControlEnabled = RemoteEnabled.IsChecked == true;
        if (pin.Length >= 4) s.RemotePin = pin;
        s.WebServerEnabled = WebEnabled.IsChecked == true;
        s.WebServerPort = port;
        s.WebAddressMode = (AddressBox.SelectedItem as Option<string>)?.Value ?? "auto";
        _vm.ApplySettings(s);
        if (MonitorBox.SelectedItem is Interop.MonitorInfo monitor) _vm.SelectedMonitor = monitor;
        _vm.TimerWindowVisible = ShowTimerWindow.IsChecked == true;
        return true;
    }

    static bool TryTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text.Trim().Replace('.', ':'), ["H:mm", "HH:mm"], It, DateTimeStyles.None, out time);

    void Apply_Click(object sender, RoutedEventArgs e) => TryApply();

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (TryApply()) DialogResult = true;
    }

    bool Error(string msg)
    {
        MessageBox.Show(this, msg, "Impostazioni", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    void Identify_Click(object sender, RoutedEventArgs e) => IdentifyWindow.ShowAll();

    void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(_vm.DataFolder) { UseShellExecute = true }); } catch { }
    }
}
