using System.Windows;

namespace TimerSala.App.Views;

static class WindowSizing
{
    /// <summary>Riduce le dimensioni predefinite se lo schermo è più piccolo della finestra.</summary>
    public static void FitToScreen(Window w)
    {
        var area = SystemParameters.WorkArea;
        if (w.Height > area.Height - 24) w.Height = Math.Max(w.MinHeight, area.Height - 24);
        if (w.Width > area.Width - 24) w.Width = Math.Max(w.MinWidth, area.Width - 24);
    }
}
