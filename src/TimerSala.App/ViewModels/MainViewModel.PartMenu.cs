using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>
/// Menu della parte (tasto destro o «⋯»), per gli imprevisti durante l'adunanza senza aprire l'editor:
/// modifica, sposta dopo la prossima, salta. Ogni azione si può annullare per qualche secondo.
/// </summary>
public sealed partial class MainViewModel
{
    public bool CanEditPart(PartItemViewModel? p) => p is { IsTimed: true } && Timer.CanEditParts;

    public bool CanMovePart(PartItemViewModel? p) =>
        CanEditPart(p) && !p!.IsRunning && Timer.NextTimedAfter(p.Index) is var next && next >= 0 && next != Timer.RunningIndex;

    public bool CanSkipPart(PartItemViewModel? p) => CanEditPart(p) && !p!.IsRunning && !p.IsSkipped;

    /// <summary>Titolo della parte che verrebbe scambiata con questa.</summary>
    public string? NextPartTitle(PartItemViewModel p) =>
        Timer.NextTimedAfter(p.Index) is var n && n >= 0 ? Timer.Meeting.Parts[n].Title : null;

    public void EditPart(PartItemViewModel p, string title, int durationSeconds)
    {
        if (!CanEditPart(p)) return;
        ChangeParts($"«{Shorten(p.Title)}» modificata", () => Timer.EditPart(p.Index, title, durationSeconds));
    }

    public void MovePartAfterNext(PartItemViewModel p)
    {
        if (!CanMovePart(p)) return;
        ChangeParts($"«{Shorten(p.Title)}» spostata dopo la prossima", () => Timer.MoveAfterNext(p.Index));
    }

    public void SkipPart(PartItemViewModel p)
    {
        if (!CanSkipPart(p)) return;
        ChangeParts($"«{Shorten(p.Title)}» saltata", () => Timer.Skip(p.Index));
    }

    /// <summary>Esegue la modifica, la salva nella settimana e offre «Annulla».</summary>
    void ChangeParts(string text, Func<bool> change)
    {
        var snapshot = Timer.Snapshot();
        bool wasEdited = Week.EditedManually;
        if (!change()) return;
        AfterPartsChanged(edited: true);
        Timer.ForgetStop();
        OfferUndo(text, () =>
        {
            Timer.Restore(snapshot);
            AfterPartsChanged(edited: wasEdited);
            ShowStatus("Modifica annullata.");
        });
        ShowStatus(text + ".");
    }

    void AfterPartsChanged(bool edited)
    {
        // in addestramento si modifica solo la copia di prova
        if (!IsTemporaryMeeting)
        {
            Week.EditedManually = edited;
            _store.SaveWeek(Week);
        }
        UpdateAdaptiveParts();
        RebuildParts();
        UpdateWeekTexts();
        RefreshDisplay();
    }

    static string Shorten(string title) => title.Length <= 32 ? title : title[..30].TrimEnd() + "…";
}
