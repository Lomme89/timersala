using System.Windows;
using System.Windows.Input;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

/// <summary>Controller ridotto, sempre in primo piano, per chi ha un solo monitor.</summary>
public partial class MiniWindow : Window
{
    readonly MainViewModel _vm;

    public MiniWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        PreviewKeyDown += (_, e) => Shortcuts.Handle(vm, e);

        // allo sforamento anche il bordo di sistema della finestra (Windows 11) diventa rosso
        vm.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) => vm.PropertyChanged -= OnViewModelChanged;

        if (vm.Settings.MiniLeft is { } l && vm.Settings.MiniTop is { } t &&
            l >= SystemParameters.VirtualScreenLeft && t >= SystemParameters.VirtualScreenTop &&
            l < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 &&
            t < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60)
        {
            Left = l;
            Top = t;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 16;
            Top = area.Bottom - 190;
        }
        LocationChanged += (_, _) =>
        {
            _vm.Settings.MiniLeft = Left;
            _vm.Settings.MiniTop = Top;
        };
    }

    void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsOvertime))
            Interop.DarkTitleBar.SetBorder(this, _vm.IsOvertime ? System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44) : null);
    }

    void Message_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? _vm.SendMessageFullCommand : _vm.SendMessageCommand).Execute(null);
        e.Handled = true;
    }

    void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
