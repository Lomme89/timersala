using System.Net.Http;
using System.Windows.Input;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>Sicurezza in sala: «Annulla» dopo Ferma, chiusura protetta, orologio del PC.</summary>
public sealed partial class MainViewModel
{
    // ───────────── Annulla dopo Ferma ─────────────

    bool _stoppedFromPhone;

    /// <summary>Nei secondi dopo Ferma c'è «Annulla» per rimediare a una pressione sbagliata.</summary>
    public bool CanUndoStop { get; private set => Set(ref field, value); }

    public string UndoStopText { get; private set => Set(ref field, value); } = "";

    /// <summary>Tempo rimasto per annullare, tra 1 e 0 (barra che si svuota).</summary>
    public double UndoStopFraction { get; private set => Set(ref field, value); }

    public ICommand UndoStopCommand => field ??= new RelayCommand(UndoStop);

    /// <summary>Esc: annulla l'ultima fermata, altrimenti l'attesa o l'avvio fatto dalla voce.</summary>
    public ICommand UndoCommand => field ??= new RelayCommand(Undo);

    public bool CanUndo => CanUndoStop || CanCancelVoice;

    void Undo()
    {
        if (Timer.CanUndoStop) UndoStop();
        else if (CanCancelVoice) CancelVoiceCommand.Execute(null);
    }

    void UndoStop()
    {
        if (!Timer.CanUndoStop) return;
        // la parte successiva potrebbe essere già in attesa della voce
        if (IsVoiceArmed) Disarm();
        if (Timer.UndoStop()) ShowStatus("Fermata annullata: il tempo continua da dove era.");
        RefreshDisplay();
    }

    void RefreshUndoStop()
    {
        var left = Timer.UndoStopRemaining;
        CanUndoStop = left is not null;
        if (left is not { } l) return;
        UndoStopFraction = Math.Clamp(l / MeetingTimer.UndoStopWindow, 0, 1);
        UndoStopText = $"{(_stoppedFromPhone ? "Fermato dal telefono" : "Parte fermata")} · {Math.Ceiling(l.TotalSeconds):0} s";
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
