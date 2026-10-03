namespace TimerSala.Core.Audio;

public enum VoiceTriggerState
{
    /// <summary>Non in attesa.</summary>
    Off,
    /// <summary>In attesa di una pausa (sta ancora parlando il presidente o suona il cantico).</summary>
    WaitingForPause,
    /// <summary>Pausa trovata: la prossima voce avvia la parte.</summary>
    Listening,
}

/// <summary>
/// Decide quando avviare la parte: dopo una pausa di almeno <see cref="Pause"/>, alla prima voce
/// che dura almeno <see cref="MinVoice"/>. Restituisce l'istante in cui la voce è iniziata,
/// così il tempo necessario a riconoscerla non va perso.
/// </summary>
public sealed class VoiceTrigger
{
    /// <summary>Sotto questa durata un suono non interrompe la pausa (colpi, tosse, regolazioni).</summary>
    static readonly TimeSpan ShortSound = TimeSpan.FromMilliseconds(150);

    /// <summary>Brevi silenzi tollerati dentro una frase.</summary>
    static readonly TimeSpan MaxGap = TimeSpan.FromMilliseconds(250);

    readonly Lock _lock = new();
    TimeSpan _silence, _sound, _voice, _gap;
    DateTimeOffset? _onset;

    public TimeSpan Pause { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan MinVoice { get; set; } = TimeSpan.FromMilliseconds(400);

    public VoiceTriggerState State { get { lock (_lock) return field; } private set; }

    /// <summary>Durata del silenzio in corso.</summary>
    public TimeSpan Silence { get { lock (_lock) return _silence; } }

    /// <summary>Mette in attesa. Conta anche il silenzio già trascorso prima di premere.</summary>
    public void Arm()
    {
        lock (_lock)
        {
            ResetCandidate();
            State = _silence >= Pause ? VoiceTriggerState.Listening : VoiceTriggerState.WaitingForPause;
        }
    }

    public void Disarm()
    {
        lock (_lock)
        {
            ResetCandidate();
            State = VoiceTriggerState.Off;
        }
    }

    /// <summary>Elabora un blocco; restituisce l'inizio della voce quando la parte deve partire.</summary>
    public DateTimeOffset? Feed(bool isVoice, DateTimeOffset start, TimeSpan length)
    {
        lock (_lock)
        {
            if (isVoice)
            {
                _sound += length;
                if (_sound >= ShortSound) _silence = TimeSpan.Zero;
                else _silence += length;
            }
            else
            {
                _sound = TimeSpan.Zero;
                _silence += length;
            }

            switch (State)
            {
                case VoiceTriggerState.WaitingForPause:
                    if (_silence >= Pause) State = VoiceTriggerState.Listening;
                    return null;

                case VoiceTriggerState.Listening:
                    if (isVoice)
                    {
                        _onset ??= start;
                        _voice += length;
                        _gap = TimeSpan.Zero;
                        if (_voice >= MinVoice)
                        {
                            var at = _onset;
                            ResetCandidate();
                            State = VoiceTriggerState.Off;
                            return at;
                        }
                    }
                    else if (_onset is not null)
                    {
                        _gap += length;
                        if (_gap > MaxGap) ResetCandidate();
                    }
                    return null;

                default:
                    return null;
            }
        }
    }

    void ResetCandidate()
    {
        _onset = null;
        _voice = _gap = TimeSpan.Zero;
    }
}
