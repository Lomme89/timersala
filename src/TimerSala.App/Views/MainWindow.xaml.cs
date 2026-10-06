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
        ControllerMotion.Attach(this, vm, StartButton, StartIcon, Digits);
        TaskbarProgress.Attach(this, vm);
        if (Background is System.Windows.Media.SolidColorBrush bg) Theming.Motion.PaintBackgroundEarly(this, bg.Color);
        Topmost = vm.Settings.ControllerTopmost;

        vm.MiniModeChanged += (_, _) => ApplyMiniMode();
        Loaded += (_, _) =>
        {
            UpdateTimerWindow();
            ApplyMiniMode();
            OfferRestore();
            // dopo un aggiornamento le novità, ma non nel mezzo di un'adunanza ripresa
            if (!_vm.IsRunning) HelpActions.ShowNewsIfUpdated(this, _vm);
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

    /// <summary>Comandi dal menu dell'icona sulla barra delle applicazioni (arrivano dalla copia già aperta).</summary>
    public void RunShellCommand(string command)
    {
        switch (command)
        {
            case "schermo":
                _vm.TimerWindowVisible = !_vm.TimerWindowVisible;
                break;
            case "mini":
                _vm.IsMiniMode = !_vm.IsMiniMode;
                break;
            case "telefono":
                BringToFront();
                ShowPhoneQr();
                break;
            default:
                BringToFront();
                break;
        }
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
            // la finestra si restringe nella mini (o si apre subito, la prima volta)
            if (IsVisible) Theming.Motion.SwitchWindows(this, _mini);
            else { _mini.Show(); _mini.Activate(); }
        }
        else if (_mini is { IsVisible: true })
        {
            Theming.Motion.SwitchWindows(_mini, this);
        }
        else
        {
            Show();
            Activate();
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

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            HelpActions.ShowHelp(this, _vm);
            e.Handled = true;
            return;
        }
        Shortcuts.Handle(_vm, e, () => Edit_Click(this, new RoutedEventArgs()));
    }

    void PartsList_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: PartItemViewModel item })
            _vm.SelectPartCommand.Execute(item);
    }

    // ───── menu della parte (tasto destro o «⋯») ─────

    PartItemViewModel? _menuPart;

    // il tasto destro seleziona la parte prima di aprire il menu
    void PartsList_RightClick(object sender, MouseButtonEventArgs e)
    {
        _menuPart = null;
        if (e.OriginalSource is FrameworkElement { DataContext: PartItemViewModel item })
        {
            _menuPart = item;
            if (!item.IsRunning) _vm.SelectPartCommand.Execute(item);
        }
    }

    void PartMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PartItemViewModel item } button) return;
        _menuPart = item;
        PartMenu.PlacementTarget = button;
        PartMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        PartMenu.IsOpen = true;
        e.Handled = true;
    }

    void PartMenu_Opened(object sender, RoutedEventArgs e)
    {
        var p = _menuPart ?? _vm.Parts.FirstOrDefault(x => x.IsSelected);
        _menuPart = p;
        EditPartItem.IsEnabled = _vm.CanEditPart(p);
        MovePartItem.IsEnabled = _vm.CanMovePart(p);
        SkipPartItem.IsEnabled = _vm.CanSkipPart(p);
        MovePartItem.Header = p is not null && _vm.NextPartTitle(p) is { } next ? $"Sposta dopo «{Short(next)}»" : "Sposta dopo la prossima";
        ResetPartItem.IsEnabled = p is { IsTimed: true, IsRunning: false } && p.ActualText is not null;
    }

    static string Short(string s) => s.Length <= 28 ? s : s[..26].TrimEnd() + "…";

    void EditPart_Click(object sender, RoutedEventArgs e)
    {
        if (_menuPart is not { } p) return;
        var dlg = new PartEditWindow(p) { Owner = this };
        if (dlg.ShowDialog() == true) _vm.EditPart(p, dlg.PartTitle, dlg.DurationSeconds);
    }

    void MovePart_Click(object sender, RoutedEventArgs e)
    {
        if (_menuPart is not { } p || _vm.NextPartTitle(p) is not { } next) return;
        if (MessageBox.Show(this, $"Spostare «{p.Title}» dopo «{next}»?\nLe due parti si scambiano di posto.", "TimerSala",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            _vm.MovePartAfterNext(p);
    }

    void SkipPart_Click(object sender, RoutedEventArgs e)
    {
        if (_menuPart is not { } p) return;
        if (MessageBox.Show(this, $"Saltare «{p.Title}»?\nConta come durata zero: il ritardo ne tiene conto.", "TimerSala",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            _vm.SkipPart(p);
    }

    void ResetPart_Click(object sender, RoutedEventArgs e)
    {
        if (_menuPart is { } p) _vm.SelectPartCommand.Execute(p);
        _vm.ResetSelectedCommand.Execute(null);
    }

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

    void Qr_Click(object sender, RoutedEventArgs e) => ShowPhoneQr();

    void Help_Click(object sender, RoutedEventArgs e) => HelpActions.ShowHelp(this, _vm);

    void ExportWeek_Click(object sender, RoutedEventArgs e) => ExchangeDialogs.ExportWeek(this, _vm);

    void ImportFile_Click(object sender, RoutedEventArgs e) => ExchangeDialogs.Import(this, _vm);

    void ShowPhoneQr()
    {
        Window owner = _vm.IsMiniMode && _mini is not null ? _mini : this;
        if (_vm.WebUrl is null)
        {
            MessageBox.Show(owner, "Il server web non è attivo. Attivalo in Impostazioni → Rete e telefono.", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (OwnedWindows.OfType<QrWindow>().Any()) return;
        new QrWindow(_vm) { Owner = owner }.ShowDialog();
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
