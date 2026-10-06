using System.Windows.Shell;
using TimerSala.App.Interop;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>Integrazione con Windows: avanzamento sull'icona della barra, mini automatica, avvio con Windows.</summary>
public sealed partial class MainViewModel
{
    /// <summary>Colore dell'avanzamento sull'icona: verde (Normal), giallo (Paused), rosso (Error) o nessuno.</summary>
    public TaskbarItemProgressState TaskbarState { get; private set => Set(ref field, value); }
    public double TaskbarProgress { get; private set => Set(ref field, value); }

    bool _partWasRunning;

    void TickWindows(TimerSnapshot s)
    {
        bool part = s.IsRunning && s.Mode != TimerMode.Countdown && s.TargetSeconds > 0;
        TaskbarState = !part ? TaskbarItemProgressState.None : s.Phase switch
        {
            TimerPhase.Overtime => TaskbarItemProgressState.Error,
            TimerPhase.Warning => TaskbarItemProgressState.Paused,
            _ => TaskbarItemProgressState.Normal,
        };
        TaskbarProgress = s.Phase == TimerPhase.Overtime ? 1 : s.Progress;

        // all'avvio della prima parte dell'adunanza il controller passa alla mini (opzione)
        bool runningPart = s.IsRunning && s.Mode == TimerMode.Part;
        if (runningPart && !_partWasRunning && Settings.AutoMiniOnFirstPart && !IsMiniMode && IsFirstPartOfMeeting())
            IsMiniMode = true;
        _partWasRunning = runningPart;
    }

    bool IsFirstPartOfMeeting()
    {
        int running = Timer.RunningIndex;
        for (int i = 0; i < Timer.Meeting.Parts.Count; i++)
            if (i != running && Timer.ActualFor(i) is not null) return false;
        return true;
    }

    void ApplyWindowsSettings() => AutoStart.Apply(Settings.StartWithWindows);
}
