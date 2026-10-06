using System.Windows.Input;
using TimerSala.Core.Models;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>
/// Modalità addestramento: l'adunanza della settimana con le parti dieci volte più brevi, per spiegare il programma
/// a un nuovo fratello dell'acustica. Schermo e pagina web mostrano «PROVA»; nulla viene salvato e all'uscita
/// si torna esattamente com'era.
/// </summary>
public sealed partial class MainViewModel
{
    public bool IsTraining { get; private set => Set(ref field, value); }

    public ICommand StopTrainingCommand => field ??= new RelayCommand(StopTraining);

    TimerState? _beforeTraining;

    /// <summary>Entra in addestramento. Restituisce il motivo se non si può adesso.</summary>
    public string? StartTraining()
    {
        if (IsTraining) return null;
        if (Timer.IsRunning || IsVoiceArmed) return "Ferma il timer prima di iniziare l'addestramento.";
        _beforeTraining = Timer.ExportState();
        ForgetUndo();
        Messages.Clear();
        IsTraining = true;
        Timer.LoadMeeting(TrainingMeeting.From(CurrentMeeting));
        // un breve countdown d'inizio, per vedere anche quello
        Timer.MeetingStart = DateTimeOffset.Now.AddSeconds(40);
        UpdateAdaptiveParts();
        RebuildParts();
        RefreshDisplay();
        ShowStatus("Addestramento: le parti durano un decimo del solito e niente viene salvato.");
        return null;
    }

    public void StopTraining()
    {
        if (!IsTraining) return;
        if (IsVoiceArmed) Disarm();
        ForgetUndo();
        Messages.Clear();
        Timer.LoadMeeting(CurrentMeeting);
        if (_beforeTraining is { } state) Timer.ImportState(state, countDowntime: false);
        _beforeTraining = null;
        IsTraining = false;
        UpdateMeetingStart();
        UpdateAdaptiveParts();
        RebuildParts();
        UpdateWeekTexts();
        RefreshDisplay();
        ShowStatus("Addestramento finito: tutto è tornato com'era.");
    }

    /// <summary>Le azioni che toccano schemi e settimane non si fanno durante l'addestramento.</summary>
    bool BlockedByTraining()
    {
        if (!IsTraining) return false;
        ShowStatus("Esci prima dall'addestramento.", error: true);
        return true;
    }
}
