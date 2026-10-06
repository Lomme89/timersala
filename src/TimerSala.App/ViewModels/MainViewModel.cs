using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TimerSala.App.Interop;
using TimerSala.App.Theming;
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

    public AppSettings Settings { get; private set; }
    public MeetingTimer Timer { get; } = new();
    public MessageBoard Messages { get; } = new();
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
        var session = store.LoadSession();
        if (session is { Timer.HasProgress: true } && DateTimeOffset.UtcNow - session.Timer.SavedAt < TimeSpan.FromHours(3)
            && !IsMeetingFinished(store, session))
            PendingRestore = session;
        else
            _sessionReady = true;
        _wol = new WolClient { DiagnosticsFolder = store.DiagnosticsFolder };
        _web = new TimerWebServer(Timer, () => DisplayOptions.From(Settings), Messages,
            () => new RemoteConfig(Settings.RemoteControlEnabled, Settings.RemotePin, Settings.MessagePresets, Settings.MessagesEnabled),
            (action, value) => Application.Current.Dispatcher.InvokeAsync(() => ExecuteRemote(action, value)).Task);
        Messages.Changed += (_, _) => Application.Current.Dispatcher.BeginInvoke(RefreshDisplay);
        ApplyTimerSettings();
        ApplyVoiceSettings();

        Timer.StateChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(RefreshParts);

        Week = new WeekSchedule();
        _suppressVisibilityEvent = true;
        TimerWindowVisible = Settings.TimerWindowVisible;
        _suppressVisibilityEvent = false;
        RefreshMonitors();
        _ = CheckClockAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        Kind = NextMeetingKind(DateTime.Now);
        LoadWeek(today);

        _tick = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        _tick.Tick += (_, _) =>
        {
            RefreshDisplay();
            TickVoice();
            KeepAwake.Set(ShouldStayAwake());
            // mentre il timer corre, salva lo stato ogni 5 secondi
            if (Timer.IsRunning && ++_ticksSinceSave >= 50) SaveSession();
        };
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
        SyncOverseerWeek();
        ApplyMeeting();
        StatusMessage = null;
        if (saved is null && Settings.AutoDownload)
            _ = DownloadAsync(silent: true);
    }

    void UpdateAdaptiveParts()
    {
        var parts = CurrentMeeting.Parts;
        var indices = new List<int>();
        if (Kind == MeetingKind.Midweek && Settings.AdaptiveStudy)
        {
            int i = parts.FindIndex(p => p.IsTimed && p.Title.Contains("Studio biblico", StringComparison.OrdinalIgnoreCase));
            if (i < 0) i = parts.FindLastIndex(p => p.IsTimed && p.Section == PartSection.Living && p.DurationSeconds >= 25 * 60);
            if (i >= 0) indices.Add(i);
        }
        if (Kind == MeetingKind.Weekend && Settings.AdaptiveWatchtower)
        {
            int i = parts.FindIndex(p => p.IsTimed && p.Section == PartSection.Watchtower);
            if (i >= 0) indices.Add(i);
        }
        Timer.SetAdaptiveParts(indices);
    }

    void UpdateMeetingStart() =>
        Timer.MeetingStart = new DateTimeOffset(Settings.StartOf(Kind, Week.WeekStart));

    void ApplyMeeting()
    {
        _offer = null;
        Timer.LoadMeeting(CurrentMeeting);
        UpdateMeetingStart();
        UpdateAdaptiveParts();
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
        if (Week.CircuitOverseerVisit) bits.Add("visita del sorvegliante");
        if (Week.EditedManually) bits.Add("modificato");
        else if (Week.FetchedAt is { } f) bits.Add($"da wol.jw.org il {f:dd/MM}");
        else bits.Add("schema predefinito");
        WeekSubtitle = string.Join(" · ", bits);
        UpdateMeetingLine();
    }

    /// <summary>L'adunanza di oggi, se c'è, altrimenti la prossima della settimana.</summary>
    MeetingKind NextMeetingKind(DateTime now)
    {
        var monday = WeekMath.MondayOf(DateOnly.FromDateTime(now));
        var mid = Settings.StartOf(MeetingKind.Midweek, monday);
        var wkd = Settings.StartOf(MeetingKind.Weekend, monday);
        if (mid.Date == now.Date) return MeetingKind.Midweek;
        if (wkd.Date == now.Date) return MeetingKind.Weekend;
        bool midAhead = mid > now, wkdAhead = wkd > now;
        if (midAhead && (!wkdAhead || mid < wkd)) return MeetingKind.Midweek;
        if (wkdAhead) return MeetingKind.Weekend;
        return mid > wkd ? MeetingKind.Midweek : MeetingKind.Weekend;
    }

    // ───────────── Modalità mini ─────────────

    public event EventHandler? MiniModeChanged;

    public bool IsMiniMode
    {
        get => Settings.MiniMode;
        set
        {
            if (Settings.MiniMode == value) return;
            Settings.MiniMode = value;
            SaveSettings();
            OnPropertyChanged();
            MiniModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ICommand ToggleMiniCommand => field ??= new RelayCommand(() => IsMiniMode = !IsMiniMode);

    public ICommand PreviousWeekCommand => field ??= new RelayCommand(PreviousWeek);


    void PreviousWeek() => LoadWeek(Week.WeekStart.AddDays(-7));
    public ICommand NextWeekCommand => field ??= new RelayCommand(NextWeek);

    void NextWeek() => LoadWeek(Week.WeekStart.AddDays(7));
    public ICommand ThisWeekCommand => field ??= new RelayCommand(ThisWeek);

    void ThisWeek() => LoadWeek(DateOnly.FromDateTime(DateTime.Today));

    public ICommand DownloadCommand => field ??= new RelayCommand(() => _ = Download());


    Task Download() => DownloadAsync(silent: false);

    public ICommand DownloadAllCommand => field ??= new RelayCommand(() => _ = DownloadAllAsync());

    /// <summary>Scarica tutte le settimane pubblicate a partire da quella visualizzata.</summary>
    async Task DownloadAllAsync()
    {
        if (IsBusy) return;
        _downloadCts?.Cancel();
        var cts = _downloadCts = new CancellationTokenSource();
        IsBusy = true;
        var progress = new Progress<string>(m => ShowStatus(m));
        try
        {
            var result = await WeekSync.DownloadAheadAsync(_wol, _store, Settings.Language, Week.WeekStart, progress: progress, ct: cts.Token,
                isOverseerWeek: Settings.IsOverseerWeek);
            ShowStatus(result.Summary, error: result.Error is not null || result.Updated == 0);
            // ricarica la settimana visualizzata con lo schema appena scaricato
            if (!Timer.IsRunning && result.UpdatedWeeks.Contains(Week.WeekStart))
            {
                var keep = StatusMessage;
                LoadWeek(Week.WeekStart);
                ShowStatus(keep ?? "", error: StatusIsError);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, error: true);
        }
        finally
        {
            if (ReferenceEquals(cts, _downloadCts)) IsBusy = false;
        }
    }

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
            if (Week.CircuitOverseerVisit || Settings.IsOverseerWeek(monday))
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

    /// <summary>Pulsante «Sorvegliante» del controller: segna o toglie la settimana visualizzata dall'elenco delle visite.</summary>
    void SetOverseerVisit(bool on)
    {
        if (on == Week.CircuitOverseerVisit) return;
        if (Timer.IsRunning)
        {
            ShowStatus("Ferma il timer prima di modificare l'adunanza.", error: true);
            OnPropertyChanged(nameof(OverseerVisit));
            return;
        }
        if (!ChangeWeekOverseer(on))
        {
            OnPropertyChanged(nameof(OverseerVisit));
            return;
        }
        Settings.OverseerVisits.Remove(Week.WeekStart);
        if (on) Settings.OverseerVisits.Add(Week.WeekStart);
        Settings.OverseerVisits.Sort();
        SaveSettings();
        ApplyMeeting();
        ShowStatus(on ? "Visita del sorvegliante: schemi adattati." : "Schema normale ripristinato.");
    }

    /// <summary>Allinea la settimana visualizzata all'elenco delle visite pianificate nelle impostazioni.</summary>
    void SyncOverseerWeek()
    {
        if (Timer.IsRunning) return;
        var monday = Week.WeekStart;
        if (Week.CircuitOverseerVisit && !Settings.IsOverseerWeek(monday) && !_overseerListEdited)
        {
            // settimana segnata prima che esistesse l'elenco: la si aggiunge
            Settings.OverseerVisits.Add(monday);
            Settings.OverseerVisits.Sort();
            SaveSettings();
            return;
        }
        bool planned = Settings.IsOverseerWeek(monday);
        if (planned != Week.CircuitOverseerVisit) ChangeWeekOverseer(planned);
    }

    bool _overseerListEdited;

    /// <summary>Applica o toglie lo schema della visita alla settimana visualizzata; false se l'utente rinuncia.</summary>
    bool ChangeWeekOverseer(bool on)
    {
        if (on)
        {
            Week.Midweek = MeetingTemplates.ApplyOverseerVisit(Week.Midweek);
            Week.Weekend = MeetingTemplates.ApplyOverseerVisit(Week.Weekend);
        }
        else
        {
            if (Week.EditedManually && !Confirm("Ripristinare lo schema senza la visita del sorvegliante?\nLe modifiche manuali di questa settimana andranno perse."))
                return false;
            Week.Midweek = Week.DownloadedMidweek?.Clone() ?? MeetingTemplates.DefaultMidweek();
            Week.Weekend = Week.DownloadedWeekend?.Clone() ?? MeetingTemplates.DefaultWeekend();
            Week.EditedManually = false;
        }
        Week.CircuitOverseerVisit = on;
        _store.SaveWeek(Week);
        return true;
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
            bool part = Timer.Mode == TimerMode.Part;
            int stopped = Timer.RunningIndex;
            _stoppedFromPhone = false;
            Timer.Stop();
            if (part) AutoArmAfterStop(stopped);
        }
        else
        {
            // un nuovo avvio (o l'attesa della voce) chiude la possibilità di annullare la fermata
            ForgetUndo();
            if (!TryArmOrStartNow()) Timer.Start();
        }
        RefreshDisplay();
    }

    /// <summary>Dal telefono: avvio immediato, senza attesa della voce.</summary>
    void ToggleStartNow()
    {
        if (IsVoiceArmed) Disarm();
        _stoppedFromPhone = Timer.IsRunning;
        Timer.Toggle();
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
            UpdatePlannedStarts();
        }
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
        if (IsVoiceArmed) Disarm();
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

    public bool IsOvertime { get; set => Set(ref field, value); }
    public bool IsCounselOrManual { get; set => Set(ref field, value); }

    // proprietà dello schermo del timer (dipendono dalle impostazioni)
    public string ScreenDigits { get; set => Set(ref field, value); } = "";
    public string ScreenTitle { get; set => Set(ref field, value); } = "";
    public string ScreenFooterLeft { get; set => Set(ref field, value); } = "";
    public string ScreenFooterRight { get; set => Set(ref field, value); } = "";

    void RefreshDisplay()
    {
        var s = Timer.GetSnapshot();
        bool countdown = s.Mode == TimerMode.Countdown;
        IsRunning = s.IsRunning;
        IsIdle = s.Phase == TimerPhase.Idle;
        Phase = s.Phase;
        IsCountdown = countdown;
        IsCounselOrManual = s.IsRunning && s.Mode != TimerMode.Part;
        ClockText = s.Now.ToString("HH:mm");
        ClockSeconds = s.Now.ToString("ss");

        // colori del controller: seguono il tema (chiaro o scuro) dell'interfaccia
        PhaseBrush = s.Phase switch
        {
            TimerPhase.Warning => UiTheme.Brush("AmberBrush"),
            TimerPhase.Overtime => UiTheme.Brush("RedBrush"),
            TimerPhase.Normal => UiTheme.Brush("GreenBrush"),
            _ => UiTheme.Brush("TextBrush"),
        };

        SectionInfo.TryParse(s.Section, out var section);
        SectionBrush = s.Section is null ? UiTheme.Brush("MutedBrush") : BrushCache.Get(SectionInfo.Color(section));

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
            ScreenSection = s.MeetingTitle;
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
                TimerMode.Countdown => s.MeetingTitle.ToUpperInvariant(),
                _ => s.Section is null ? "" : SectionInfo.Label(section).ToUpperInvariant(),
            };
            Progress = s.Phase == TimerPhase.Overtime ? 1 : s.Progress;
            InfoText = countdown
                ? $"Inizio alle {Timer.MeetingStart:HH:mm}"
                : $"Assegnato {TimerSnapshot.FormatDuration(s.TargetSeconds)} · trascorso {TimerSnapshot.FormatDuration(s.ElapsedSeconds)}";
            NextText = s.NextTitle is null ? "" : countdown ? $"Prima parte: {s.NextTitle}" : $"Dopo: {s.NextTitle}";

            ScreenDigits = s.Display;
            ScreenTitle = s.Title;
            ScreenSection = SectionText;
            ScreenFooterLeft = Settings.ShowNextPart && s.Mode is TimerMode.Part or TimerMode.Countdown ? NextText : "";
        }

        IsOvertime = s.Phase == TimerPhase.Overtime;

        if (Math.Abs(s.DelaySeconds) < 5)
        {
            DelayText = "In orario";
            DelayBrush = UiTheme.Brush("MutedBrush");
        }
        else
        {
            var d = TimerSnapshot.FormatDuration(Math.Abs(s.DelaySeconds));
            DelayText = s.DelaySeconds > 0 ? $"Ritardo +{d}" : $"Anticipo −{d}";
            DelayBrush = UiTheme.Brush(s.DelaySeconds > 0 ? "RedBrush" : "GreenBrush");
        }
        ScreenFooterRight = Settings.ShowDelayOnDisplay && Math.Abs(s.DelaySeconds) >= 5 ? DelayText
            : !IsIdle && Settings.ShowClockWhileRunning ? ClockText : "";

        if (Timer.MeetingStart is { } start)
        {
            var end = start.AddMinutes(Settings.MeetingLengthMinutes).AddSeconds(Math.Max(0, s.DelaySeconds));
            ScheduleText = $"Inizio {start:HH:mm} · fine prevista {end:HH:mm}";
        }

        RefreshScreenStyle();
        RefreshCountdown(s);
        RefreshMessage();
        RefreshUndoStop();
        RefreshControllerCard(s);
    }

    // ───────────── Stile dello schermo del timer ─────────────

    internal sealed record Palette(Brush Bg, Brush Fg, Brush Muted, Brush Track, Brush Green, Brush Amber, Brush Red, Brush OvertimeBg);

    internal static Palette PaletteFor(DisplayTheme theme) => theme == DisplayTheme.Light ? LightPalette : DarkPalette;

    static readonly Palette DarkPalette = new(Black, White, Muted, BrushCache.Get("#262A32"), Green, Amber, Red, OvertimeBg);
    static readonly Palette LightPalette = new(BrushCache.Get("#FFFFFF"), BrushCache.Get("#111827"), BrushCache.Get("#4B5563"),
        BrushCache.Get("#E5E7EB"), BrushCache.Get("#15803D"), BrushCache.Get("#B45309"), BrushCache.Get("#B91C1C"), BrushCache.Get("#FDE2E2"));

    public Brush ScreenForeground { get; set => Set(ref field, value); } = White;
    public Brush ScreenMuted { get; set => Set(ref field, value); } = Muted;
    public Brush ScreenTrack { get; set => Set(ref field, value); } = BrushCache.Get("#262A32");
    public Brush ScreenDigitsBrush { get; set => Set(ref field, value); } = White;
    public Brush ScreenPhaseBrush { get; set => Set(ref field, value); } = Green;
    public string ScreenSection { get; set => Set(ref field, value); } = "";
    public FontFamily ScreenFont { get; set => Set(ref field, value); } = new("Bahnschrift SemiBold");
    public double RemainingFraction { get; set => Set(ref field, value); }
    public bool IsCountdown { get; set => Set(ref field, value); }
    public bool ShowScreenHeader { get; set => Set(ref field, value); } = true;
    public bool ShowScreenTitle { get; set => Set(ref field, value); } = true;
    public bool ShowScreenSection { get; set => Set(ref field, value); } = true;
    public bool ShowScreenBar { get; set => Set(ref field, value); } = true;
    public bool ShowScreenFooter { get; set => Set(ref field, value); } = true;
    public bool ShowHourglass { get; set => Set(ref field, value); }
    public bool FlashDigits { get; set => Set(ref field, value); }
    public string ScheduleText { get; set => Set(ref field, value); } = "";

    string? _fontName;

    void RefreshScreenStyle()
    {
        var p = Settings.DisplayTheme == DisplayTheme.Light ? LightPalette : DarkPalette;
        var layout = Settings.DisplayLayout;
        bool active = Phase != TimerPhase.Idle;

        var phaseBrush = Phase switch
        {
            TimerPhase.Warning => p.Amber,
            TimerPhase.Overtime => p.Red,
            _ => p.Green,
        };
        ScreenPhaseBrush = phaseBrush;
        DisplayBackground = Phase == TimerPhase.Overtime ? p.OvertimeBg : p.Bg;
        ScreenForeground = p.Fg;
        ScreenMuted = p.Muted;
        ScreenTrack = p.Track;
        // nella clessidra le cifre restano neutre per contrastare con il riempimento colorato
        ScreenDigitsBrush = !active || !Settings.ColoredDigits || layout == DisplayLayout.Hourglass ? p.Fg : phaseBrush;

        ShowScreenHeader = layout != DisplayLayout.DigitsOnly;
        ShowScreenTitle = Settings.ShowTitle;
        ShowScreenSection = Settings.ShowSection;
        ShowScreenFooter = layout != DisplayLayout.DigitsOnly;
        ShowScreenBar = layout == DisplayLayout.Classic && Settings.ShowProgressBar && active;
        ShowHourglass = layout == DisplayLayout.Hourglass && active;
        RemainingFraction = Phase == TimerPhase.Overtime ? 1 : 1 - Progress;
        FlashDigits = Settings.FlashOnOvertime && Phase == TimerPhase.Overtime;

        if (_fontName != Settings.DisplayFont)
        {
            _fontName = Settings.DisplayFont;
            ScreenFont = new FontFamily($"{Settings.DisplayFont}, Bahnschrift SemiBold, Segoe UI");
        }
    }

    void RebuildParts()
    {
        Parts.Clear();
        var m = Timer.Meeting;
        for (int i = 0; i < m.Parts.Count; i++)
            Parts.Add(new PartItemViewModel(m.Parts[i], i));
        UpdatePlannedStarts();
        RefreshParts();
    }

    void RefreshParts()
    {
        SaveSession();
        int selected = Timer.SelectedIndex;
        int running = Timer.Mode == TimerMode.Part ? Timer.RunningIndex : -1;
        int current = running >= 0 ? running : selected;
        foreach (var p in Parts)
        {
            p.IsSelected = p.Index == selected;
            p.IsRunning = p.Index == running;
            // le parti già fatte si compattano
            p.IsPast = current >= 0 && p.Index < current;
            var actual = Timer.ActualFor(p.Index);
            p.IsSkipped = actual == TimeSpan.Zero;
            p.ActualText = actual is { } a ? a == TimeSpan.Zero ? "saltata" : TimerSnapshot.FormatDuration(a.TotalSeconds) : null;
            p.IsOver = actual is { } b && b.TotalSeconds >= p.Part.DurationSeconds + 1;
            p.AdaptedSeconds = actual is null ? Timer.AdaptedTargetFor(p.Index) : null;
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
        // lo schermo della sala è sparito (cavo staccato, TV spenta) e resta solo il principale: il timer si nasconde
        // invece di coprire il controller, e torna da solo quando lo schermo si ricollega
        bool disconnected = Settings.TimerMonitor is not null && all.Count > 0
                            && all.All(m => m.DeviceName != Settings.TimerMonitor) && all.All(m => m.IsPrimary);
        if (disconnected != TimerScreenDisconnected)
        {
            TimerScreenDisconnected = disconnected;
            ShowStatus(disconnected
                ? "Schermo della sala non collegato: il timer tornerà da solo quando si ricollega."
                : "Schermo della sala ricollegato.", error: disconnected);
        }
        if (SelectedMonitor?.DeviceName != target?.DeviceName || SelectedMonitor?.Bounds != target?.Bounds)
        {
            _selectedMonitorSilent = true;
            SelectedMonitor = target;
            _selectedMonitorSilent = false;
        }
        DisplayTargetChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Lo schermo scelto per la sala non c'è più e resta solo quello principale.</summary>
    public bool TimerScreenDisconnected { get; private set => Set(ref field, value); }

    // true mentre lo schermo viene scelto automaticamente (non va salvato come preferenza)
    bool _selectedMonitorSilent;

    // ───────────── Messaggi all'oratore ─────────────

    public string MessageText { get; set => Set(ref field, value); } = "";
    public string? ScreenMessage { get; set => Set(ref field, value); }
    public bool HasMessage { get; set => Set(ref field, value); }
    public string MessageInfo { get; set => Set(ref field, value); } = "";
    public IReadOnlyList<string> MessagePresets => Settings.MessagePresets;
    public bool MessagesEnabled => Settings.MessagesEnabled;

    public ICommand SendMessageCommand => field ??= new RelayCommand(p => ShowMessage(p as string ?? MessageText));
    public ICommand SendMessageFullCommand => field ??= new RelayCommand(p => ShowMessage(p as string ?? MessageText, fullScreen: true));
    public bool ScreenMessageFull { get; set => Set(ref field, value); }
    /// <summary>Fascia gialla in basso: c'è un messaggio e non è a tutto schermo.</summary>
    public bool ShowMessageBand { get; set => Set(ref field, value); }
    public ICommand ClearMessageCommand => field ??= new RelayCommand(() => Messages.Clear());

    /// <summary>Mostra un messaggio; <paramref name="fullScreen"/> = prima a tutto schermo, poi nella fascia.</summary>
    public void ShowMessage(string? text, bool fullScreen = false)
    {
        if (string.IsNullOrWhiteSpace(text) || !Settings.MessagesEnabled) return;
        Messages.Show(text, Settings.MessageSeconds > 0 ? TimeSpan.FromSeconds(Settings.MessageSeconds) : null,
            fullScreen ? TimeSpan.FromSeconds(Math.Max(2, Settings.MessageFullScreenSeconds)) : null);
        MessageText = "";
    }

    void RefreshMessage()
    {
        var current = Messages.Current;
        ScreenMessage = current;
        HasMessage = current is not null;
        ScreenMessageFull = current is not null && Messages.IsFullScreen;
        ShowMessageBand = current is not null && !ScreenMessageFull;
        MessageInfo = current is null ? ""
            : Messages.SecondsLeft is { } left ? $"Sullo schermo: «{current}» ({Math.Ceiling(left):0} s)"
            : $"Sullo schermo: «{current}»";
    }

    // ───────────── Controllo remoto ─────────────

    void ExecuteRemote(string action, string? value)
    {
        switch (action)
        {
            case RemoteActions.Toggle: ToggleStartNow(); break;
            case RemoteActions.Next: Timer.SelectNext(); break;
            case RemoteActions.Previous: Timer.SelectPrevious(); break;
            case RemoteActions.AddMinute: Adjust(+60); break;
            case RemoteActions.RemoveMinute: Adjust(-60); break;
            case RemoteActions.Select when int.TryParse(value, out var i): Timer.Select(i); break;
            case RemoteActions.Message: ShowMessage(value); break;
            case RemoteActions.MessageFull: ShowMessage(value, fullScreen: true); break;
            case RemoteActions.ClearMessage: Messages.Clear(); break;
        }
        RefreshDisplay();
    }

    public string? ControlUrl => WebUrl is null || !Settings.RemoteControlEnabled ? null : $"{WebUrl}/?pin={Uri.EscapeDataString(Settings.RemotePin)}";

    public void RegeneratePin()
    {
        Settings.RemotePin = Random.Shared.Next(1000, 10000).ToString();
        SaveSettings();
        OnPropertyChanged(nameof(ControlUrl));
    }

    // ───────────── Ripristino dopo un riavvio ─────────────

    /// <summary>Adunanza rimasta in corso alla chiusura precedente (da proporre all'avvio).</summary>
    public SessionState? PendingRestore { get; private set; }

    bool _sessionReady;
    int _ticksSinceSave;

    void SaveSession()
    {
        if (!_sessionReady) return;
        _ticksSinceSave = 0;
        var timer = Timer.ExportState();
        if (timer.HasProgress)
            _store.SaveSession(new SessionState { WeekStart = Week.WeekStart, Kind = Kind, Timer = timer });
        else
            _store.ClearSession();
    }

    /// <summary>L'adunanza salvata era già finita (ultima parte cronometrata e timer fermo).</summary>
    static bool IsMeetingFinished(DataStore store, SessionState s)
    {
        if (s.Timer.Running) return false;
        var parts = store.LoadWeek(s.WeekStart)?.Get(s.Kind).Parts;
        if (parts is null) return false;
        int last = parts.FindLastIndex(p => p.IsTimed);
        return last >= 0 && s.Timer.ActualSeconds.ContainsKey(last);
    }

    public enum RestoreChoice { CountDowntime, ResumeAsWas, StartOver }

    public void Restore(RestoreChoice choice)
    {
        var session = PendingRestore;
        PendingRestore = null;
        _sessionReady = true;
        if (session is null || choice == RestoreChoice.StartOver)
        {
            _store.ClearSession();
            return;
        }
        Kind = session.Kind;
        OnPropertyChanged(nameof(IsMidweek));
        OnPropertyChanged(nameof(IsWeekend));
        LoadWeek(session.WeekStart);
        Timer.ImportState(session.Timer, countDowntime: choice == RestoreChoice.CountDowntime);
        RefreshParts();
        ShowStatus("Adunanza ripristinata.");
    }

    /// <summary>Descrizione per la finestra di ripristino.</summary>
    public string DescribeRestore(SessionState s)
    {
        var week = _store.LoadWeek(s.WeekStart);
        var meeting = week?.Get(s.Kind);
        int index = s.Timer.Running ? s.Timer.RunningIndex : s.Timer.SelectedIndex;
        string part = meeting is not null && index >= 0 && index < meeting.Parts.Count ? meeting.Parts[index].Title : "";
        string kind = s.Kind == MeetingKind.Midweek ? "adunanza infrasettimanale" : "adunanza del fine settimana";
        var ago = DateTimeOffset.UtcNow - s.Timer.SavedAt;
        string when = ago.TotalMinutes < 1 ? "meno di un minuto fa" : $"{(int)ago.TotalMinutes} minuti fa";
        return s.Timer.Running
            ? $"TimerSala si è chiuso {when} durante l'{kind}, mentre era in corso «{part}»."
            : $"TimerSala si è chiuso {when} durante l'{kind} ({s.Timer.ActualSeconds.Count} parti già cronometrate).";
    }

    // ───────────── Server web ─────────────

    public string? WebUrl { get; set => Set(ref field, value); }
    public string WebStatus { get; set => Set(ref field, value); } = "Server web disattivato";
    public bool WebRunning { get; set => Set(ref field, value); }

    /// <summary>Indirizzo senza "http://" da mostrare nel controller.</summary>
    public string WebAddressText { get; set => Set(ref field, value); } = "";

    public async Task StartWebServerAsync()
    {
        await _web.StopAsync();
        WebUrl = null;
        WebRunning = false;
        WebAddressText = "";
        if (!Settings.WebServerEnabled)
        {
            WebStatus = "Server web disattivato";
            return;
        }
        try
        {
            await _web.StartAsync(Settings.WebServerPort, alternatives: 10);
            WebRunning = true;
            UpdateWebUrl();
            if (_web.Port != Settings.WebServerPort)
                ShowStatus($"La porta {Settings.WebServerPort} è usata da un altro programma: il timer in rete usa la {_web.Port}. " +
                           "Collegamenti e QR salvati con la porta vecchia non funzionano finché resta così.", error: true);
        }
        catch (Exception ex)
        {
            WebStatus = $"Server web non avviato (porta {Settings.WebServerPort}): {ex.Message}";
        }
    }

    /// <summary>Ricalcola l'indirizzo (scheda di rete preferita, nome del PC o IP scelto).</summary>
    public void UpdateWebUrl()
    {
        if (!WebRunning) return;
        var host = NetworkInfo.ResolveHost(Settings.WebAddressMode);
        WebUrl = $"http://{host}:{_web.Port}";
        WebAddressText = $"{host}:{_web.Port}";
        WebStatus = WebUrl;
        OnPropertyChanged(nameof(ControlUrl));
    }

    public Task StopWebServerAsync() => _web.StopAsync();

    // ───────────── Impostazioni ─────────────

    public void ApplySettings(AppSettings settings)
    {
        bool webChanged = settings.WebServerEnabled != Settings.WebServerEnabled || settings.WebServerPort != Settings.WebServerPort;
        Settings = settings;
        OnPropertyChanged(nameof(MessagePresets));
        OnPropertyChanged(nameof(MessagesEnabled));
        if (!settings.MessagesEnabled) Messages.Clear();
        OnPropertyChanged(nameof(ControlUrl));
        ApplyTimerSettings();
        UpdateMeetingStart();
        UpdateAdaptiveParts();
        UpdatePlannedStarts();
        UpdateMeetingLine();
        ApplyVoiceSettings();
        UiTheme.Apply(settings);
        SaveSettings();
        if (!Timer.IsRunning && settings.IsOverseerWeek(Week.WeekStart) != Week.CircuitOverseerVisit)
        {
            _overseerListEdited = true;
            if (ChangeWeekOverseer(settings.IsOverseerWeek(Week.WeekStart))) ApplyMeeting();
            _overseerListEdited = false;
        }
        if (webChanged) _ = StartWebServerAsync();
        else UpdateWebUrl();
        RefreshDisplay();
    }

    void ApplyTimerSettings()
    {
        Timer.WarningSeconds = Settings.WarningSeconds;
        Timer.CounselSeconds = Settings.CounselSeconds;
        Timer.CountdownLeadSeconds = Settings.CountdownMinutes * 60;
    }

    /// <summary>Alla chiusura: rilascia l'ingresso audio.</summary>
    public void Shutdown()
    {
        DisposeVoice();
        KeepAwake.Set(false);
    }

    /// <summary>Con un'adunanza in corso o imminente il PC e gli schermi restano accesi.</summary>
    bool ShouldStayAwake()
    {
        if (Timer.IsRunning || IsVoiceArmed) return true;
        if (Timer.MeetingStart is not { } start) return false;
        var now = DateTimeOffset.Now;
        var lead = TimeSpan.FromMinutes(Math.Max(30, Settings.CountdownMinutes + 15));
        var tail = TimeSpan.FromMinutes(Settings.MeetingLengthMinutes + 30);
        return now >= start - lead && now <= start + tail;
    }

    public void SaveSettings()
    {
        try { _store.SaveSettings(Settings); } catch { /* non bloccare l'interfaccia */ }
    }

    public string DataFolder => _store.Root;

    public void ShowInfo(string message) => ShowStatus(message);

    void ShowStatus(string message, bool error = false)
    {
        StatusMessage = message;
        StatusIsError = error;
    }
}
