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
        WindowSizing.FitToScreen(this);
        Topmost = vm.Settings.ControllerTopmost;

        vm.MiniModeChanged += (_, _) => ApplyMiniMode();
        Loaded += (_, _) =>
        {
            UpdateTimerWindow();
            ApplyMiniMode();
            OfferRestore();
        };
        LocationChanged += (_, _) => UpdateTopmostOfDisplay();
        vm.Timer.StateChanged += (_, _) => Dispatcher.BeginInvoke(CenterCurrentPart, System.Windows.Threading.DispatcherPriority.Background);
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    // Windows avvisa del cambio di schermi prima di aver finito di sistemarli (e a volte sposta le finestre dopo):
    // si ricontrolla subito e poi ancora due volte
    void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        _vm.RefreshMonitors();
        foreach (var delay in new[] { 1500, 4000 })
        {
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            t.Tick += (_, _) => { t.Stop(); if (!_closing) _vm.RefreshMonitors(); };
            t.Start();
        }
    });

    void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) OnDisplaySettingsChanged(sender, e);
    }

    /// <summary>Riporta in primo piano il programma (controller o mini), per esempio se lo si apre una seconda volta.</summary>
    public void BringToFront()
    {
        Window w = _vm.IsMiniMode && _mini is not null ? _mini : this;
        w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
        // Windows a volte non concede il primo piano: un breve «sempre in primo piano» lo forza
        bool topmost = w.Topmost;
        w.Topmost = true;
        w.Topmost = topmost;
        w.Focus();
    }

    void UpdateTimerWindow()
    {
        var monitor = _vm.SelectedMonitor;
        if (!_vm.TimerWindowVisible || monitor is null || _vm.TimerScreenDisconnected)
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

    /// <summary>Se l'adunanza era in corso quando il programma si è chiuso, propone di riprenderla.</summary>
    void OfferRestore()
    {
        if (_vm.PendingRestore is not { } session) return;
        var owner = _vm.IsMiniMode && _mini is not null ? (Window)_mini : this;
        var dlg = new RestoreWindow(_vm.DescribeRestore(session)) { Owner = owner };
        _vm.Restore(dlg.ShowDialog() == true ? dlg.Choice : MainViewModel.RestoreChoice.StartOver);
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

    int _centeredIndex = -1;

    /// <summary>Lo scorrimento tiene al centro la parte in corso (o selezionata) quando cambia.</summary>
    void CenterCurrentPart()
    {
        var current = _vm.Parts.FirstOrDefault(p => p.IsRunning) ?? _vm.Parts.FirstOrDefault(p => p.IsSelected);
        if (current is null || current.Index == _centeredIndex) return;
        if (PartsList.ItemContainerGenerator.ContainerFromItem(current) is not FrameworkElement row) return;
        if (FindScrollViewer(PartsList) is not { } sv || sv.ViewportHeight <= 0) return;
        _centeredIndex = current.Index;
        double top = row.TransformToVisual(sv).Transform(new Point(0, 0)).Y + sv.VerticalOffset;
        double target = top + row.ActualHeight / 2 - sv.ViewportHeight / 2;
        sv.ScrollToVerticalOffset(Math.Clamp(target, 0, sv.ScrollableHeight));
    }

    static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv) return sv;
            if (FindScrollViewer(child) is { } found) return found;
        }
        return null;
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
        new SettingsWindow(_vm) { Owner = this }.ShowDialog();
        Topmost = _vm.Settings.ControllerTopmost;
    }

    void Download_Click(object sender, RoutedEventArgs e)
    {
        var menu = DownloadButton.ContextMenu;
        menu.PlacementTarget = DownloadButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    void DownloadWeek_Click(object sender, RoutedEventArgs e) => _vm.DownloadCommand.Execute(null);

    void DownloadAll_Click(object sender, RoutedEventArgs e) => _vm.DownloadAllCommand.Execute(null);

    void Qr_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.WebUrl is null)
        {
            MessageBox.Show(this, "Il server web non è attivo. Attivalo in Impostazioni → Rete e telefono.", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        new QrWindow(_vm) { Owner = this }.ShowDialog();
    }

    void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        // Ctrl+Invio: a tutto schermo
        (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? _vm.SendMessageFullCommand : _vm.SendMessageCommand).Execute(null);
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
        if (WindowPlacement.Restore(this, s.Windows.GetValueOrDefault("controller"))) return;
        // posizione salvata dalla 1.13 o precedenti
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
        if (_vm.CloseWarning() is { } warning &&
            MessageBox.Show(_vm.IsMiniMode && _mini is not null ? _mini : this, $"{warning}\nChiudere comunque TimerSala?", "TimerSala",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        var s = _vm.Settings;
        if (WindowPlacement.Capture(this) is { } controller) s.Windows["controller"] = controller;
        if (_mini is not null && WindowPlacement.Capture(_mini) is { } mini) s.Windows["mini"] = mini;
        _vm.SaveSettings();
        _vm.Shutdown();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _closing = true;
        _timerWindow?.Close();
        _mini?.Close();
        base.OnClosing(e);
    }
}
