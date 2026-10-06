using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TimerSala.App.Audio;
using TimerSala.App.ViewModels;
using TimerSala.Core.Models;
using TimerSala.Core.Storage;
using TimerSala.Core.Web;
using TimerSala.Core.Wol;

namespace TimerSala.App.Views;

public partial class SettingsWindow : Window
{
    public sealed record Option<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    readonly MainViewModel _vm;
    readonly bool _ready;

    // settimane con la visita del sorvegliante (da questa in poi)
    readonly System.Collections.ObjectModel.ObservableCollection<Option<DateOnly>> _overseerWeeks = [];
    readonly DateOnly _thisMonday;

    // prova dell'ingresso audio (scheda Voce)
    AudioInput? _test;
    DispatcherTimer? _meterTimer;
    double _testPeak = -90;
    bool _testVoice;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        WindowSizing.FitToScreen(this);
        WindowSizing.Remember(this, vm, "impostazioni");
        _vm = vm;
        var s = vm.Settings;

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Select(d => new Option<DayOfWeek>(d, It.TextInfo.ToTitleCase(It.DateTimeFormat.GetDayName(d)))).ToList();
        MidweekDay.ItemsSource = days;
        WeekendDay.ItemsSource = days;
        MidweekDay.SelectedItem = days.First(d => d.Value == s.MidweekDay);
        WeekendDay.SelectedItem = days.First(d => d.Value == s.WeekendDay);
        MidweekTime.Text = s.MidweekTime.ToString("HH:mm");
        var overseerDays = new List<Option<DayOfWeek?>> { new(null, "Stesso giorno") };
        overseerDays.AddRange(days.Select(d => new Option<DayOfWeek?>(d.Value, d.Label)));
        OverseerDay.ItemsSource = overseerDays;
        OverseerDay.SelectedItem = overseerDays.First(d => d.Value == s.OverseerMidweekDay);
        OverseerTime.Text = s.OverseerMidweekTime?.ToString("HH:mm") ?? "";
        _thisMonday = WeekMath.MondayOf(DateOnly.FromDateTime(DateTime.Today));
        foreach (var m in s.OverseerVisits.Where(m => m >= _thisMonday).Order()) _overseerWeeks.Add(WeekOption(m));
        OverseerList.ItemsSource = _overseerWeeks;
        RefreshOverseerWeeks();
        WeekendTime.Text = s.WeekendTime.ToString("HH:mm");
        MeetingLength.Text = s.MeetingLengthMinutes.ToString();
        CountdownMinutes.Text = s.CountdownMinutes.ToString();
        AdaptiveStudy.IsChecked = s.AdaptiveStudy;
        AdaptiveWatchtower.IsChecked = s.AdaptiveWatchtower;

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
            new(DisplayLayout.Classic, "Classico — titolo, cifre e barra"),
            new(DisplayLayout.Hourglass, "Clessidra — lo sfondo si svuota col tempo"),
            new(DisplayLayout.DigitsOnly, "Solo cifre — massima grandezza"),
        };
        LayoutBox.ItemsSource = layouts;
        LayoutBox.SelectedItem = layouts.First(l => l.Value == s.DisplayLayout);
        var cdStyles = new List<Option<CountdownStyle>>
        {
            new(CountdownStyle.Tide, "Marea — lo schermo si riempie fino all'inizio"),
            new(CountdownStyle.Ring, "Anello — blu notte, l'anello si chiude"),
            new(CountdownStyle.Blocks, "Blocchi — cifre grandi, un blocco per minuto"),
            new(CountdownStyle.Clock, "Orologio — l'ora in grande, l'inizio sotto"),
            new(CountdownStyle.Dial, "Quadrante — minuti al centro, secondi sulle tacche"),
            new(CountdownStyle.Words, "A parole — «si comincia tra 4 minuti»"),
            new(CountdownStyle.Classic, "Classico — come una parte"),
        };
        CountdownStyleBox.ItemsSource = cdStyles;
        CountdownStyleBox.SelectedItem = cdStyles.First(c => c.Value == s.CountdownStyle);

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
        MessageFullSeconds.Text = s.MessageFullScreenSeconds.ToString();
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

        VoiceEnabled.IsChecked = s.VoiceStartEnabled;
        var devices = new List<Option<string>> { new("", "Predefinito di Windows") };
        devices.AddRange(AudioInput.Devices().Select(d => new Option<string>(d.Id, d.Name)));
        VoiceDevice.ItemsSource = devices;
        VoiceDevice.SelectedItem = devices.FirstOrDefault(d => d.Value == (s.VoiceInputDevice ?? "")) ?? devices[0];
        VoiceThreshold.Value = s.VoiceThresholdDb;
        VoicePause.Value = s.VoicePauseSeconds;
        VoiceMin.Value = s.VoiceMinSeconds;
        VoiceAutoArm.IsChecked = s.VoiceAutoArmNext;
        _ready = true;
        UpdateVoiceTexts();
    }

    // ───── scheda Voce: prova dell'ingresso ─────

    void Tabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready || e.OriginalSource != Tabs) return;
        if (VoiceTab.IsSelected) StartTest(); else StopTest();
    }

    void VoiceDevice_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_ready && VoiceTab.IsSelected) StartTest();
    }

    void VoiceSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        if (_test is not null) _test.ThresholdDb = VoiceThreshold.Value;
        UpdateVoiceTexts();
    }

    void UpdateVoiceTexts()
    {
        VoiceThresholdText.Text = $"{VoiceThreshold.Value:0} dB";
        VoicePauseText.Text = VoicePause.Value.ToString("0.0", It) + " s";
        VoiceMinText.Text = VoiceMin.Value.ToString("0.0", It) + " s";
        double t = MainViewModel.LevelOf(VoiceThreshold.Value);
        ThresholdLeft.Width = new GridLength(t, GridUnitType.Star);
        ThresholdRight.Width = new GridLength(1 - t, GridUnitType.Star);
    }

    void StartTest()
    {
        StopTest();
        var id = (VoiceDevice.SelectedItem as Option<string>)?.Value;
        _test = new AudioInput { ThresholdDb = VoiceThreshold.Value };
        _test.Frame += f =>
        {
            if (f.LevelDb > _testPeak) _testPeak = f.LevelDb;
            if (f.IsVoice) _testVoice = true;
        };
        try
        {
            _test.Start(string.IsNullOrEmpty(id) ? null : id);
            VoiceTestStatus.Text = $"In ascolto: {_test.DeviceName}. Il segno giallo è la soglia.";
            VoiceTestStatus.Foreground = (Brush)FindResource("MutedBrush");
        }
        catch (Exception ex)
        {
            _test.Dispose();
            _test = null;
            VoiceTestStatus.Text = "Ingresso non disponibile: " + ex.Message;
            VoiceTestStatus.Foreground = (Brush)FindResource("RedBrush");
            return;
        }
        _meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _meterTimer.Tick += (_, _) =>
        {
            double level = MainViewModel.LevelOf(_testPeak);
            var track = (FrameworkElement)VoiceMeter.Parent;
            VoiceMeter.Width = Math.Max(0, track.ActualWidth * level);
            VoiceLamp.Fill = _testVoice ? (Brush)FindResource("GreenBrush") : (Brush)FindResource("LineBrush");
            VoiceLampText.Text = _testVoice ? "Voce" : "Silenzio";
            _testPeak = -90;
            _testVoice = false;
        };
        _meterTimer.Start();
    }

    void StopTest()
    {
        _meterTimer?.Stop();
        _meterTimer = null;
        _test?.Dispose();
        _test = null;
        VoiceMeter.Width = 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopTest();
        base.OnClosed(e);
    }

    bool TryApply()
    {
        if (!TryTime(MidweekTime.Text, out var midTime) || !TryTime(WeekendTime.Text, out var wkTime))
            return Error("Orari: scrivi l'ora come 19:00.");
        TimeOnly? overseerTime = null;
        if (!string.IsNullOrWhiteSpace(OverseerTime.Text))
        {
            if (!TryTime(OverseerTime.Text, out var ot)) return Error("Visita del sorvegliante: scrivi l'ora come 19:00, o lasciala vuota.");
            overseerTime = ot;
        }
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
        if (!int.TryParse(MessageFullSeconds.Text, out var fullSeconds) || fullSeconds < 2 || fullSeconds > 60)
            return Error("Messaggi a tutto schermo: inserisci i secondi (tra 2 e 60).");
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
        // le visite passate restano: servono se si riapre una settimana già fatta
        s.OverseerVisits = s.OverseerVisits.Where(m => m < _thisMonday).Concat(_overseerWeeks.Select(w => w.Value)).Distinct().Order().ToList();
        s.OverseerMidweekDay = ((Option<DayOfWeek?>)OverseerDay.SelectedItem).Value;
        s.OverseerMidweekTime = overseerTime;
        s.CountdownMinutes = countdown;
        s.AdaptiveStudy = AdaptiveStudy.IsChecked == true;
        s.AdaptiveWatchtower = AdaptiveWatchtower.IsChecked == true;
        if (LanguageBox.SelectedItem is WolLanguage lang) s.Language = lang;
        s.AutoDownload = AutoDownload.IsChecked == true;

        s.WarningSeconds = warn;
        s.ControllerTopmost = TopmostBox.IsChecked == true;

        s.DisplayTheme = ((Option<DisplayTheme>)ThemeBox.SelectedItem).Value;
        s.DisplayLayout = ((Option<DisplayLayout>)LayoutBox.SelectedItem).Value;
        s.CountdownStyle = ((Option<CountdownStyle>)CountdownStyleBox.SelectedItem).Value;
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
        s.MessageFullScreenSeconds = fullSeconds;
        s.MessagesEnabled = MessagesEnabled.IsChecked == true;
        s.RemoteControlEnabled = RemoteEnabled.IsChecked == true;
        if (pin.Length >= 4) s.RemotePin = pin;
        s.WebServerEnabled = WebEnabled.IsChecked == true;
        s.WebServerPort = port;
        s.WebAddressMode = (AddressBox.SelectedItem as Option<string>)?.Value ?? "auto";
        s.VoiceStartEnabled = VoiceEnabled.IsChecked == true;
        var device = (VoiceDevice.SelectedItem as Option<string>)?.Value;
        s.VoiceInputDevice = string.IsNullOrEmpty(device) ? null : device;
        s.VoiceThresholdDb = VoiceThreshold.Value;
        s.VoicePauseSeconds = VoicePause.Value;
        s.VoiceMinSeconds = VoiceMin.Value;
        s.VoiceAutoArmNext = VoiceAutoArm.IsChecked == true;
        _vm.ApplySettings(s);
        if (MonitorBox.SelectedItem is Interop.MonitorInfo monitor) _vm.SelectedMonitor = monitor;
        _vm.TimerWindowVisible = ShowTimerWindow.IsChecked == true;
        return true;
    }

    static bool TryTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text.Trim().Replace('.', ':'), ["H:mm", "HH:mm"], It, DateTimeStyles.None, out time);

    void Apply_Click(object sender, RoutedEventArgs e) => TryApply();

    static Option<DateOnly> WeekOption(DateOnly monday) => new(monday, WeekMath.Label(monday));

    /// <summary>Le prossime 52 settimane non ancora segnate.</summary>
    void RefreshOverseerWeeks()
    {
        var taken = _overseerWeeks.Select(w => w.Value).ToHashSet();
        var weeks = Enumerable.Range(0, 52).Select(i => _thisMonday.AddDays(7 * i)).Where(m => !taken.Contains(m)).Select(WeekOption).ToList();
        OverseerWeekBox.ItemsSource = weeks;
        OverseerWeekBox.SelectedIndex = weeks.Count > 0 ? 0 : -1;
        OverseerEmpty.Visibility = _overseerWeeks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void AddOverseerWeek_Click(object sender, RoutedEventArgs e)
    {
        if (OverseerWeekBox.SelectedItem is not Option<DateOnly> week) return;
        int i = 0;
        while (i < _overseerWeeks.Count && _overseerWeeks[i].Value < week.Value) i++;
        _overseerWeeks.Insert(i, week);
        RefreshOverseerWeeks();
    }

    void RemoveOverseerWeek_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button { Tag: DateOnly monday }) return;
        var item = _overseerWeeks.FirstOrDefault(w => w.Value == monday);
        if (item is not null) _overseerWeeks.Remove(item);
        RefreshOverseerWeeks();
    }

    void PreviewCountdown_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApply()) return;
        _vm.PreviewCountdown();
        if (!_vm.TimerWindowVisible)
            MessageBox.Show(this, "Lo schermo del timer è nascosto: attiva «Mostra il timer su questo schermo» per vedere l'anteprima.",
                "Impostazioni", MessageBoxButton.OK, MessageBoxImage.Information);
    }

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
