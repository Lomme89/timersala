using System.Windows.Controls;
using System.Windows.Input;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

/// <summary>Scorciatoie da tastiera comuni a controller e modalità mini.</summary>
static class Shortcuts
{
    public static void Handle(MainViewModel vm, KeyEventArgs e, Action? edit = null)
    {
        if (e.OriginalSource is TextBox or ComboBox or ComboBoxItem) return;
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                vm.ToggleStartCommand.Execute(null);
                break;
            case Key.Right:
            case Key.Down:
            case Key.PageDown:
                vm.NextCommand.Execute(null);
                break;
            case Key.Left:
            case Key.Up:
            case Key.PageUp:
                vm.PreviousCommand.Execute(null);
                break;
            case Key.Add:
            case Key.OemPlus:
                vm.AddMinuteCommand.Execute(null);
                break;
            case Key.Subtract:
            case Key.OemMinus:
                vm.RemoveMinuteCommand.Execute(null);
                break;
            case Key.Escape when vm.CanUndo:
                vm.UndoCommand.Execute(null);
                break;
            case Key.M:
                vm.ToggleMiniCommand.Execute(null);
                break;
            case Key.E when edit is not null && Keyboard.Modifiers == ModifierKeys.None:
                edit();
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
