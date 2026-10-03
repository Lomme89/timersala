namespace TimerSala.Core.Timing;

/// <summary>Messaggio breve per l'oratore mostrato sullo schermo del timer. Thread-safe.</summary>
public sealed class MessageBoard(TimeProvider? clock = null)
{
    readonly object _lock = new();
    readonly TimeProvider _clock = clock ?? TimeProvider.System;
    string? _text;
    DateTimeOffset? _expires;
    DateTimeOffset? _fullUntil;

    public event EventHandler? Changed;

    /// <summary>
    /// Mostra un messaggio; <paramref name="duration"/> nullo o zero = finché non viene tolto.
    /// Con <paramref name="fullScreen"/> occupa tutto lo schermo per quel tempo, poi resta nella fascia.
    /// </summary>
    public void Show(string text, TimeSpan? duration, TimeSpan? fullScreen = null)
    {
        text = text.Trim();
        if (text.Length == 0) { Clear(); return; }
        if (text.Length > 120) text = text[..120];
        lock (_lock)
        {
            var now = _clock.GetUtcNow();
            _text = text;
            _fullUntil = fullScreen is { TotalSeconds: > 0 } f ? now + f : null;
            _expires = duration is { TotalSeconds: > 0 } d ? now + d : null;
            // un messaggio a tutto schermo resta almeno qualche secondo in fascia dopo il tutto schermo
            if (_expires is { } e && _fullUntil is { } fu && e < fu + TimeSpan.FromSeconds(5)) _expires = fu + TimeSpan.FromSeconds(5);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (_text is null) return;
            _text = null;
            _expires = null;
            _fullUntil = null;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Il messaggio corrente occupa tutto lo schermo.</summary>
    public bool IsFullScreen
    {
        get
        {
            var text = Current;
            lock (_lock) return text is not null && _fullUntil is { } f && _clock.GetUtcNow() < f;
        }
    }

    /// <summary>Testo visibile in questo momento (null se nessuno o scaduto).</summary>
    public string? Current
    {
        get
        {
            lock (_lock)
            {
                if (_text is not null && _expires is { } e && _clock.GetUtcNow() >= e)
                {
                    _text = null;
                    _expires = null;
                }
                return _text;
            }
        }
    }

    /// <summary>Secondi rimanenti prima che il messaggio sparisca (null se fisso o assente).</summary>
    public double? SecondsLeft
    {
        get
        {
            lock (_lock)
                return _text is not null && _expires is { } e ? Math.Max(0, (e - _clock.GetUtcNow()).TotalSeconds) : null;
        }
    }
}
