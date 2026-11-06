using System.Windows.Input;
using System.Windows.Threading;
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
    DispatcherTimer? _associateTimer;
    string? _associateHint; // l'ultimo tasto scartato durante l'associazione
    ClickerAction _learning; // durante l'associazione: il tasto premuto diventa questa azione (None = solo tasti predefiniti)

    // l'ultimo tasto già gestito dal telecomando associato: se TimerSala è in primo piano arriva anche come tasto normale
    int _consumedKey;
    long _consumedAt;

    public string ClickerStatus { get; private set => Set(ref field, value); } = "";
    public bool ClickerAssociated => Settings.ClickerDevice is not null;
    public bool ClickerHasCustomKeys => Settings.ClickerForwardKeys.Count + Settings.ClickerBackKeys.Count + Settings.ClickerZoomKeys.Count > 0;

    /// <summary>Riga «Tasto avanti» delle impostazioni: i tasti, o l'attesa mentre se ne sceglie uno.</summary>
    public string ClickerForwardText => ClickerKeysText(ClickerAction.Forward);
    public string ClickerBackText => ClickerKeysText(ClickerAction.Back);
    public string ClickerZoomText => ClickerKeysText(ClickerAction.Zoom) is { Length: > 0 } t ? t
        : ClickerZoomCaps.Count == 0 ? "Nessuno. Premi «Scegli» e poi il tasto sul telecomando" : "";

    /// <summary>I tasti di ogni azione, da mostrare come tasti veri (vuoti mentre se ne sceglie uno).</summary>
    public IReadOnlyList<string> ClickerForwardCaps => ClickerCaps(ClickerAction.Forward);
    public IReadOnlyList<string> ClickerBackCaps => ClickerCaps(ClickerAction.Back);
    public IReadOnlyList<string> ClickerZoomCaps => ClickerCaps(ClickerAction.Zoom);

    IReadOnlyList<string> ClickerCaps(ClickerAction action) =>
        _associating && _learning == action ? []
            : ClickerKeys.KeysFor(action, Settings.ClickerForwardKeys, Settings.ClickerBackKeys, Settings.ClickerZoomKeys).Select(ClickerKeys.Name).ToList();

    // solo mentre si sceglie un tasto: il resto del tempo i tasti si vedono come tasti
    string ClickerKeysText(ClickerAction action) =>
        _associating && _learning == action ? $"Premi il tasto sul telecomando… ({AssociateSeconds} s){_associateHint}" : "";

    int AssociateSeconds => Math.Max(0, (int)Math.Ceiling((_associateUntil - DateTime.UtcNow).TotalSeconds));

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
        OnPropertyChanged(nameof(ClickerHasCustomKeys));
        OnPropertyChanged(nameof(ClickerForwardText));
        OnPropertyChanged(nameof(ClickerBackText));
        OnPropertyChanged(nameof(ClickerZoomText));
        OnPropertyChanged(nameof(ClickerForwardCaps));
        OnPropertyChanged(nameof(ClickerBackCaps));
        OnPropertyChanged(nameof(ClickerZoomCaps));
        ClickerStatus = _associating && _learning == ClickerAction.None
                ? $"Premi «avanti» o «indietro» sul telecomando… ({AssociateSeconds} s){_associateHint}"
            : !Settings.ClickerEnabled ? "Spento."
            : Settings.ClickerDevice is { } d ? $"Associato: {ClickerKeys.ShortName(d)}."
            : "Non associato.";
    }

    /// <summary>
    /// Il prossimo tasto premuto su un qualsiasi dispositivo diventa il telecomando (entro 20 secondi).
    /// Con <paramref name="learn"/> quel tasto, qualunque sia, fa anche quell'azione.
    /// </summary>
    public void StartClickerAssociation(ClickerAction learn = ClickerAction.None)
    {
        _associating = true;
        _learning = learn;
        _associateUntil = DateTime.UtcNow.AddSeconds(20);
        _associateHint = null;
        // conto alla rovescia visibile; allo scadere si torna allo stato di prima
        if (_associateTimer is null)
        {
            _associateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _associateTimer.Tick += (_, _) =>
            {
                if (DateTime.UtcNow <= _associateUntil) { UpdateClickerStatus(); return; }
                StopAssociation();
                ShowStatus("Dal telecomando non è arrivato nessun tasto, associazione annullata.", error: true);
            };
        }
        _associateTimer.Start();
        ApplyClickerSettings();
    }

    void StopAssociation()
    {
        _associating = false;
        _associateTimer?.Stop();
        ApplyClickerSettings();
    }

    public void ForgetClicker()
    {
        _associating = false;
        _associateTimer?.Stop();
        Settings.ClickerDevice = null;
        SaveSettings();
        ApplyClickerSettings();
    }

    public void ResetClickerKeys()
    {
        Settings.ClickerForwardKeys = [];
        Settings.ClickerBackKeys = [];
        Settings.ClickerZoomKeys = [];
        SaveSettings();
        UpdateClickerStatus();
    }

    void OnRawKey(string device, int vkey)
    {
        if (_associating)
        {
            if (DateTime.UtcNow > _associateUntil) return; // ci pensa il timer
            if (_learning != ClickerAction.None)
            {
                // lettere e numeri no: il dispositivo verrebbe preso per una tastiera e l'associazione annullata
                if (ClickerKeys.IsTypingKey(vkey))
                {
                    _associateHint = $"\n«{ClickerKeys.Name(vkey)}» non va bene, lettere e numeri sono della tastiera.";
                    UpdateClickerStatus();
                    return;
                }
                // un tasto fa una cosa sola: si toglie dalle altre azioni
                foreach (var list in new[] { Settings.ClickerForwardKeys, Settings.ClickerBackKeys, Settings.ClickerZoomKeys }) list.Remove(vkey);
                var learned = _learning;
                (learned switch { ClickerAction.Forward => Settings.ClickerForwardKeys, ClickerAction.Back => Settings.ClickerBackKeys, _ => Settings.ClickerZoomKeys }).Add(vkey);
                Settings.ClickerDevice = device;
                SaveSettings();
                StopAssociation();
                ShowStatus($"Telecomando: «{ClickerKeys.Name(vkey)}» ora {learned switch { ClickerAction.Forward => "avvia e ferma", ClickerAction.Back => "annulla", _ => "fa il pulsante Zoom" }}.");
                return;
            }
            // si associa solo premendo un tasto da telecomando (avanti o indietro); gli altri si mostrano, così si vede che arrivano
            if (ClickerFromDevice(vkey) == ClickerAction.None)
            {
                _associateHint = $"\nRicevuto «{ClickerKeys.Name(vkey)}», che non è un tasto avanti o indietro. Puoi assegnarlo qui sotto con «Scegli».";
                UpdateClickerStatus();
                return;
            }
            Settings.ClickerDevice = device;
            SaveSettings();
            StopAssociation();
            ShowStatus("Telecomando associato: avanti = Avvia/Ferma, indietro = Annulla.");
            return;
        }
        if (!Settings.ClickerEnabled || device != Settings.ClickerDevice) return;
        if (ClickerKeys.IsTypingKey(vkey))
        {
            ForgetClicker();
            ShowStatus("Il dispositivo associato è una tastiera, quindi l'associazione è annullata. Riassocia premendo un tasto del telecomando.", error: true);
            return;
        }
        var action = ClickerFromDevice(vkey);
        if (action == ClickerAction.None) return;
        _consumedKey = vkey;
        _consumedAt = Environment.TickCount64;
        RunClicker(action);
    }

    ClickerAction ClickerFromDevice(int vkey) => ClickerKeys.FromAssociatedDevice(vkey, Settings.ClickerForwardKeys, Settings.ClickerBackKeys, Settings.ClickerZoomKeys);

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
        else if (action == ClickerAction.Zoom) { if (ZoomEnabled) ZoomStepCommand.Execute(null); }
        else if (CanUndo) UndoCommand.Execute(null);
    }

    void DisposeClicker()
    {
        _associateTimer?.Stop();
        _raw?.Dispose();
        _raw = null;
    }
}
