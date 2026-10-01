using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TimerSala.App.Interop;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm;
    TimerWindow? _timerWindow;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.Confirm = msg => MessageBox.Show(this, msg, "TimerSala", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        vm.DisplayTargetChanged += (_, _) => Dispatcher.BeginInvoke(UpdateTimerWindow);

        RestorePlacement();
        Topmost = vm.Settings.ControllerTopmost;

        Loaded += (_, _) => UpdateTimerWindow();
        LocationChanged += (_, _) => UpdateTopmostOfDisplay();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => _vm.RefreshMonitors());

    void UpdateTimerWindow()
    {
        var monitor = _vm.SelectedMonitor;
        if (!_vm.TimerWindowVisible || monitor is null)
        {
            _timerWindow?.Hide();
            return;
        }
        _timerWindow ??= new TimerWindow(_vm);
        _timerWindow.Monitor = monitor;
        if (!_timerWindow.IsVisible) _timerWindow.Show();
        _timerWindow.PlaceOnMonitor();
        UpdateTopmostOfDisplay();
        Activate();
    }

    void UpdateTopmostOfDisplay()
    {
        if (_timerWindow is null || _vm.SelectedMonitor is null) return;
        // se il timer è sullo stesso schermo del controller non deve coprirlo
        bool sameScreen = Monitors.DeviceOf(this) == _vm.SelectedMonitor.DeviceName;
        _timerWindow.Topmost = !sameScreen;
    }

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox or ComboBox or ComboBoxItem) return;
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                _vm.ToggleStartCommand.Execute(null);
                break;
            case Key.Right:
            case Key.Down:
            case Key.PageDown:
                _vm.NextCommand.Execute(null);
                break;
            case Key.Left:
            case Key.Up:
            case Key.PageUp:
                _vm.PreviousCommand.Execute(null);
                break;
            case Key.Add:
            case Key.OemPlus:
                _vm.AddMinuteCommand.Execute(null);
                break;
            case Key.Subtract:
            case Key.OemMinus:
                _vm.RemoveMinuteCommand.Execute(null);
                break;
            case Key.C:
                _vm.CounselCommand.Execute(null);
                break;
            case Key.E when Keyboard.Modifiers == ModifierKeys.None:
                Edit_Click(this, new RoutedEventArgs());
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    void PartsList_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: PartItemViewModel item })
            _vm.SelectPartCommand.Execute(item);
    }

    // il tasto destro seleziona la parte prima di aprire il menu
    void PartsList_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: PartItemViewModel item })
            _vm.SelectPartCommand.Execute(item);
    }

    void ResetPart_Click(object sender, RoutedEventArgs e) => _vm.ResetSelectedCommand.Execute(null);

    void ResetAll_Click(object sender, RoutedEventArgs e) => _vm.ResetAllCommand.Execute(null);

    void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.IsRunning)
        {
            MessageBox.Show(this, "Ferma il timer prima di modificare lo schema.", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var editor = new EditorWindow(_vm) { Owner = this };
        editor.ShowDialog();
    }

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_vm) { Owner = this };
        if (dlg.ShowDialog() == true)
            Topmost = _vm.Settings.ControllerTopmost;
    }

    void Identify_Click(object sender, RoutedEventArgs e) => IdentifyWindow.ShowAll();

    void WebLink_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.WebUrl is null) return;
        try { Process.Start(new ProcessStartInfo(_vm.WebUrl) { UseShellExecute = true }); } catch { }
    }

    void RestorePlacement()
    {
        var s = _vm.Settings;
        if (s.ControllerWidth is > 300 and < 4000) Width = s.ControllerWidth.Value;
        if (s.ControllerHeight is > 300 and < 4000) Height = s.ControllerHeight.Value;
        if (s.ControllerLeft is { } l && s.ControllerTop is { } t &&
            l >= SystemParameters.VirtualScreenLeft && t >= SystemParameters.VirtualScreenTop &&
            l < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100 &&
            t < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = l;
            Top = t;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_vm.IsRunning &&
            MessageBox.Show(this, "Il timer è in funzione. Chiudere comunque TimerSala?", "TimerSala",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        var s = _vm.Settings;
        if (WindowState == WindowState.Normal)
        {
            s.ControllerLeft = Left;
            s.ControllerTop = Top;
            s.ControllerWidth = Width;
            s.ControllerHeight = Height;
        }
        _vm.SaveSettings();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _timerWindow?.Close();
        base.OnClosing(e);
    }
}
