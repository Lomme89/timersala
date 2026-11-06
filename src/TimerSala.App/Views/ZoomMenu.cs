using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TimerSala.App.Views;

/// <summary>La freccia accanto al pulsante Zoom apre il menu con un clic sinistro, sotto di sé.</summary>
static class ZoomMenu
{
    public static void Open(object sender)
    {
        if (sender is not Button { ContextMenu: { } menu } b) return;
        menu.PlacementTarget = b;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}
