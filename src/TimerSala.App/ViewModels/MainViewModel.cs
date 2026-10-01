using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TimerSala.App.Interop;
using TimerSala.Core.Models;
using TimerSala.Core.Storage;
using TimerSala.Core.Timing;
using TimerSala.Core.Web;
using TimerSala.Core.Wol;

using System.Windows.Input;

namespace TimerSala.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    static readonly Brush Green = BrushCache.Get("#22C55E");
    static readonly Brush Amber = BrushCache.Get("#FBBF24");
    static readonly Brush Red = BrushCache.Get("#EF4444");
    static readonly Brush White = BrushCache.Get("#F4F5F7");
    static readonly Brush Muted = BrushCache.Get("#8D94A0");
    static readonly Brush Black = BrushCache.Get("#000000");
    static readonly Brush OvertimeBg = BrushCache.Get("#1D0505");

    readonly DataStore _store;
    readonly WolClient _wol;
    readonly TimerWebServer _web;
    readonly DispatcherTimer _tick;
    CancellationTokenSource? _downloadCts;
    int _lastStoppedIndex = -1;

    public AppSettings Settings { get; private set; }
    public MeetingTimer Timer { get; } = new();
    public ObservableCollection<PartItemViewModel> Parts { get; } = [];
    public ObservableCollection<MonitorInfo> AvailableMonitors { get; } = [];

    /// <summary>Chiamata quando serve una conferma dall'utente (impostata dalla finestra).</summary>
    public Func<string, bool> Confirm { get; set; } = _ => true;

    /// <summary>Richiesta di mostrare/nascondere/spostare lo schermo del timer.</summary>
    public event EventHandler? DisplayTargetChanged;

    public MainViewModel(DataStore store)
    {
        _store = store;
        Settings = store.LoadSettings();
        _wol = new WolClient { DiagnosticsFolder = store.DiagnosticsFolder };
        _web = new TimerWebServer(Timer, () => new DisplayOptions(Settings.ShowClockWhenIdle, Settings.ShowNextPartWhenIdle, Settings.ShowDelayOnDisplay));
        ApplyTimerSettings();

        Timer.StateChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(RefreshParts);

        Week = new WeekSchedule();
        _suppressVisibilityEvent = true;
        TimerWindowVisible = Settings.TimerWindowVisible;
        _suppressVisibilityEvent = false;
        RefreshMonitors();

        var today = DateOnly.FromDateTime(DateTime.Today);
        Kind = today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? MeetingKind.Weekend : MeetingKind.Midweek;
        LoadWeek(today);

        _tick = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        _tick.Tick += (_, _) => RefreshDisplay();
        _tick.Start();
        RefreshDisplay();
    }

    // ───────────── Settimana e adunanza ─────────────

    public WeekSchedule Week { get; set => Set(ref field, value); }
    public MeetingKind Kind { get; set => Set(ref field, value); }
    public string WeekTitle { get; set => Set(ref field, value); } = "";
    public string WeekSubtitle { get; set => Set(ref field, value); } = "";
    public bool IsBusy { get; set => Set(ref field, value); }
    public string? StatusMessage { get; set => Set(ref field, value); }
    public bool StatusIsError { get; set => Set(ref field, value); }

    public bool IsMidweek
    {
        get => Kind == MeetingKind.Midweek;
        set { if (value) SwitchKind(MeetingKind.Midweek); }
    }

    public bool IsWeekend
    {
        get => Kind == MeetingKind.Weekend;
        set { if (value) SwitchKind(MeetingKind.Weekend); }
    }

    public bool OverseerVisit
    {
        get => Week.CircuitOverseerVisit;
        set => SetOverseerVisit(value);
    }

    public Meeting CurrentMeeting => Week.Get(Kind);

    void SwitchKind(MeetingKind kind)
    {
        if (kind == Kind) return;
        if (Timer.IsRunning)
        {
            ShowStatus("Ferma il timer prima di cambiare adunanza.", error: true);
            OnPropertyChanged(nameof(IsMidweek));
            OnPropertyChanged(nameof(IsWeekend));
            return;
        }
        Kind = kind;
        OnPropertyChanged(nameof(IsMidweek));
        OnPropertyChanged(nameof(IsWeekend));
        ApplyMeeting();
    }

    public void LoadWeek(DateOnly anyDay)
    {
        if (Timer.IsRunning)
        {
            ShowStatus("Ferma il timer prima di cambiare settimana.", error: true);
            return;
        }
        var monday = WeekMath.MondayOf(anyDay);
        var saved = _store.LoadWeek(monday);
        Week = saved ?? new WeekSchedule
        {
            WeekStart = monday,
            Midweek = MeetingTemplates.DefaultMidweek(),
            Weekend = MeetingTemplates.DefaultWeekend(),
        };
        ApplyMeeting();
        StatusMessage = null;
        if (saved is null && Settings.AutoDownload)
            _ = DownloadAsync(silent: true);
    }

    void ApplyMeeting()
    {
        Timer.LoadMeeting(CurrentMeeting);
        _lastStoppedIndex = -1;
        RebuildParts();
        UpdateWeekTexts();
        OnPropertyChanged(nameof(OverseerVisit));
        OnPropertyChanged(nameof(CurrentMeeting));
    }

    void UpdateWeekTexts()
    {
        WeekTitle = WeekMath.Label(Week.WeekStart);
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(Week.BibleReading)) bits.Add(Week.BibleReading!);
        if (Week.EditedManually) bits.Add("modificato");
        else if (Week.FetchedAt is { } f) bits.Add($"da wol.jw.org il {f:dd/MM}");
        else bits.Add("schema predefinito");
        WeekSubtitle = string.Join(" · ", bits);
    }

    public ICommand PreviousWeekCommand => field ??= new RelayCommand(PreviousWeek);


    void PreviousWeek() => LoadWeek(Week.WeekStart.AddDays(-7));
    public ICommand NextWeekCommand => field ??= new RelayCommand(NextWeek);

    void NextWeek() => LoadWeek(Week.WeekStart.AddDays(7));
    public ICommand ThisWeekCommand => field ??= new RelayCommand(ThisWeek);

    void ThisWeek() => LoadWeek(DateOnly.FromDateTime(DateTime.Today));

    public ICommand DownloadCommand => field ??= new RelayCommand(() => _ = Download());


    Task Download() => DownloadAsync(silent: false);

    async Task DownloadAsync(bool silent)
    {
        if (!silent && Week.EditedManually &&
            !Confirm("Questa settimana è stata modificata a mano.\nScaricando di nuovo lo schema le modifiche andranno perse. Continuare?"))
            return;

        _downloadCts?.Cancel();
        var cts = _downloadCts = new CancellationTokenSource();
        var monday = Week.WeekStart;
        IsBusy = true;
        ShowStatus("Download da wol.jw.org…");
        try
        {
            var fetched = await _wol.FetchWeekAsync(monday, Settings.Language, cts.Token);
            if (cts.IsCancellationRequested || Week.WeekStart != monday) return;
            if (Timer.IsRunning)
            {
                ShowStatus("Schema scaricato: verrà applicato quando fermi il timer.");
                // salva comunque: sarà caricato alla prossima apertura della settimana
                _store.SaveWeek(fetched);
                return;
            }
            if (Week.CircuitOverseerVisit)
            {
                fetched.CircuitOverseerVisit = true;
                fetched.Midweek = MeetingTemplates.ApplyOverseerVisit(fetched.Midweek);
                fetched.Weekend = MeetingTemplates.ApplyOverseerVisit(fetched.Weekend);
            }
            Week = fetched;
            _store.SaveWeek(Week);
            ApplyMeeting();
            ShowStatus("Schema aggiornato da wol.jw.org.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ShowStatus(silent ? "Schema non scaricato: in uso lo schema predefinito. " + ex.Message : ex.Message, error: true);
        }
        finally
        {
            if (ReferenceEquals(cts, _downloadCts)) IsBusy = false;
        }
    }

    void SetOverseerVisit(bool on)
    {
        if (on == Week.CircuitOverseerVisit) return;
        if (Timer.IsRunning)
        {
            ShowStatus("Ferma il timer prima di modificare l'adunanza.", error: true);
            OnPropertyChanged(nameof(OverseerVisit));
            return;
        }
        if (on)
        {
            Week.Midweek = MeetingTemplates.ApplyOverseerVisit(Week.Midweek);
            Week.Weekend = MeetingTemplates.ApplyOverseerVisit(Week.Weekend);
        }
        else
        {
            if (Week.EditedManually && !Confirm("Ripristinare lo schema senza la visita del sorvegliante?\nLe modifiche manuali di questa settimana andranno perse."))
            {
                OnPropertyChanged(nameof(OverseerVisit));
                return;
            }
            Week.Midweek = Week.DownloadedMidweek?.Clone() ?? MeetingTemplates.DefaultMidweek();
            Week.Weekend = Week.DownloadedWeekend?.Clone() ?? MeetingTemplates.DefaultWeekend();
            Week.EditedManually = false;
        }
        Week.CircuitOverseerVisit = on;
        _store.SaveWeek(Week);
        ApplyMeeting();
        ShowStatus(on ? "Visita del sorvegliante: schemi adattati." : "Schema normale ripristinato.");
    }

    /// <summary>Salva un'adunanza modificata con l'editor.</summary>
    public void SaveEditedMeeting(Meeting meeting)
    {
        Week.Set(Kind, meeting);
        Week.EditedManually = true;
        _store.SaveWeek(Week);
        ApplyMeeting();
        ShowStatus("Modifiche salvate.");
    }

    public void ResetWeekToDownloaded()
    {
        _store.DeleteWeek(Week.WeekStart);
        LoadWeek(Week.WeekStart);
    }

    // ───────────── Comandi del timer ─────────────

    public ICommand ToggleStartCommand => field ??= new RelayCommand(ToggleStart);


    void ToggleStart()
    {
        if (Timer.IsRunning)
        {
            int running = Timer.Mode == TimerMode.Part ? Timer.RunningIndex : -1;
            Timer.Stop();
            _lastStoppedIndex = running;
        }
        else
        {
            Timer.Start();
            _lastStoppedIndex = -1;
        }
        RefreshDisplay();
    }

    public ICommand NextCommand => field ??= new RelayCommand(Next);


    void Next() => Timer.SelectNext();
    public ICommand PreviousCommand => field ??= new RelayCommand(Previous);

    void Previous() => Timer.SelectPrevious();

    public ICommand AddMinuteCommand => field ??= new RelayCommand(AddMinute);


    void AddMinute() => Adjust(+60);
    public ICommand RemoveMinuteCommand => field ??= new RelayCommand(RemoveMinute);

    void RemoveMinute() => Adjust(-60);

    void Adjust(int delta)
    {
        bool running = Timer.IsRunning;
        Timer.AdjustTarget(delta);
        if (!running)
        {
            Week.EditedManually = true;
            _store.SaveWeek(Week);
            UpdateWeekTexts();
        }
        RefreshDisplay();
    }

    public ICommand CounselCommand => field ??= new RelayCommand(Counsel);


    void Counsel()
    {
        Timer.StartCounsel();
        _lastStoppedIndex = -1;
        RefreshDisplay();
    }

    public ICommand SelectPartCommand => field ??= new RelayCommand(p => SelectPart(p as PartItemViewModel));


    void SelectPart(PartItemViewModel? item)
    {
        if (item is null || !item.IsTimed) return;
        Timer.Select(item.Index);
    }

    public ICommand ResetSelectedCommand => field ??= new RelayCommand(ResetSelected);


    void ResetSelected() => Timer.ResetSelected();

    public ICommand ResetAllCommand => field ??= new RelayCommand(ResetAll);


    void ResetAll()
    {
        if (Confirm("Azzerare tutti i tempi registrati di questa adunanza?"))
            Timer.ResetAll();
    }

    public ICommand StartManualCommand => field ??= new RelayCommand(p => StartManual(p as string));


    void StartManual(string? minutes)
    {
        if (!int.TryParse(minutes, out var m) || m <= 0) return;
        Timer.StartManual(TimeSpan.FromMinutes(m), $"Timer {m} min");
        RefreshDisplay();
    }

    // ───────────── Stato visualizzato (controller e schermo) ─────────────

    public string DisplayText { get; set => Set(ref field, value); } = "--:--";
    public string TitleText { get; set => Set(ref field, value); } = "";
    public string SectionText { get; set => Set(ref field, value); } = "";
    public Brush SectionBrush { get; set => Set(ref field, value); } = Muted;
    public Brush PhaseBrush { get; set => Set(ref field, value); } = White;
    public Brush DisplayBackground { get; set => Set(ref field, value); } = Black;
    public double Progress { get; set => Set(ref field, value); }
    public bool IsRunning { get; set => Set(ref field, value); }
    public bool IsIdle { get; set => Set(ref field, value); } = true;
    public TimerPhase Phase { get; set => Set(ref field, value); }
    public string ClockText { get; set => Set(ref field, value); } = "";
    public string ClockSeconds { get; set => Set(ref field, value); } = "";
    public string DelayText { get; set => Set(ref field, value); } = "";
    public Brush DelayBrush { get; set => Set(ref field, value); } = Muted;
    public string NextText { get; set => Set(ref field, value); } = "";
    public string InfoText { get; set => Set(ref field, value); } = "";
    public bool CanCounsel { get; set => Set(ref field, value); }
    public bool IsCounselOrManual { get; set => Set(ref field, value); }

    // proprietà dello schermo del timer (dipendono dalle impostazioni)
    public string ScreenDigits { get; set => Set(ref field, value); } = "";
    public string ScreenTitle { get; set => Set(ref field, value); } = "";
    public string ScreenFooterLeft { get; set => Set(ref field, value); } = "";
    public string ScreenFooterRight { get; set => Set(ref field, value); } = "";

    void RefreshDisplay()
    {
        var s = Timer.GetSnapshot();
        IsRunning = s.IsRunning;
        IsIdle = s.Phase == TimerPhase.Idle;
        Phase = s.Phase;
        IsCounselOrManual = s.IsRunning && s.Mode != TimerMode.Part;
        ClockText = s.Now.ToString("HH:mm");
        ClockSeconds = s.Now.ToString("ss");

        PhaseBrush = s.Phase switch
        {
            TimerPhase.Warning => Amber,
            TimerPhase.Overtime => Red,
            TimerPhase.Normal => Green,
            _ => White,
        };
        DisplayBackground = s.Phase == TimerPhase.Overtime ? OvertimeBg : Black;

        SectionInfo.TryParse(s.Section, out var section);
        SectionBrush = s.Section is null ? Muted : BrushCache.Get(SectionInfo.Color(section));

        if (s.Phase == TimerPhase.Idle)
        {
            DisplayText = s.TargetSeconds > 0 ? TimerSnapshot.FormatRemaining(s.TargetSeconds) : "--:--";
            TitleText = s.Title;
            SectionText = "PRONTO";
            Progress = 0;
            InfoText = s.TargetSeconds > 0 ? $"Durata {TimerSnapshot.FormatDuration(s.TargetSeconds)}" : "Nessuna parte selezionata";
            NextText = "";

            ScreenDigits = Settings.ShowClockWhenIdle ? ClockText : "";
            ScreenTitle = Settings.ShowNextPartWhenIdle && !string.IsNullOrEmpty(s.Title) ? $"Prossima: {s.Title}" : "";
            ScreenFooterLeft = "";
        }
        else
        {
            DisplayText = s.Display;
            TitleText = s.Title;
            SectionText = s.Mode switch
            {
                TimerMode.Counsel => "CONSIGLIO",
                TimerMode.Manual => "TIMER",
                _ => s.Section is null ? "" : SectionInfo.Label(section).ToUpperInvariant(),
            };
            Progress = s.Phase == TimerPhase.Overtime ? 1 : s.Progress;
            InfoText = $"Assegnato {TimerSnapshot.FormatDuration(s.TargetSeconds)} · trascorso {TimerSnapshot.FormatDuration(s.ElapsedSeconds)}";
            NextText = s.NextTitle is null ? "" : $"Dopo: {s.NextTitle}";

            ScreenDigits = s.Display;
            ScreenTitle = s.Title;
            ScreenFooterLeft = s.Mode == TimerMode.Part ? NextText : "";
        }

        if (Math.Abs(s.DelaySeconds) < 5)
        {
            DelayText = "In orario";
            DelayBrush = Muted;
        }
        else
        {
            var d = TimerSnapshot.FormatDuration(Math.Abs(s.DelaySeconds));
            DelayText = s.DelaySeconds > 0 ? $"Ritardo +{d}" : $"Anticipo −{d}";
            DelayBrush = s.DelaySeconds > 0 ? Red : Green;
        }
        ScreenFooterRight = Settings.ShowDelayOnDisplay && Math.Abs(s.DelaySeconds) >= 5 ? DelayText : (IsIdle ? "" : ClockText);

        CanCounsel = !s.IsRunning && _lastStoppedIndex >= 0 && _lastStoppedIndex < CurrentMeeting.Parts.Count
                     && CurrentMeeting.Parts[_lastStoppedIndex].HasCounsel;
    }

    void RebuildParts()
    {
        Parts.Clear();
        var m = Timer.Meeting;
        for (int i = 0; i < m.Parts.Count; i++)
            Parts.Add(new PartItemViewModel(m.Parts[i], i));
        RefreshParts();
    }

    void RefreshParts()
    {
        int selected = Timer.SelectedIndex;
        int running = Timer.Mode == TimerMode.Part ? Timer.RunningIndex : -1;
        foreach (var p in Parts)
        {
            p.IsSelected = p.Index == selected;
            p.IsRunning = p.Index == running;
            var actual = Timer.ActualFor(p.Index);
            p.ActualText = actual is { } a ? TimerSnapshot.FormatDuration(a.TotalSeconds) : null;
            p.IsOver = actual is { } b && b.TotalSeconds >= p.Part.DurationSeconds + 1;
            p.RefreshDuration();
        }
        RefreshDisplay();
    }

    // ───────────── Schermo del timer ─────────────

    public MonitorInfo? SelectedMonitor { get; set { if (Set(ref field, value)) OnSelectedMonitorChanged(value); } }
    public bool TimerWindowVisible { get; set { if (Set(ref field, value)) OnTimerWindowVisibleChanged(value); } }

    void OnSelectedMonitorChanged(MonitorInfo? value)
    {
        if (value is null) return;
        if (!_selectedMonitorSilent)
        {
            Settings.TimerMonitor = value.DeviceName;
            SaveSettings();
        }
        DisplayTargetChanged?.Invoke(this, EventArgs.Empty);
    }

    bool _suppressVisibilityEvent;

    void OnTimerWindowVisibleChanged(bool value)
    {
        if (_suppressVisibilityEvent) return;
        Settings.TimerWindowVisible = value;
        SaveSettings();
        DisplayTargetChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshMonitors()
    {
        var all = Monitors.GetAll();
        AvailableMonitors.Clear();
        foreach (var m in all) AvailableMonitors.Add(m);

        // preferenza: schermo salvato, altrimenti lo schermo 3, altrimenti l'ultimo non principale
        var target = all.FirstOrDefault(m => m.DeviceName == Settings.TimerMonitor)
                     ?? all.FirstOrDefault(m => m.Number == 3 && !m.IsPrimary)
                     ?? all.LastOrDefault(m => !m.IsPrimary)
                     ?? all.FirstOrDefault();
        if (all.Count == 1 && Settings.TimerMonitor is null && TimerWindowVisible)
        {
            // con un solo schermo il timer coprirebbe il controller: resta nascosto finché non lo si attiva
            _suppressVisibilityEvent = true;
            TimerWindowVisible = false;
            _suppressVisibilityEvent = false;
        }
        if (SelectedMonitor?.DeviceName != target?.DeviceName || SelectedMonitor?.Bounds != target?.Bounds)
        {
            _selectedMonitorSilent = true;
            SelectedMonitor = target;
            _selectedMonitorSilent = false;
        }
        DisplayTargetChanged?.Invoke(this, EventArgs.Empty);
    }

    // true mentre lo schermo viene scelto automaticamente (non va salvato come preferenza)
    bool _selectedMonitorSilent;

    // ───────────── Server web ─────────────

    public string? WebUrl { get; set => Set(ref field, value); }
    public string WebStatus { get; set => Set(ref field, value); } = "Server web disattivato";

    public async Task StartWebServerAsync()
    {
        await _web.StopAsync();
        WebUrl = null;
        if (!Settings.WebServerEnabled)
        {
            WebStatus = "Server web disattivato";
            return;
        }
        try
        {
            await _web.StartAsync(Settings.WebServerPort);
            var ip = TimerWebServer.LocalAddresses().FirstOrDefault() ?? "localhost";
            WebUrl = $"http://{ip}:{Settings.WebServerPort}";
            WebStatus = WebUrl;
        }
        catch (Exception ex)
        {
            WebStatus = $"Server web non avviato (porta {Settings.WebServerPort}): {ex.Message}";
        }
    }

    public Task StopWebServerAsync() => _web.StopAsync();

    // ───────────── Impostazioni ─────────────

    public void ApplySettings(AppSettings settings)
    {
        bool webChanged = settings.WebServerEnabled != Settings.WebServerEnabled || settings.WebServerPort != Settings.WebServerPort;
        Settings = settings;
        ApplyTimerSettings();
        SaveSettings();
        if (webChanged) _ = StartWebServerAsync();
        RefreshDisplay();
    }

    void ApplyTimerSettings()
    {
        Timer.WarningSeconds = Settings.WarningSeconds;
        Timer.CounselSeconds = Settings.CounselSeconds;
    }

    public void SaveSettings()
    {
        try { _store.SaveSettings(Settings); } catch { /* non bloccare l'interfaccia */ }
    }

    public string DataFolder => _store.Root;

    void ShowStatus(string message, bool error = false)
    {
        StatusMessage = message;
        StatusIsError = error;
    }
}
