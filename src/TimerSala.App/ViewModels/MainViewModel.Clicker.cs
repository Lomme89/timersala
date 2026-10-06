using System.Windows.Input;
using TimerSala.App.Interop;
using TimerSala.Core.Input;

namespace TimerSala.App.ViewModels;

/// <summary>
/// Telecomando per presentazioni. Senza associazione: Pagina giù / Pagina su con TimerSala in primo piano.
/// Associato: tutti i suoi tasti «avanti» e «indietro», anche con JW Library o Zoom in primo piano.
/// </summary>
public sealed partial class MainViewModel
{
    RawKeyboard? _raw;
    bool _associating;
    DateTime _associateUntil;

    // l'ultimo tasto già gestito dal telecomando associato: se TimerSala è in primo piano arriva anche come tasto normale
    int _consumedKey;
    long _consumedAt;

    public string ClickerStatus { get; private set => Set(ref field, value); } = "";
    public bool ClickerAssociated => Settings.ClickerDevice is not null;

    void ApplyClickerSettings()
    {
        bool needRaw = Settings.ClickerEnabled && (Settings.ClickerDevice is not null || _associating);
        if (needRaw && _raw is null)
        {
            try
            {
                _raw = new RawKeyboard();
                _raw.KeyDown += OnRawKey;
            }
            catch { _raw = null; }
        }
        else if (!needRaw && _raw is not null)
        {
            _raw.Dispose();
            _raw = null;
        }
        UpdateClickerStatus();
    }

    void UpdateClickerStatus()
    {
        OnPropertyChanged(nameof(ClickerAssociated));
        ClickerStatus = _associating ? "Premi un tasto del telecomando…"
            : !Settings.ClickerEnabled ? "Spento"
            : Settings.ClickerDevice is { } d ? $"Associato ({ClickerKeys.ShortName(d)}): funziona anche con altri programmi in primo piano."
            : "Non associato: Pagina giù e Pagina su funzionano con TimerSala in primo piano.";
    }

    /// <summary>Il prossimo tasto premuto su un qualsiasi dispositivo diventa il telecomando (entro 20 secondi).</summary>
    public void StartClickerAssociation()
    {
        _associating = true;
        _associateUntil = DateTime.UtcNow.AddSeconds(20);
        ApplyClickerSettings();
    }

    public void ForgetClicker()
    {
        _associating = false;
        Settings.ClickerDevice = null;
        SaveSettings();
        ApplyClickerSettings();
    }

    void OnRawKey(string device, int vkey)
    {
        if (_associating)
        {
            if (DateTime.UtcNow > _associateUntil)
            {
                _associating = false;
                ApplyClickerSettings();
                return;
            }
            // si associa solo premendo un tasto da telecomando (avanti o indietro)
            if (ClickerKeys.FromAssociatedDevice(vkey) == ClickerAction.None) return;
            _associating = false;
            Settings.ClickerDevice = device;
            SaveSettings();
            ApplyClickerSettings();
            ShowStatus("Telecomando associato: avanti = Avvia/Ferma, indietro = Annulla.");
            return;
        }
        if (!Settings.ClickerEnabled || device != Settings.ClickerDevice) return;
        if (ClickerKeys.IsTypingKey(vkey))
        {
            ForgetClicker();
            ShowStatus("Il dispositivo associato come telecomando è una tastiera: associazione annullata. Riassocia premendo un tasto del telecomando.", error: true);
            return;
        }
        var action = ClickerKeys.FromAssociatedDevice(vkey);
        if (action == ClickerAction.None) return;
        _consumedKey = vkey;
        _consumedAt = Environment.TickCount64;
        RunClicker(action);
    }

    /// <summary>
    /// Dalle scorciatoie del controller e della mini: true se il tasto è del telecomando (e quindi già gestito).
    /// </summary>
    public bool HandleClickerKey(Key key)
    {
        int vkey = KeyInterop.VirtualKeyFromKey(key);
        if (vkey == _consumedKey && Environment.TickCount64 - _consumedAt < 400) return true;
        if (!Settings.ClickerEnabled) return false;
        var action = ClickerKeys.FromKeyboard(vkey);
        if (action == ClickerAction.None) return false;
        RunClicker(action);
        return true;
    }

    void RunClicker(ClickerAction action)
    {
        if (action == ClickerAction.Forward) ToggleStartCommand.Execute(null);
        else if (CanUndo) UndoCommand.Execute(null);
    }

    void DisposeClicker()
    {
        _raw?.Dispose();
        _raw = null;
    }
}
