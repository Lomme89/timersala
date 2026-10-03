using System.Windows;
using System.Windows.Input;
using TimerSala.App.Audio;
using TimerSala.Core.Audio;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>Avvio con la voce (sperimentale): Avvia mette la parte in attesa, la voce dell'oratore la fa partire.</summary>
public sealed partial class MainViewModel
{
    static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(12);

    readonly VoiceTrigger _voiceTrigger = new();
    AudioInput? _audio;
    double _peakDb = -90;
    bool _heardVoice;
    DateTime? _voiceStartedAt;

    public bool VoiceEnabled { get; private set => Set(ref field, value); }

    /// <summary>La parte è in attesa della voce.</summary>
    public bool IsVoiceArmed { get; private set => Set(ref field, value); }

    /// <summary>Si può annullare: l'attesa, o un avvio fatto dalla voce da pochi secondi.</summary>
    public bool CanCancelVoice { get; private set => Set(ref field, value); }

    public string VoiceStatus { get; private set => Set(ref field, value); } = "";
    public bool VoiceError { get; private set => Set(ref field, value); }

    /// <summary>Livello dell'ingresso tra 0 e 1 (da −60 a 0 dBFS).</summary>
    public double VoiceLevel { get; private set => Set(ref field, value); }

    /// <summary>Posizione della soglia sulla stessa scala del livello.</summary>
    public double VoiceThresholdLevel => LevelOf(Settings.VoiceThresholdDb);

    public bool VoiceHeard { get; private set => Set(ref field, value); }

    public ICommand CancelVoiceCommand => field ??= new RelayCommand(CancelVoice);

    public static double LevelOf(double db) => Math.Clamp((db + 60) / 60, 0, 1);

    void ApplyVoiceSettings()
    {
        var s = Settings;
        _voiceTrigger.Pause = TimeSpan.FromSeconds(s.VoicePauseSeconds);
        _voiceTrigger.MinVoice = TimeSpan.FromSeconds(s.VoiceMinSeconds);
        OnPropertyChanged(nameof(VoiceThresholdLevel));

        if (!s.VoiceStartEnabled)
        {
            Disarm();
            _audio?.Dispose();
            _audio = null;
            VoiceEnabled = false;
            return;
        }

        VoiceEnabled = true;
        if (_audio is null)
        {
            _audio = new AudioInput { Trigger = _voiceTrigger };
            _audio.Frame += f =>
            {
                if (f.LevelDb > _peakDb) _peakDb = f.LevelDb;
                if (f.IsVoice) _heardVoice = true;
            };
            _audio.VoiceStarted += at => Application.Current.Dispatcher.BeginInvoke(() => OnVoiceStarted(at));
            _audio.Failed += msg => Application.Current.Dispatcher.BeginInvoke(() => OnAudioFailed(msg));
        }
        _audio.ThresholdDb = s.VoiceThresholdDb;
        if (!_audio.IsRunning || _audio.DeviceId != s.VoiceInputDevice) StartAudio();
        UpdateVoiceStatus();
    }

    void StartAudio()
    {
        try
        {
            _audio!.Start(Settings.VoiceInputDevice);
            VoiceError = false;
        }
        catch (Exception ex)
        {
            _audio!.Stop();
            Disarm();
            VoiceError = true;
            VoiceStatus = "Ingresso audio non disponibile: " + ex.Message;
        }
    }

    void OnAudioFailed(string message)
    {
        _audio?.Stop();
        Disarm();
        VoiceError = true;
        VoiceStatus = "Ingresso audio interrotto: " + message;
    }

    /// <summary>Avvia: con la voce attiva mette in attesa; premuto di nuovo avvia subito.</summary>
    bool TryArmOrStartNow()
    {
        if (IsVoiceArmed)
        {
            Disarm();
            Timer.Start();
            return true;
        }
        if (!CanArm()) return false;
        Arm();
        return true;
    }

    bool CanArm()
    {
        if (!VoiceEnabled || VoiceError || _audio is not { IsRunning: true } || Timer.IsRunning) return false;
        int i = Timer.SelectedIndex;
        return i >= 0 && i < Timer.Meeting.Parts.Count && Timer.Meeting.Parts[i].IsTimed;
    }

    void Arm()
    {
        _voiceStartedAt = null;
        _voiceTrigger.Arm();
        IsVoiceArmed = true;
        UpdateVoiceStatus();
    }

    void Disarm()
    {
        _voiceTrigger.Disarm();
        IsVoiceArmed = false;
        UpdateVoiceStatus();
    }

    void OnVoiceStarted(DateTimeOffset at)
    {
        if (!IsVoiceArmed) return;
        IsVoiceArmed = false;
        if (Timer.IsRunning) { UpdateVoiceStatus(); return; }
        Timer.Start(at);
        _voiceStartedAt = DateTime.UtcNow;
        UpdateVoiceStatus();
        RefreshDisplay();
    }

    /// <summary>Esc: toglie l'attesa, oppure annulla un avvio appena fatto dalla voce e torna in attesa.</summary>
    void CancelVoice()
    {
        if (IsVoiceArmed)
        {
            Disarm();
        }
        else if (VoiceUndoAvailable)
        {
            Timer.CancelStart();
            Arm();
        }
        RefreshDisplay();
    }

    bool VoiceUndoAvailable => _voiceStartedAt is { } t && DateTime.UtcNow - t < UndoWindow && Timer.IsRunning && Timer.Mode == TimerMode.Part;

    /// <summary>Dopo Ferma: la parte successiva va in attesa, se non c'è un cantico in mezzo.</summary>
    void AutoArmAfterStop(int stoppedIndex)
    {
        if (!Settings.VoiceAutoArmNext || !CanArm()) return;
        int next = Timer.SelectedIndex;
        if (next <= stoppedIndex) return;
        for (int i = stoppedIndex + 1; i < next; i++)
            if (Timer.Meeting.Parts[i].IsSong) return;
        Arm();
    }

    /// <summary>Chiamata dal tick dell'interfaccia (10 volte al secondo).</summary>
    void TickVoice()
    {
        if (!VoiceEnabled) return;
        VoiceLevel = LevelOf(_peakDb);
        VoiceHeard = _heardVoice;
        _peakDb = -90;
        _heardVoice = false;
        if (_voiceStartedAt is not null && !VoiceUndoAvailable) _voiceStartedAt = null;
        UpdateVoiceStatus();
    }

    void UpdateVoiceStatus()
    {
        CanCancelVoice = IsVoiceArmed || VoiceUndoAvailable;
        if (!VoiceEnabled || VoiceError) return;
        VoiceStatus = IsVoiceArmed
            ? _voiceTrigger.State == VoiceTriggerState.Listening
                ? "In ascolto: parte alla prima voce"
                : "In attesa di una pausa…"
            : VoiceUndoAvailable
                ? "Partita con la voce · Esc per annullare"
                : $"Avvio con la voce · {_audio?.DeviceName}";
    }

    void DisposeVoice()
    {
        _audio?.Dispose();
        _audio = null;
    }
}
