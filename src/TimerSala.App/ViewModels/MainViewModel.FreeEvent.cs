using System.Windows.Input;
using TimerSala.Core.Models;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>
/// Modalità libera: un evento fuori programma al posto dell'adunanza (matrimonio, funerale, adunanza per il servizio…).
/// Countdown, schermo e telefono funzionano come sempre; lo schema della settimana non viene toccato.
/// </summary>
public sealed partial class MainViewModel
{
    public bool IsFreeEvent { get; private set => Set(ref field, value); }
    public string FreeEventTitle { get; private set => Set(ref field, value); } = "";

    /// <summary>Addestramento o evento: l'adunanza in corso non è quella della settimana.</summary>
    public bool IsTemporaryMeeting => IsTraining || IsFreeEvent;

    public ICommand StopFreeEventCommand => field ??= new RelayCommand(StopFreeEvent);

    TimerState? _beforeEvent;

    /// <summary>Carica l'evento. Restituisce il motivo se non si può adesso.</summary>
    public string? StartFreeEvent(FreeEvent ev, TimeOnly start)
    {
        if (IsTraining) return "Esci prima dall'addestramento.";
        if (Timer.IsRunning || IsVoiceArmed) return "Ferma il timer prima di iniziare l'evento.";
        var meeting = ev.ToMeeting(Kind);
        if (meeting.Parts.Count == 0) return "Indica almeno una parte con la sua durata.";

        Settings.RecentEvents = FreeEvent.Remember(Settings.RecentEvents, ev);
        SaveSettings();
        if (!IsFreeEvent) _beforeEvent = Timer.ExportState();
        ForgetUndo();
        IsFreeEvent = true;
        FreeEventTitle = ev.Title;
        OnPropertyChanged(nameof(IsTemporaryMeeting));
        Timer.LoadMeeting(meeting);
        var today = DateTime.Today.Add(start.ToTimeSpan());
        Timer.MeetingStart = new DateTimeOffset(today);
        Timer.TitleOverride = null;
        Timer.SetAdaptiveParts([]);
        RebuildParts();
        UpdatePlannedStarts();
        RefreshDisplay();
        ShowStatus($"Evento «{ev.Title}»: lo schema della settimana non viene toccato.");
        return null;
    }

    public void StopFreeEvent()
    {
        if (!IsFreeEvent) return;
        if (Timer.IsRunning && !Confirm("Il timer è in funzione. Tornare comunque all'adunanza della settimana?")) return;
        if (IsVoiceArmed) Disarm();
        ForgetUndo();
        Timer.LoadMeeting(CurrentMeeting);
        if (_beforeEvent is { } state) Timer.ImportState(state, countDowntime: false);
        _beforeEvent = null;
        IsFreeEvent = false;
        OnPropertyChanged(nameof(IsTemporaryMeeting));
        UpdateMeetingStart();
        UpdateAdaptiveParts();
        RebuildParts();
        UpdatePlannedStarts();
        UpdateWeekTexts();
        RefreshDisplay();
        ShowStatus("Di nuovo l'adunanza della settimana.");
    }
}
