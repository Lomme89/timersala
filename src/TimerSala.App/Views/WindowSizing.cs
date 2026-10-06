using System.Windows;
using TimerSala.App.Interop;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

static class WindowSizing
{
    /// <summary>La finestra si riapre dove e grande com'era l'ultima volta (chiave in <c>Settings.Windows</c>).</summary>
    public static void Remember(Window w, MainViewModel vm, string key)
    {
        WindowPlacement.Restore(w, vm.Settings.Windows.GetValueOrDefault(key));
        w.Closing += (_, e) =>
        {
            if (e.Cancel || WindowPlacement.Capture(w) is not { } bounds) return;
            vm.Settings.Windows[key] = bounds;
            vm.SaveSettings();
        };
    }

    /// <summary>Riduce le dimensioni predefinite se lo schermo è più piccolo della finestra.</summary>
    public static void FitToScreen(Window w)
    {
        var area = SystemParameters.WorkArea;
        if (w.Height > area.Height - 24) w.Height = Math.Max(w.MinHeight, area.Height - 24);
        if (w.Width > area.Width - 24) w.Width = Math.Max(w.MinWidth, area.Width - 24);
    }
}
