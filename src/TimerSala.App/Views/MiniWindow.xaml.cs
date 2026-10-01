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

    void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
