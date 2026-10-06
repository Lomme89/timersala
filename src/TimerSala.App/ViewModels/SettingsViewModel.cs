using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TimerSala.App.Interop;
using TimerSala.Core.Models;
using TimerSala.Core.Storage;
using TimerSala.Core.Timing;
using TimerSala.Core.Web;
using TimerSala.Core.Wol;

namespace TimerSala.App.ViewModels;

/// <summary>Una voce di un elenco a scelta: valore e testo mostrato.</summary>
public sealed record Choice<T>(T Value, string Label, string Detail = "")
{
    public override string ToString() => Label;
}

/// <summary>Uno stile del countdown, con il colore di sfondo per la miniatura.</summary>
public sealed record CountdownChoice(CountdownStyle Value, string Label, string Detail, string Color, string Accent);

/// <summary>Una frase pronta nell'elenco riordinabile.</summary>
public sealed class PresetItem(string text) : ObservableObject
{
    public string Text { get; set => Set(ref field, value); } = text;
    public int Number { get; set => Set(ref field, value); }
}

/// <summary>
/// Impostazioni 2.0: ogni modifica si applica subito (dopo un attimo, per non riavviare il server a ogni tasto),
/// «Annulla le modifiche» riporta tutto com'era all'apertura. I controlli non accettano valori fuori dai limiti,
/// quindi non ci sono più errori al salvataggio.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    readonly MainViewModel _main;
    readonly AppSettings _original;
    readonly DispatcherTimer _applyTimer;
    readonly DateOnly _thisMonday;
    bool _loading;

    /// <summary>Copia di lavoro: le modifiche vanno qui e poi al programma.</summary>
    AppSettings Draft { get; set; }

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        _original = Clone(main.Settings);
        Draft = Clone(main.Settings);
        _thisMonday = WeekMath.MondayOf(DateOnly.FromDateTime(DateTime.Today));
        _applyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _applyTimer.Tick += (_, _) => Flush();

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        Days = days.Select(d => new Choice<DayOfWeek>(d, It.TextInfo.ToTitleCase(It.DateTimeFormat.GetDayName(d)))).ToList();
        OverseerDays = [new(null, "Stesso giorno"), .. Days.Select(d => new Choice<DayOfWeek?>(d.Value, d.Label))];

        var langs = WolLanguage.Presets.ToList();
        if (!langs.Contains(Draft.Language)) langs.Add(Draft.Language);
        Languages = langs;

        var suggested = new[] { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Variable Display Semibold", "Segoe UI", "Arial", "Verdana", "Consolas" };
        var installed = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToList();
        FontNames = suggested.Concat(installed.Except(suggested)).ToList();

        Addresses =
        [
            new("auto", $"Automatico (consigliato) · {NetworkInfo.ResolveHost("auto")}"),
            new("hostname", $"Nome del PC · {Environment.MachineName}"),
            .. NetworkInfo.Addresses().Select(a => new Choice<string>(a.Ip, $"{a.Ip} · {a.Adapter}{(a.IsVirtual ? " (virtuale)" : "")}")),
        ];

        LoadLists();
        RefreshPreview();
    }

    static AppSettings Clone(AppSettings s) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;

    // ───── applicazione immediata ─────

    /// <summary>Una proprietà è cambiata: si applica tra un attimo.</summary>
    void Edit(Action<AppSettings> change, [CallerMemberName] string? name = null)
    {
        if (_loading) return;
        change(Draft);
        OnPropertyChanged(name);
        Changed();
    }

    void Changed()
    {
        RefreshPreview();
        OnPropertyChanged(nameof(EndTimesText));
        HasChanges = true;
        _applyTimer.Stop();
        _applyTimer.Start();
    }

    /// <summary>Applica subito le modifiche in sospeso (alla chiusura, prima di «Prova»).</summary>
    public void Flush()
    {
        _applyTimer.Stop();
        if (!HasPendingApply()) return;
        Apply(Draft);
    }

    bool HasPendingApply() => JsonSerializer.Serialize(Draft) != JsonSerializer.Serialize(_main.Settings);

    void Apply(AppSettings settings)
    {
        var copy = Clone(settings);
        _main.ApplySettings(copy);
        var monitor = _main.AvailableMonitors.FirstOrDefault(m => m.DeviceName == settings.TimerMonitor);
        if (monitor is not null && _main.SelectedMonitor?.DeviceName != monitor.DeviceName) _main.SelectedMonitor = monitor;
        _main.TimerWindowVisible = settings.TimerWindowVisible;
    }

    /// <summary>Ci sono modifiche rispetto all'apertura della finestra.</summary>
    public bool HasChanges { get; private set => Set(ref field, value); }

    /// <summary>Riporta tutto com'era all'apertura della finestra.</summary>
    public void Revert()
    {
        _applyTimer.Stop();
        Draft = Clone(_original);
        Apply(_original);
        _loading = true;
        LoadLists();
        _loading = false;
        HasChanges = false;
        RefreshPreview();
        OnPropertyChanged(string.Empty);
    }

    // ───── Adunanze ─────

    public IReadOnlyList<Choice<DayOfWeek>> Days { get; }
    public IReadOnlyList<Choice<DayOfWeek?>> OverseerDays { get; }
    public IReadOnlyList<WolLanguage> Languages { get; }

    public DayOfWeek MidweekDay { get => Draft.MidweekDay; set => Edit(s => s.MidweekDay = value); }
    public DayOfWeek WeekendDay { get => Draft.WeekendDay; set => Edit(s => s.WeekendDay = value); }
    public TimeOnly? MidweekTime { get => Draft.MidweekTime; set { if (value is { } t) Edit(s => s.MidweekTime = t); } }
    public TimeOnly? WeekendTime { get => Draft.WeekendTime; set { if (value is { } t) Edit(s => s.WeekendTime = t); } }
    public int MeetingLength { get => Draft.MeetingLengthMinutes; set => Edit(s => s.MeetingLengthMinutes = value); }

    /// <summary>«Fine prevista: 20:45 · 11:45».</summary>
    public string EndTimesText
    {
        get
        {
            var len = TimeSpan.FromMinutes(Draft.MeetingLengthMinutes);
            return $"Fine prevista: {Draft.MidweekTime.Add(len):HH:mm} infrasettimanale · {Draft.WeekendTime.Add(len):HH:mm} fine settimana";
        }
    }

    public WolLanguage Language { get => Draft.Language; set { if (value is not null) Edit(s => s.Language = value); } }
    public bool AutoDownload { get => Draft.AutoDownload; set => Edit(s => s.AutoDownload = value); }
    public bool AdaptiveStudy { get => Draft.AdaptiveStudy; set => Edit(s => s.AdaptiveStudy = value); }
    public bool AdaptiveWatchtower { get => Draft.AdaptiveWatchtower; set => Edit(s => s.AdaptiveWatchtower = value); }

    // settimane particolari: per ora la visita del sorvegliante

    public ObservableCollection<Choice<DateOnly>> OverseerWeeks { get; } = [];
    public ObservableCollection<Choice<DateOnly>> FreeWeeks { get; } = [];
    public Choice<DateOnly>? WeekToAdd { get; set => Set(ref field, value); }
    public bool HasOverseerWeeks => OverseerWeeks.Count > 0;

    public DayOfWeek? OverseerDay { get => Draft.OverseerMidweekDay; set => Edit(s => s.OverseerMidweekDay = value); }
    public TimeOnly? OverseerTime { get => Draft.OverseerMidweekTime; set => Edit(s => s.OverseerMidweekTime = value); }

    public ICommand AddOverseerWeekCommand => field ??= new RelayCommand(() =>
    {
        if (WeekToAdd is not { } week) return;
        SetOverseerWeeks(Draft.OverseerVisits.Append(week.Value));
    });

    public ICommand RemoveOverseerWeekCommand => field ??= new RelayCommand(p =>
    {
        if (p is DateOnly monday) SetOverseerWeeks(Draft.OverseerVisits.Where(m => m != monday));
    });

    void SetOverseerWeeks(IEnumerable<DateOnly> weeks)
    {
        Draft.OverseerVisits = weeks.Distinct().Order().ToList();
        LoadOverseerWeeks();
        Changed();
    }

    void LoadOverseerWeeks()
    {
        OverseerWeeks.Clear();
        // le visite passate restano salvate (servono se si riapre una settimana già fatta), ma non si mostrano
        foreach (var m in Draft.OverseerVisits.Where(m => m >= _thisMonday).Order())
            OverseerWeeks.Add(new(m, WeekMath.Label(m)));
        var taken = Draft.OverseerVisits.ToHashSet();
        FreeWeeks.Clear();
        foreach (var m in Enumerable.Range(0, 52).Select(i => _thisMonday.AddDays(7 * i)).Where(m => !taken.Contains(m)))
            FreeWeeks.Add(new(m, WeekMath.Label(m)));
        WeekToAdd = FreeWeeks.FirstOrDefault();
        OnPropertyChanged(nameof(HasOverseerWeeks));
    }

    // ───── Schermo ─────

    public ObservableCollection<MonitorInfo> Monitors => _main.AvailableMonitors;

    public MonitorInfo? Monitor
    {
        get => _main.AvailableMonitors.FirstOrDefault(m => m.DeviceName == Draft.TimerMonitor) ?? _main.SelectedMonitor;
        set { if (value is not null) Edit(s => s.TimerMonitor = value.DeviceName); }
    }

    public bool ShowTimerWindow { get => Draft.TimerWindowVisible; set => Edit(s => s.TimerWindowVisible = value); }

    public bool IsDarkScreen { get => Draft.DisplayTheme == DisplayTheme.Dark; set { if (value) SetScreenTheme(DisplayTheme.Dark); } }
    public bool IsLightScreen { get => Draft.DisplayTheme == DisplayTheme.Light; set { if (value) SetScreenTheme(DisplayTheme.Light); } }

    void SetScreenTheme(DisplayTheme theme)
    {
        Edit(s => s.DisplayTheme = theme, nameof(IsDarkScreen));
        OnPropertyChanged(nameof(IsLightScreen));
    }

    public bool LayoutClassic { get => Draft.DisplayLayout == DisplayLayout.Classic; set { if (value) SetLayout(DisplayLayout.Classic); } }
    public bool LayoutHourglass { get => Draft.DisplayLayout == DisplayLayout.Hourglass; set { if (value) SetLayout(DisplayLayout.Hourglass); } }
    public bool LayoutDigits { get => Draft.DisplayLayout == DisplayLayout.DigitsOnly; set { if (value) SetLayout(DisplayLayout.DigitsOnly); } }

    void SetLayout(DisplayLayout layout)
    {
        Edit(s => s.DisplayLayout = layout, nameof(LayoutClassic));
        OnPropertyChanged(nameof(LayoutHourglass));
        OnPropertyChanged(nameof(LayoutDigits));
        OnPropertyChanged(nameof(IsClassicLayout));
    }

    /// <summary>La barra di avanzamento c'è solo nel layout classico.</summary>
    public bool IsClassicLayout => Draft.DisplayLayout == DisplayLayout.Classic;

    public IReadOnlyList<string> FontNames { get; }

    public string DisplayFont
    {
        get => Draft.DisplayFont;
        set => Edit(s => s.DisplayFont = string.IsNullOrWhiteSpace(value) ? "Bahnschrift SemiBold" : value.Trim());
    }

    public int WarningSeconds { get => Draft.WarningSeconds; set => Edit(s => s.WarningSeconds = value); }
    public bool ColoredDigits { get => Draft.ColoredDigits; set => Edit(s => s.ColoredDigits = value); }
    public bool FlashOnOvertime { get => Draft.FlashOnOvertime; set => Edit(s => s.FlashOnOvertime = value); }

    // cosa mostrare: durante la parte / a riposo
    public bool ShowTitle { get => Draft.ShowTitle; set => Edit(s => s.ShowTitle = value); }
    public bool ShowSection { get => Draft.ShowSection; set => Edit(s => s.ShowSection = value); }
    public bool ShowProgressBar { get => Draft.ShowProgressBar; set => Edit(s => s.ShowProgressBar = value); }
    public bool ShowNextPart { get => Draft.ShowNextPart; set => Edit(s => s.ShowNextPart = value); }
    public bool ShowNextPartWhenIdle { get => Draft.ShowNextPartWhenIdle; set => Edit(s => s.ShowNextPartWhenIdle = value); }
    public bool ShowClockWhileRunning { get => Draft.ShowClockWhileRunning; set => Edit(s => s.ShowClockWhileRunning = value); }
    public bool ShowClockWhenIdle { get => Draft.ShowClockWhenIdle; set => Edit(s => s.ShowClockWhenIdle = value); }
    public bool ShowDelayOnDisplay { get => Draft.ShowDelayOnDisplay; set => Edit(s => s.ShowDelayOnDisplay = value); }

    // ───── Anteprima dello schermo ─────

    /// <summary>L'anteprima mostra lo schermo a riposo invece che durante una parte.</summary>
    public bool PreviewIdle { get; set { if (Set(ref field, value)) { OnPropertyChanged(nameof(PreviewRunning)); RefreshPreview(); } } }

    public bool PreviewRunning { get => !PreviewIdle; set => PreviewIdle = !value; }

    public Brush PreviewBackground { get; private set => Set(ref field, value); } = Brushes.Black;
    public Brush PreviewForeground { get; private set => Set(ref field, value); } = Brushes.White;
    public Brush PreviewMuted { get; private set => Set(ref field, value); } = Brushes.Gray;
    public Brush PreviewTrack { get; private set => Set(ref field, value); } = Brushes.DimGray;
    public Brush PreviewPhase { get; private set => Set(ref field, value); } = Brushes.Green;
    public Brush PreviewDigitsBrush { get; private set => Set(ref field, value); } = Brushes.White;
    public FontFamily PreviewFont { get; private set => Set(ref field, value); } = new("Bahnschrift SemiBold");
    public string PreviewDigits { get; private set => Set(ref field, value); } = "";
    public string PreviewTitle { get; private set => Set(ref field, value); } = "";
    public string PreviewSection { get; private set => Set(ref field, value); } = "";
    public string PreviewFooterLeft { get; private set => Set(ref field, value); } = "";
    public string PreviewFooterRight { get; private set => Set(ref field, value); } = "";
    public bool PreviewShowHeader { get; private set => Set(ref field, value); }
    public bool PreviewShowTitle { get; private set => Set(ref field, value); }
    public bool PreviewShowSection { get; private set => Set(ref field, value); }
    public bool PreviewShowBar { get; private set => Set(ref field, value); }
    public bool PreviewShowHourglass { get; private set => Set(ref field, value); }
    public double PreviewProgress { get; private set => Set(ref field, value); }
    public double PreviewRemaining { get; private set => Set(ref field, value); }

    const int SampleTarget = 600, SampleElapsed = 228;

    /// <summary>Stessa logica dello schermo vero, con una parte d'esempio (1. Discorso, 06:12 su 10:00).</summary>
    void RefreshPreview()
    {
        var s = Draft;
        var p = MainViewModel.PaletteFor(s.DisplayTheme);
        int remaining = SampleTarget - SampleElapsed;
        bool active = !PreviewIdle;
        var phase = remaining <= s.WarningSeconds ? p.Amber : p.Green;
        var layout = s.DisplayLayout;
        const string clock = "19:16";

        PreviewBackground = p.Bg;
        PreviewForeground = p.Fg;
        PreviewMuted = p.Muted;
        PreviewTrack = p.Track;
        PreviewPhase = phase;
        PreviewDigitsBrush = !active || !s.ColoredDigits || layout == DisplayLayout.Hourglass ? p.Fg : phase;
        PreviewFont = new FontFamily($"{s.DisplayFont}, Bahnschrift SemiBold, Segoe UI");
        PreviewShowHeader = layout != DisplayLayout.DigitsOnly;
        PreviewShowTitle = s.ShowTitle;
        PreviewShowSection = s.ShowSection;
        PreviewShowBar = layout == DisplayLayout.Classic && s.ShowProgressBar && active;
        PreviewShowHourglass = layout == DisplayLayout.Hourglass && active;
        PreviewProgress = SampleElapsed / (double)SampleTarget;
        PreviewRemaining = 1 - PreviewProgress;

        if (active)
        {
            PreviewDigits = TimerSnapshot.FormatRemaining(remaining);
            PreviewTitle = "1. Discorso";
            PreviewSection = "TESORI DELLA PAROLA DI DIO";
            PreviewFooterLeft = s.ShowNextPart ? "Dopo: 2. Gemme spirituali" : "";
            PreviewFooterRight = s.ShowDelayOnDisplay ? "Ritardo +1:20" : s.ShowClockWhileRunning ? clock : "";
        }
        else
        {
            PreviewDigits = s.ShowClockWhenIdle ? clock : "";
            PreviewTitle = s.ShowNextPartWhenIdle ? "Prossima: 1. Discorso" : "";
            PreviewSection = "Adunanza infrasettimanale";
            PreviewFooterLeft = "";
            PreviewFooterRight = s.ShowDelayOnDisplay ? "Ritardo +1:20" : "";
        }
    }

    // ───── Countdown ─────

    public int CountdownMinutes { get => Draft.CountdownMinutes; set => Edit(s => s.CountdownMinutes = value); }

    public IReadOnlyList<CountdownChoice> CountdownStyles { get; } = AllCountdownStyles;

    static readonly IReadOnlyList<CountdownChoice> AllCountdownStyles =
    [
        new(CountdownStyle.Tide, "Marea", "Lo schermo si riempie di blu fino all'inizio", "#123A52", "#A8D0E6"),
        new(CountdownStyle.Ring, "Anello", "Blu notte, un anello che si chiude", "#0A1724", "#7FB3D5"),
        new(CountdownStyle.Blocks, "Blocchi", "Cifre grandi e un blocco per ogni minuto", "#0D0F12", "#7FB3D5"),
        new(CountdownStyle.Clock, "Orologio", "L'ora attuale in grande, l'inizio sotto", "#0B0B0C", "#F4F5F7"),
        new(CountdownStyle.Dial, "Quadrante", "I minuti al centro, i secondi sulle tacche", "#0A0E14", "#E8F1F8"),
        new(CountdownStyle.Words, "A parole", "«Si comincia tra 4 minuti»", "#0C0C0D", "#D9D3C8"),
        new(CountdownStyle.Classic, "Classico", "Come una parte, con verde, giallo e rosso", "#000000", "#22C55E"),
    ];

    public CountdownChoice SelectedCountdownStyle
    {
        get => CountdownStyles.First(c => c.Value == Draft.CountdownStyle);
        set { if (value is not null) Edit(s => s.CountdownStyle = value.Value); }
    }

    // ───── Messaggi ─────

    public bool MessagesEnabled { get => Draft.MessagesEnabled; set => Edit(s => s.MessagesEnabled = value); }
    public int MessageSeconds { get => Draft.MessageSeconds; set => Edit(s => s.MessageSeconds = value); }
    public int MessageFullScreenSeconds { get => Draft.MessageFullScreenSeconds; set => Edit(s => s.MessageFullScreenSeconds = value); }

    public ObservableCollection<PresetItem> Presets { get; } = [];

    public const int MaxPresets = 20;

    public bool CanAddPreset => Presets.Count < MaxPresets;

    /// <summary>Aggiunge una frase vuota in fondo e la restituisce (la finestra ci mette il cursore).</summary>
    public PresetItem? AddPreset()
    {
        if (!CanAddPreset) return null;
        var item = WatchPreset(new PresetItem(""));
        Presets.Add(item);
        Renumber();
        return item;
    }

    public void RemovePreset(PresetItem item)
    {
        Presets.Remove(item);
        Renumber();
        SavePresets();
    }

    public void MovePreset(PresetItem item, int newIndex)
    {
        int old = Presets.IndexOf(item);
        newIndex = Math.Clamp(newIndex, 0, Presets.Count - 1);
        if (old < 0 || old == newIndex) return;
        Presets.Move(old, newIndex);
        Renumber();
        SavePresets();
    }

    void Renumber()
    {
        for (int i = 0; i < Presets.Count; i++) Presets[i].Number = i + 1;
        OnPropertyChanged(nameof(CanAddPreset));
    }

    void SavePresets()
    {
        if (_loading) return;
        Draft.MessagePresets = Presets.Select(p => p.Text.Trim()).Where(t => t.Length > 0).Take(MaxPresets).ToList();
        Changed();
    }

    // ───── Voce ─────

    public bool VoiceStartEnabled { get => Draft.VoiceStartEnabled; set => Edit(s => s.VoiceStartEnabled = value); }

    public string VoiceInputDevice
    {
        get => Draft.VoiceInputDevice ?? "";
        set => Edit(s => s.VoiceInputDevice = string.IsNullOrEmpty(value) ? null : value);
    }

    public double VoiceThresholdDb { get => Draft.VoiceThresholdDb; set => Edit(s => s.VoiceThresholdDb = Math.Round(value)); }
    public double VoicePauseSeconds { get => Draft.VoicePauseSeconds; set => Edit(s => s.VoicePauseSeconds = Math.Round(value * 2) / 2); }
    public double VoiceMinSeconds { get => Draft.VoiceMinSeconds; set => Edit(s => s.VoiceMinSeconds = Math.Round(value, 1)); }
    public bool VoiceAutoArmNext { get => Draft.VoiceAutoArmNext; set => Edit(s => s.VoiceAutoArmNext = value); }

    // ───── Rete e telefono ─────

    public IReadOnlyList<Choice<string>> Addresses { get; }

    public bool WebServerEnabled { get => Draft.WebServerEnabled; set => Edit(s => s.WebServerEnabled = value); }
    public int WebServerPort { get => Draft.WebServerPort; set => Edit(s => s.WebServerPort = value); }

    public string WebAddressMode
    {
        get => Addresses.Any(a => a.Value == Draft.WebAddressMode) ? Draft.WebAddressMode : "auto";
        set => Edit(s => s.WebAddressMode = value ?? "auto");
    }

    public bool RemoteControlEnabled { get => Draft.RemoteControlEnabled; set => Edit(s => s.RemoteControlEnabled = value); }

    public string RemotePin => Draft.RemotePin;

    /// <summary>Nuovo PIN: da 4 a 12 cifre. Restituisce false (e non cambia nulla) se non è valido.</summary>
    public bool TrySetPin(string pin)
    {
        pin = pin.Trim();
        if (pin.Length is < 4 or > 12 || !pin.All(char.IsAsciiDigit)) return false;
        if (pin != Draft.RemotePin) Edit(s => s.RemotePin = pin, nameof(RemotePin));
        return true;
    }

    public ICommand NewPinCommand => field ??= new RelayCommand(() => TrySetPin(Random.Shared.Next(1000, 10000).ToString()));

    // ───── Programma ─────

    public bool ControllerTopmost { get => Draft.ControllerTopmost; set => Edit(s => s.ControllerTopmost = value); }

    public bool ThemeDark { get => Draft.ControllerTheme == ControllerTheme.Dark; set { if (value) SetControllerTheme(ControllerTheme.Dark); } }
    public bool ThemeLight { get => Draft.ControllerTheme == ControllerTheme.Light; set { if (value) SetControllerTheme(ControllerTheme.Light); } }
    public bool ThemeAuto { get => Draft.ControllerTheme == ControllerTheme.Auto; set { if (value) SetControllerTheme(ControllerTheme.Auto); } }

    void SetControllerTheme(ControllerTheme theme)
    {
        Edit(s => s.ControllerTheme = theme, nameof(ThemeDark));
        OnPropertyChanged(nameof(ThemeLight));
        OnPropertyChanged(nameof(ThemeAuto));
    }

    public bool StartWithWindows { get => Draft.StartWithWindows; set => Edit(s => s.StartWithWindows = value); }
    public bool AutoMiniOnFirstPart { get => Draft.AutoMiniOnFirstPart; set => Edit(s => s.AutoMiniOnFirstPart = value); }
    public int UiScalePercent { get => Draft.UiScalePercent; set => Edit(s => s.UiScalePercent = value); }

    /// <summary>Opzioni rare (porta, indirizzo, regolazioni fini della voce).</summary>
    public bool ShowAdvanced { get; set => Set(ref field, value); }

    public string VersionText
    {
        get
        {
            var v = typeof(SettingsViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "";
            v = v.Split('+')[0];
            return v is "" or "1.0.0" ? "TimerSala · versione di sviluppo" : $"TimerSala {v}";
        }
    }

    public string DataFolder => _main.DataFolder;

    // ───── caricamento degli elenchi dalla copia di lavoro ─────

    void LoadLists()
    {
        LoadOverseerWeeks();
        Presets.Clear();
        foreach (var text in Draft.MessagePresets)
            Presets.Add(WatchPreset(new PresetItem(text)));
        Renumber();
    }

    PresetItem WatchPreset(PresetItem item)
    {
        item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PresetItem.Text)) SavePresets(); };
        return item;
    }
}
