using System.Windows.Input;
using TimerSala.App.Interop;
using TimerSala.Core.Models;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>Quello che fa ora il pulsante Zoom: cambia da solo con l'adunanza.</summary>
public enum ZoomStep { StopMusic, ShareScreen, StopShare, EndMeeting }

/// <summary>
/// Zoom e musica d'attesa. Prima dell'inizio parte la musica di JW Library, poi Zoom muta tutti (senza riattivazione)
/// e manda a casa solo l'audio. Un solo pulsante fa il passo successivo: ferma musica → condividi schermo → interrompi →
/// dopo l'ultimo cantico, fine adunanza. Il menu accanto ha tutte le azioni, per quando l'adunanza va diversamente.
/// </summary>
public sealed partial class MainViewModel
{
    public bool ZoomEnabled { get; private set => Set(ref field, value); }
    public bool MusicOn { get; private set { if (Set(ref field, value)) UpdateZoomStep(); } }
    public bool ZoomBusy { get; private set { if (Set(ref field, value)) { OnPropertyChanged(nameof(ZoomIdle)); UpdateZoomStep(); } } }
    public bool ZoomIdle => !ZoomBusy;

    /// <summary>Lo schermo è condiviso in Zoom: da TimerSala o da Zoom stesso, si rilegge ogni secondo.</summary>
    public bool ScreenShared
    {
        get;
        private set
        {
            if (!Set(ref field, value)) return;
            // finita la condivisione dell'ultimo cantico: il prossimo passo è il muto di fine adunanza
            if (!value && InClosing()) _endReady = true;
            UpdateZoomStep();
        }
    }

    public ZoomStep ZoomStep { get; private set => Set(ref field, value); } = ZoomStep.ShareScreen;
    public string ZoomIcon { get; private set => Set(ref field, value); } = "";
    public string ZoomText { get; private set => Set(ref field, value); } = "Condividi schermo";
    public string ZoomTip { get; private set => Set(ref field, value); } = "";

    // per adunanza: musica e condivisione partono una volta; dopo l'ultimo cantico si offre la fine
    DateTimeOffset? _zoomMeeting, _musicFor, _shareMusicFor;
    bool _endReady, _endDone, _zoomBeforeStart, _zoomWasRunning;
    long _lastSharePoll;

    void UpdateZoomStep()
    {
        ZoomStep = MusicOn ? ZoomStep.StopMusic
            : ScreenShared ? ZoomStep.StopShare
            : _endReady && !_endDone ? ZoomStep.EndMeeting
            : ZoomStep.ShareScreen;
        (ZoomIcon, ZoomText, ZoomTip) = ZoomStep switch
        {
            ZoomStep.StopMusic => ("", "Ferma musica", "Ferma la musica e l'audio condiviso quando l'oratore invita ad accomodarsi. All'inizio si ferma da sola"),
            ZoomStep.StopShare => ("", "Interrompi condivisione", "Interrompe la condivisione dello schermo in Zoom"),
            ZoomStep.EndMeeting => ("", "Fine adunanza", "Dopo cantico e preghiera mette tutti muti, liberi di riattivarsi per salutarsi"),
            _ => ("", "Condividi schermo", "Condivide in Zoom lo schermo scelto nelle impostazioni, con il suono"),
        };
        if (ZoomBusy) (ZoomIcon, ZoomText) = ("", "Zoom…");
    }

    /// <summary>Il pulsante Zoom (anche dal telecomando): il passo successivo.</summary>
    public ICommand ZoomStepCommand => field ??= new RelayCommand(() =>
        (ZoomStep switch { ZoomStep.StopMusic => StopMusicCommand, ZoomStep.StopShare => StopShareCommand, ZoomStep.EndMeeting => EndMeetingZoomCommand, _ => ShareScreenCommand })
            .Execute(null));

    public ICommand StopMusicCommand => field ??= new RelayCommand(() => StopMusic());

    public ICommand ShareScreenCommand => field ??= new RelayCommand(() =>
    {
        var screen = Settings.ZoomScreen;
        if (screen.Length == 0) { ShowStatus("Scegli lo schermo da condividere in Impostazioni › Zoom e musica.", error: true); return; }
        bool playing = MusicOn;
        RunZoom("Condivisione schermo", () =>
        {
            if (playing) { ZoomClient.StopShare(audioOnly: true); JwMusic.Pause(); } // la musica non deve finire nello schermo condiviso
            ZoomClient.ShareScreen(screen);
        }, () => { MusicOn = false; ScreenShared = true; });
    });

    public ICommand StopShareCommand => field ??= new RelayCommand(() =>
        RunZoom("Interrompi condivisione", () => ZoomClient.StopShare(), () => ScreenShared = false));

    public ICommand EndMeetingZoomCommand => field ??= new RelayCommand(() =>
        RunZoom("Fine adunanza", () => ZoomClient.MuteAll(allowUnmute: true), () =>
        {
            _endDone = true;
            UpdateZoomStep();
            ShowStatus("Tutti muti, possono riattivarsi per salutarsi.");
        }));

    public ICommand WaitingMusicNowCommand => field ??= new RelayCommand(() => StartWaitingMusic());

    void StopMusic(bool auto = false)
    {
        bool playing = MusicOn;
        RunZoom(auto ? "Fine della musica d'attesa" : "Ferma musica", () =>
        {
            ZoomClient.StopShare(audioOnly: true);
            if (playing) JwMusic.Pause();
        }, () => MusicOn = false);
    }

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
        if (_zoomMeeting != start)
        {
            _zoomMeeting = start;
            _endReady = _endDone = false;
            UpdateZoomStep();
        }

        // la musica si ferma da sola allo scadere del countdown o alla prima parte avviata
        var left = start - s.Now;
        bool before = left > TimeSpan.Zero;
        bool started = (_zoomBeforeStart && !before) || (s.IsRunning && !_zoomWasRunning);
        _zoomBeforeStart = before;
        _zoomWasRunning = s.IsRunning;
        if (MusicOn && started) { StopMusic(auto: true); return; }

        if (s.IsRunning) return;
        bool music = WaitingMusic.Due(left, Settings.MusicMinutes, _musicFor == start);
        bool share = WaitingMusic.Due(left, Settings.ShareMusicMinutes, _shareMusicFor == start);
        if (!music && !share) return;
        if (music) _musicFor = start;
        if (share) _shareMusicFor = start;
        StartWaitingMusic(music, share);
    }

    /// <summary>Quello che fa il countdown, subito: per provarlo, o se il PC è stato acceso tardi.</summary>
    public void StartWaitingMusic(bool music = true, bool share = true) =>
        RunZoom("Musica d'attesa", () =>
        {
            if (share) ZoomClient.MuteAll(allowUnmute: false);
            if (music) JwMusic.Start();
            if (share) ZoomClient.ShareAudio();
        }, () => { if (music) MusicOn = true; });

    // l'ultimo cantico è nella parte conclusiva: da lì in poi una condivisione che finisce porta alla fine adunanza
    bool InClosing()
    {
        var parts = Timer.Meeting.Parts;
        if (parts.Count == 0) return false;
        int closing = parts.FindIndex(p => p.Section == PartSection.Closing);
        return Timer.SelectedIndex >= (closing >= 0 ? closing : parts.Count - 1);
    }

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
