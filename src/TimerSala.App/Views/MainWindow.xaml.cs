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
    MiniWindow? _mini;
    bool _closing;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.Confirm = msg => MessageBox.Show(this, msg, "TimerSala", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        vm.DisplayTargetChanged += (_, _) => Dispatcher.BeginInvoke(UpdateTimerWindow);

        RestorePlacement();
        Topmost = vm.Settings.ControllerTopmost;

        vm.MiniModeChanged += (_, _) => ApplyMiniMode();
        Loaded += (_, _) =>
        {
            UpdateTimerWindow();
            ApplyMiniMode();
        };
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
        if (_vm.IsMiniMode) _mini?.Activate(); else Activate();
    }

    void ApplyMiniMode()
    {
        if (_vm.IsMiniMode)
        {
            if (_mini is null)
            {
                _mini = new MiniWindow(_vm);
                // chiudere la mini equivale a chiudere il programma
                _mini.Closing += (_, e) =>
                {
                    if (_closing) return;
                    e.Cancel = true;
                    Dispatcher.BeginInvoke(Close);
                };
            }
            _mini.Show();
            _mini.Activate();
            Hide();
        }
        else
        {
            Show();
            Activate();
            _mini?.Hide();
        }
    }

    void UpdateTopmostOfDisplay()
    {
        if (_timerWindow is null || _vm.SelectedMonitor is null) return;
        // se il timer è sullo stesso schermo del controller non deve coprirlo
        bool sameScreen = Monitors.DeviceOf(this) == _vm.SelectedMonitor.DeviceName;
        _timerWindow.Topmost = !sameScreen;
    }

    void OnPreviewKeyDown(object sender, KeyEventArgs e) =>
        Shortcuts.Handle(_vm, e, () => Edit_Click(this, new RoutedEventArgs()));

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

    void Qr_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.WebUrl is null)
        {
            MessageBox.Show(this, "Il server web non è attivo. Attivalo in Impostazioni → Rete.", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        new QrWindow(_vm) { Owner = this }.ShowDialog();
    }

    void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _vm.SendMessageCommand.Execute(null);
        e.Handled = true;
    }

    void Address_Click(object sender, MouseButtonEventArgs e)
    {
        if (_vm.WebUrl is null) return;
        if (e.ClickCount >= 2)
        {
            try { Process.Start(new ProcessStartInfo(_vm.WebUrl) { UseShellExecute = true }); } catch { }
            return;
        }
        try
        {
            Clipboard.SetText(_vm.WebUrl);
            _vm.ShowInfo($"Indirizzo copiato: {_vm.WebUrl}");
        }
        catch { /* appunti occupati da un altro programma */ }
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
            MessageBox.Show(_vm.IsMiniMode && _mini is not null ? _mini : this, "Il timer è in funzione. Chiudere comunque TimerSala?", "TimerSala",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        var s = _vm.Settings;
        if (WindowState == WindowState.Normal && !_vm.IsMiniMode)
        {
            s.ControllerLeft = Left;
            s.ControllerTop = Top;
            s.ControllerWidth = Width;
            s.ControllerHeight = Height;
        }
        _vm.SaveSettings();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _closing = true;
        _timerWindow?.Close();
        _mini?.Close();
        base.OnClosing(e);
    }
}
