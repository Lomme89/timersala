using System.Net.Http;
using System.Windows.Input;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>Sicurezza in sala: «Annulla» dopo Ferma, chiusura protetta, orologio del PC.</summary>
public sealed partial class MainViewModel
{
    // ───────────── Annulla: dopo Ferma e dopo le modifiche dal menu della parte ─────────────

    /// <summary>Un'azione da poter annullare per qualche secondo.</summary>
    sealed record UndoOffer(string Text, Action Undo, DateTime Until, TimeSpan Window);

    static readonly TimeSpan EditUndoWindow = TimeSpan.FromSeconds(10);

    UndoOffer? _offer;
    bool _stoppedFromPhone;

    /// <summary>C'è qualcosa da annullare: compare la barra con «Annulla».</summary>
    public bool CanUndoAction { get; private set => Set(ref field, value); }

    public string UndoActionText { get; private set => Set(ref field, value); } = "";

    /// <summary>Tempo rimasto per annullare, tra 1 e 0 (barra che si svuota).</summary>
    public double UndoActionFraction { get; private set => Set(ref field, value); }

    public ICommand UndoActionCommand => field ??= new RelayCommand(UndoAction);

    /// <summary>Esc: annulla l'ultima azione, altrimenti l'attesa o l'avvio fatto dalla voce.</summary>
    public ICommand UndoCommand => field ??= new RelayCommand(Undo);

    public bool CanUndo => CanUndoAction || CanCancelVoice;

    void Undo()
    {
        if (CanUndoAction) UndoAction();
        else if (CanCancelVoice) CancelVoiceCommand.Execute(null);
    }

    void UndoAction()
    {
        if (Timer.CanUndoStop)
        {
            // la parte successiva potrebbe essere già in attesa della voce
            if (IsVoiceArmed) Disarm();
            if (Timer.UndoStop()) ShowStatus("Fermata annullata: il tempo continua da dove era.");
        }
        else if (_offer is { } offer && DateTime.UtcNow < offer.Until)
        {
            _offer = null;
            offer.Undo();
        }
        RefreshDisplay();
    }

    /// <summary>Offre «Annulla» per qualche secondo dopo una modifica.</summary>
    void OfferUndo(string text, Action undo) =>
        _offer = new UndoOffer(text, undo, DateTime.UtcNow + EditUndoWindow, EditUndoWindow);

    void ForgetUndo()
    {
        _offer = null;
        Timer.ForgetStop();
    }

    void RefreshUndoStop()
    {
        if (Timer.UndoStopRemaining is { } l)
        {
            CanUndoAction = true;
            UndoActionFraction = Math.Clamp(l / MeetingTimer.UndoStopWindow, 0, 1);
            UndoActionText = $"{(_stoppedFromPhone ? "Fermato dal telefono" : "Parte fermata")} · {Math.Ceiling(l.TotalSeconds):0} s";
            return;
        }
        if (_offer is { } o && DateTime.UtcNow < o.Until)
        {
            var left = o.Until - DateTime.UtcNow;
            CanUndoAction = true;
            UndoActionFraction = Math.Clamp(left / o.Window, 0, 1);
            UndoActionText = $"{o.Text} · {Math.Ceiling(left.TotalSeconds):0} s";
            return;
        }
        _offer = null;
        CanUndoAction = false;
    }

    // ───────────── Chiusura protetta ─────────────

    /// <summary>Perché non conviene chiudere adesso, o null se si può chiudere senza chiedere.</summary>
    public string? CloseWarning()
    {
        if (Timer.IsRunning) return "Il timer è in funzione.";
        if (IsVoiceArmed) return "Una parte è in attesa della voce per partire.";
        if (IsCountdown && !Timer.IsPreviewingCountdown) return "Sullo schermo della sala c'è il countdown d'inizio.";
        return null;
    }

    // ───────────── Orologio del PC ─────────────

    /// <summary>Avviso se l'ora del PC si discosta da quella di internet (null se è giusta o non verificabile).</summary>
    public string? ClockWarning
    {
        get;
        private set { if (Set(ref field, value)) OnPropertyChanged(nameof(HasClockWarning)); }
    }

    public bool HasClockWarning => ClockWarning is not null;

    async Task CheckClockAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        await Task.Delay(TimeSpan.FromSeconds(5));
        while (true)
        {
            try
            {
                var offset = await ClockCheck.MeasureAsync(http);
                // senza connessione resta l'ultimo esito
                if (offset is not null) ClockWarning = ClockCheck.Warning(offset);
            }
            catch { /* mai bloccare il programma per questo controllo */ }
            // dopo una correzione l'avviso sparisce al controllo successivo
            await Task.Delay(ClockWarning is null ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(2));
        }
    }
}
