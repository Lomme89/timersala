using System.Collections.ObjectModel;
using System.Windows.Input;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>Una voce della lista di controllo.</summary>
public sealed class ChecklistItem(string text) : ObservableObject
{
    public string Text { get; } = text;
    public bool Done { get; set => Set(ref field, value); }
}

/// <summary>
/// Lista di controllo prima dell'adunanza: compare durante il countdown d'inizio (o con il pulsante),
/// si spunta con un clic e si azzera a ogni adunanza. Mai bloccante: all'avvio della prima parte avvisa soltanto.
/// </summary>
public sealed partial class MainViewModel
{
    public ObservableCollection<ChecklistItem> Checklist { get; } = [];

    /// <summary>Aperta a mano con il pulsante dell'intestazione.</summary>
    public bool ChecklistOpen { get; set { if (Set(ref field, value)) UpdateChecklistVisibility(); } }

    public bool ChecklistVisible { get; private set => Set(ref field, value); }
    public bool HasChecklist { get; private set => Set(ref field, value); }
    public string ChecklistSummary { get; private set => Set(ref field, value); } = "";
    public bool ChecklistComplete { get; private set => Set(ref field, value); }

    public ICommand ToggleChecklistCommand => field ??= new RelayCommand(() => ChecklistOpen = !ChecklistOpen);

    // l'adunanza a cui si riferiscono le spunte: cambiando adunanza si ricomincia
    DateTime? _checklistFor;
    bool _checklistCountdown, _checklistPartWasRunning;

    void ApplyChecklistSettings()
    {
        var done = Checklist.Where(c => c.Done).Select(c => c.Text).ToHashSet();
        foreach (var c in Checklist) c.PropertyChanged -= OnChecklistItemChanged;
        Checklist.Clear();
        if (Settings.ChecklistEnabled)
            foreach (var text in Settings.ChecklistItems.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                var item = new ChecklistItem(text.Trim()) { Done = done.Contains(text.Trim()) };
                item.PropertyChanged += OnChecklistItemChanged;
                Checklist.Add(item);
            }
        HasChecklist = Checklist.Count > 0;
        if (!HasChecklist) ChecklistOpen = false;
        UpdateChecklistSummary();
        UpdateChecklistVisibility();
    }

    void OnChecklistItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => UpdateChecklistSummary();

    void UpdateChecklistSummary()
    {
        int done = Checklist.Count(c => c.Done);
        ChecklistComplete = Checklist.Count > 0 && done == Checklist.Count;
        ChecklistSummary = ChecklistComplete ? "Tutto pronto" : $"{done} di {Checklist.Count}";
    }

    void UpdateChecklistVisibility() => ChecklistVisible = HasChecklist && (ChecklistOpen || _checklistCountdown);

    /// <summary>Chiamata a ogni aggiornamento del display.</summary>
    void TickChecklist(TimerSnapshot s)
    {
        if (!HasChecklist) return;

        // nuova adunanza: si ricomincia da capo
        var meeting = Timer.MeetingStart?.DateTime;
        if (meeting != _checklistFor && !IsTraining)
        {
            _checklistFor = meeting;
            foreach (var c in Checklist) c.Done = false;
        }

        bool countdown = s.Mode == TimerMode.Countdown;
        if (countdown != _checklistCountdown)
        {
            _checklistCountdown = countdown;
            UpdateChecklistVisibility();
        }

        // alla prima parte: un avviso discreto se manca qualcosa, e la lista si chiude
        bool partRunning = s.IsRunning && s.Mode == TimerMode.Part;
        if (partRunning && !_checklistPartWasRunning && IsFirstPartOfMeeting())
        {
            var missing = Checklist.Where(c => !c.Done).Select(c => c.Text).ToList();
            if (missing.Count > 0 && (_checklistCountdown || ChecklistOpen || Checklist.Any(c => c.Done)))
                ShowStatus($"Lista di controllo: {(missing.Count == 1 ? "manca" : "mancano")} {string.Join(", ", missing.Select(m => "«" + m + "»"))}.", error: true);
            ChecklistOpen = false;
        }
        _checklistPartWasRunning = partRunning;
    }
}
