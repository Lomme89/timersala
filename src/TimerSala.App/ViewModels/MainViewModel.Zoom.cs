using System.Windows.Input;
using TimerSala.App.Interop;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>Quello che fa ora il pulsante Zoom: condivide lo schermo, o interrompe la condivisione in corso.</summary>
public enum ZoomStep { ShareScreen, StopShare }

/// <summary>
/// Zoom pilotato da fuori. Il pulsante in vista è sempre «Condividi schermo» (o «Interrompi» mentre si condivide);
/// le altre azioni (muto, audio del computer, fine adunanza) sono nel menu accanto.
/// Prima dell'inizio, se scelto, Zoom muta tutti e condivide l'audio del computer, così la musica d'attesa
/// fatta partire in sala arriva anche a casa; all'inizio quella condivisione si chiude da sola.
/// JW Library non si pilota più: i suoi collegamenti erano instabili e l'app sta per cambiare molto.
/// </summary>
public sealed partial class MainViewModel
{
    public bool ZoomEnabled { get; private set => Set(ref field, value); }

    /// <summary>L'audio del computer è condiviso in Zoom da TimerSala (prima dell'inizio).</summary>
    public bool AudioShared { get; private set => Set(ref field, value); }
    public bool ZoomBusy { get; private set { if (Set(ref field, value)) { OnPropertyChanged(nameof(ZoomIdle)); UpdateZoomStep(); } } }
    public bool ZoomIdle => !ZoomBusy;

    /// <summary>Lo schermo è condiviso in Zoom: da TimerSala o da Zoom stesso, si rilegge ogni secondo.</summary>
    public bool ScreenShared { get; private set { if (Set(ref field, value)) UpdateZoomStep(); } }

    public ZoomStep ZoomStep { get; private set => Set(ref field, value); } = ZoomStep.ShareScreen;
    public string ZoomIcon { get; private set => Set(ref field, value); } = "";
    public string ZoomText { get; private set => Set(ref field, value); } = "Condividi schermo";
    public string ZoomTip { get; private set => Set(ref field, value); } = "";

    // per adunanza l'audio condiviso parte una volta sola
    DateTimeOffset? _audioFor;
    bool _zoomBeforeStart, _zoomWasRunning;
    long _lastSharePoll;

    void UpdateZoomStep()
    {
        ZoomStep = ScreenShared ? ZoomStep.StopShare : ZoomStep.ShareScreen;
        (ZoomIcon, ZoomText, ZoomTip) = ZoomStep == ZoomStep.StopShare
            ? ("", "Interrompi condivisione", "Interrompe la condivisione dello schermo in Zoom")
            : ("", "Condividi schermo", "Condivide in Zoom lo schermo scelto nelle impostazioni, con il suono. Le altre azioni sono nella freccia accanto");
        if (ZoomBusy) (ZoomIcon, ZoomText) = ("", "Zoom…");
    }

    /// <summary>Il pulsante Zoom (anche dal telecomando): condivide, o interrompe se sta già condividendo.</summary>
    public ICommand ZoomStepCommand => field ??= new RelayCommand(() =>
        (ZoomStep == ZoomStep.StopShare ? StopShareCommand : ShareScreenCommand).Execute(null));

    public ICommand ShareScreenCommand => field ??= new RelayCommand(() =>
    {
        var screen = Settings.ZoomScreen;
        if (screen.Length == 0) { ShowStatus("Scegli lo schermo da condividere in Impostazioni › Zoom.", error: true); return; }
        // la condivisione dello schermo prende il posto di quella dell'audio
        RunZoom("Condivisione schermo", () => ZoomClient.ShareScreen(screen), () => { AudioShared = false; ScreenShared = true; });
    });

    public ICommand StopShareCommand => field ??= new RelayCommand(() =>
        RunZoom("Interrompi condivisione", () => ZoomClient.StopShare(), () => { ScreenShared = false; AudioShared = false; }));

    public ICommand ShareAudioCommand => field ??= new RelayCommand(() => StartWaitingAudio());

    public ICommand StopAudioCommand => field ??= new RelayCommand(() => StopAudio());

    public ICommand EndMeetingZoomCommand => field ??= new RelayCommand(() =>
        RunZoom("Fine adunanza", () => ZoomClient.MuteAll(allowUnmute: true),
            () => ShowStatus("Tutti muti, possono riattivarsi per salutarsi.")));

    void StopAudio(bool auto = false) =>
        RunZoom(auto ? "Fine dell'audio condiviso" : "Interrompi audio", () => ZoomClient.StopShare(audioOnly: true), () => AudioShared = false);

    void TickZoom(TimerSnapshot s)
    {
        ZoomEnabled = Settings.ZoomEnabled;
        if (!ZoomEnabled || ZoomBusy) return;

        // una condivisione si può interrompere anche da Zoom: il pulsante deve accorgersene
        if (Environment.TickCount64 - _lastSharePoll >= 1000)
        {
            _lastSharePoll = Environment.TickCount64;
            try { ScreenShared = ZoomClient.SharingScreen; } catch (InvalidOperationException) { } // Zoom che si chiude in quel momento
        }

        if (IsTemporaryMeeting || Timer.MeetingStart is not { } start) return;

        // l'audio condiviso si chiude da solo allo scadere del countdown o alla prima parte avviata
        var left = start - s.Now;
        bool before = left > TimeSpan.Zero;
        bool started = (_zoomBeforeStart && !before) || (s.IsRunning && !_zoomWasRunning);
        _zoomBeforeStart = before;
        _zoomWasRunning = s.IsRunning;
        if (AudioShared && started) { StopAudio(auto: true); return; }

        if (s.IsRunning || !WaitingMusic.Due(left, Settings.ShareMusicMinutes, _audioFor == start)) return;
        _audioFor = start;
        StartWaitingAudio();
    }

    /// <summary>Quello che succede prima dell'inizio, subito: per provarlo, o se il PC è stato acceso tardi.</summary>
    public void StartWaitingAudio() =>
        RunZoom("Muto e audio condiviso", () =>
        {
            ZoomClient.MuteAll(allowUnmute: false);
            ZoomClient.ShareAudio();
        }, () => AudioShared = true);

    async void RunZoom(string what, Action work, Action? done = null)
    {
        if (ZoomBusy) { ShowStatus("Zoom sta ancora eseguendo il comando precedente.", error: true); return; }
        ZoomBusy = true;
        try
        {
            await Task.Run(work);
            done?.Invoke();
        }
        // confine con un'app altrui: qualunque cosa vada storta si dice e si rifà a mano, il timer non deve cadere
        catch (Exception e) { ShowStatus($"{what}: {e.Message}. Fallo a mano in Zoom.", error: true); }
        finally { ZoomBusy = false; }
    }

    /// <summary>Per le impostazioni: le righe della scheda «Schermi» di Zoom.</summary>
    internal static Task<List<string>> ReadZoomScreensAsync() => Task.Run(ZoomClient.Screens);
}
