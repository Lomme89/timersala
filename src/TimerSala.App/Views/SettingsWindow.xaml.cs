using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TimerSala.App.Audio;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

/// <summary>
/// Impostazioni con barra laterale. Le modifiche si applicano subito (vedi <see cref="SettingsViewModel"/>):
/// «Fatto» chiude, «Annulla le modifiche» riporta tutto com'era all'apertura.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>Sezioni della barra laterale, nell'ordine.</summary>
    public enum Section { Meetings, Screen, Countdown, Messages, Voice, Network, Program }

    static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    static Section _lastSection = Section.Meetings;

    readonly MainViewModel _main;
    readonly SettingsViewModel _vm;
    readonly bool _ready;
    bool _reverted;

    // prova dell'ingresso audio (sezione Voce)
    AudioInput? _test;
    DispatcherTimer? _meterTimer;
    double _testPeak = -90;
    bool _testVoice;

    public SettingsWindow(MainViewModel main, Section? section = null)
    {
        InitializeComponent();
        Interop.DarkTitleBar.HideIconWhenCreated(this);
        WindowSizing.FitToScreen(this);
        WindowSizing.Remember(this, main, "impostazioni");
        _main = main;
        _vm = new SettingsViewModel(main);

        var devices = new List<Choice<string>> { new("", "Predefinito di Windows") };
        devices.AddRange(AudioInput.Devices().Select(d => new Choice<string>(d.Id, d.Name)));
        VoiceDevice.ItemsSource = devices;

        DataContext = _vm;
        PinBox.Text = _vm.RemotePin;
        ProfileNameBox.Text = main.ProfileName;
        _ready = true;
        Nav.SelectedIndex = (int)(section ?? _lastSection);
        UpdateVoiceTexts();
    }

    // ───── barra laterale ─────

    void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedIndex < 0) return;
        for (int i = 0; i < Pages.Children.Count; i++)
            Pages.Children[i].Visibility = i == Nav.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
        _lastSection = (Section)Nav.SelectedIndex;
        if (!_ready) return;
        if (_lastSection == Section.Voice) StartTest(); else StopTest();
    }

    // ───── chiusura ─────

    void Done_Click(object sender, RoutedEventArgs e) => Close();

    void Revert_Click(object sender, RoutedEventArgs e)
    {
        _vm.Revert();
        _reverted = true;
        PinBox.Text = _vm.RemotePin;
        _main.ShowInfo("Impostazioni riportate com'erano.");
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reverted)
        {
            // il campo in modifica (per esempio un orario appena scritto) conta anche se non si è premuto Invio
            if (Keyboard.FocusedElement is UIElement focused) focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            CommitPin();
            _vm.Flush();
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopTest();
        _vm.Detach();
        base.OnClosed(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Esc chiude tenendo le modifiche (sono già applicate)
        if (e.Key == Key.Escape && Keyboard.FocusedElement is not ComboBox { IsDropDownOpen: true })
        {
            Close();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    // ───── Schermo e countdown ─────

    void Identify_Click(object sender, RoutedEventArgs e) => IdentifyWindow.ShowAll();

    void PreviewCountdown_Click(object sender, RoutedEventArgs e)
    {
        _vm.Flush();
        _main.PreviewCountdown();
        if (!_main.TimerWindowVisible)
            _main.ShowInfo("Lo schermo del timer è nascosto: l'anteprima del countdown si vede solo nella pagina web.");
    }

    // ───── Messaggi: frasi pronte ─────

    void AddPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.AddPreset() is not { } item) return;
        // mette il cursore nella riga nuova
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FocusPreset(item));
    }

    void FocusPreset(PresetItem item)
    {
        if (PresetList.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter row) return;
        var box = FindChild<TextBox>(row);
        box?.Focus();
        box?.SelectAll();
    }

    static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            if (FindChild<T>(child) is { } found) return found;
        }
        return null;
    }

    void RemovePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PresetItem item }) _vm.RemovePreset(item);
    }

    void Preset_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PresetItem item }) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Up or Key.Down)
        {
            _vm.MovePreset(item, _vm.Presets.IndexOf(item) + (key == Key.Up ? -1 : 1));
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FocusPreset(item));
            e.Handled = true;
        }
        else if (key == Key.Enter)
        {
            AddPreset_Click(sender, e);
            e.Handled = true;
        }
    }

    void PresetHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PresetItem item } handle) return;
        DragDrop.DoDragDrop(handle, new DataObject(typeof(PresetItem), item), DragDropEffects.Move);
        e.Handled = true;
    }

    void Preset_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(PresetItem)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    // la frase trascinata prende il posto di quella su cui viene lasciata
    void Preset_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(PresetItem)) is PresetItem dragged && sender is FrameworkElement { Tag: PresetItem target } && dragged != target)
            _vm.MovePreset(dragged, _vm.Presets.IndexOf(target));
        e.Handled = true;
    }

    // ───── Rete: PIN ─────

    void Pin_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitPin();

    void Pin_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitPin(); e.Handled = true; }
    }

    void CommitPin()
    {
        if (_vm.TrySetPin(PinBox.Text))
        {
            PinHint.Text = "Da 4 a 12 cifre.";
            PinHint.ClearValue(TextBlock.ForegroundProperty);
            return;
        }
        // un PIN non valido non viene usato: resta quello di prima
        PinHint.Text = $"«{PinBox.Text.Trim()}» non va bene: servono da 4 a 12 cifre. Resta il PIN {_vm.RemotePin}.";
        PinHint.Foreground = (Brush)FindResource("AmberBrush");
        PinBox.Text = _vm.RemotePin;
    }

    void NewPin_Click(object sender, RoutedEventArgs e)
    {
        _vm.NewPinCommand.Execute(null);
        PinBox.Text = _vm.RemotePin;
    }

    // ───── Programma ─────

    void ExportBackup_Click(object sender, RoutedEventArgs e)
    {
        CommitPin();
        _vm.Flush();
        ExchangeDialogs.ExportBackup(this, _main);
    }

    void ImportBackup_Click(object sender, RoutedEventArgs e)
    {
        _vm.Flush();
        if (!ExchangeDialogs.Import(this, _main)) return;
        // le impostazioni ora sono quelle importate: la finestra si chiude senza riapplicare le sue
        _reverted = true;
        Close();
    }

    void Help_Click(object sender, RoutedEventArgs e) => HelpActions.ShowHelp(this, _main);

    // ───── Congregazioni ─────

    void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_main.CurrentProfile is { } p && ProfileNameBox.Text.Trim().Length > 0) _main.RenameProfile(p, ProfileNameBox.Text);
    }

    void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = NewProfileBox.Text.Trim();
        if (name.Length == 0) { NewProfileBox.Focus(); return; }
        _vm.Flush();
        if (_main.AddProfile(name) is null) return;
        NewProfileBox.Text = "";
        MessageBox.Show(this, $"Aggiunta «{name}». Per passarci usa il nome della congregazione in alto nel controller; lì poi ne imposti orari e schemi.",
            "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button { Tag: Core.Storage.ProfileInfo p }) return;
        if (p.Id == _main.CurrentProfile?.Id)
        {
            MessageBox.Show(this, "È la congregazione in uso: passa prima a un'altra.", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (p.Id == "")
        {
            MessageBox.Show(this, "La prima congregazione non si può togliere.", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(this, $"Togliere «{p.Name}» con le sue impostazioni e i suoi schemi?", "TimerSala", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            _main.RemoveProfile(p);
    }

    void ClickerAssociate_Click(object sender, RoutedEventArgs e)
    {
        _vm.Flush();
        _main.StartClickerAssociation();
    }

    void ClickerForget_Click(object sender, RoutedEventArgs e) => _main.ForgetClicker();

    void Training_Click(object sender, RoutedEventArgs e)
    {
        if (_main.StartTraining() is { } why)
        {
            MessageBox.Show(this, why, "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Close();
    }

    void Welcome_Click(object sender, RoutedEventArgs e)
    {
        _vm.Flush();
        new WelcomeWindow(_main) { Owner = this }.ShowDialog();
        // le scelte della configurazione guidata sono già applicate: la finestra si chiude senza riapplicare le sue
        _reverted = true;
        Close();
    }

    void News_Click(object sender, RoutedEventArgs e) => HelpActions.ShowNews(this);

    void ReportProblem_Click(object sender, RoutedEventArgs e) => HelpActions.Report(this, _main, isProblem: true);

    void ReportIdea_Click(object sender, RoutedEventArgs e) => HelpActions.Report(this, _main, isProblem: false);

    void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(_vm.DataFolder) { UseShellExecute = true }); } catch { }
    }

    // ───── Voce: prova dell'ingresso ─────

    void VoiceDevice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && _lastSection == Section.Voice) StartTest();
    }

    void VoiceSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        if (_test is not null) _test.ThresholdDb = VoiceThreshold.Value;
        UpdateVoiceTexts();
    }

    void UpdateVoiceTexts()
    {
        VoiceThresholdText.Text = $"{VoiceThreshold.Value:0} dB";
        VoicePauseText.Text = VoicePause.Value.ToString("0.0", It) + " s";
        VoiceMinText.Text = VoiceMin.Value.ToString("0.0", It) + " s";
        double t = MainViewModel.LevelOf(VoiceThreshold.Value);
        ThresholdLeft.Width = new GridLength(t, GridUnitType.Star);
        ThresholdRight.Width = new GridLength(1 - t, GridUnitType.Star);
    }

    // ───── Voce: taratura automatica ─────

    List<double>? _calibration;
    DispatcherTimer? _calibrationTimer;

    async void Calibrate_Click(object sender, RoutedEventArgs e)
    {
        if (_calibrationTimer is not null) return;
        if (_test is null) StartTest();
        if (_test is null) { CalibrateText.Text = "Ingresso non disponibile: scegline un altro."; return; }
        CalibrateButton.IsEnabled = false;
        try
        {
            var silence = await Collect("Silenzio in sala per 5 secondi…");
            var voice = await Collect("Ora parla al microfono per 5 secondi, come in una parte…");
            var result = Core.Audio.VoiceCalibration.Compute(silence, voice);
            CalibrateText.Text = result.Message;
            if (result.ThresholdDb is { } db) VoiceThreshold.Value = db;
        }
        finally { CalibrateButton.IsEnabled = true; }
    }

    /// <summary>Raccoglie per 5 secondi i livelli dell'ingresso, con il conto alla rovescia nel testo.</summary>
    Task<List<double>> Collect(string text)
    {
        var done = new TaskCompletionSource<List<double>>();
        var levels = new List<double>();
        _calibration = levels;
        int left = 5;
        CalibrateText.Text = $"{text} {left}";
        _calibrationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _calibrationTimer.Tick += (_, _) =>
        {
            if (--left > 0) { CalibrateText.Text = $"{text} {left}"; return; }
            _calibrationTimer!.Stop();
            _calibrationTimer = null;
            _calibration = null;
            lock (levels) done.SetResult([.. levels]);
        };
        _calibrationTimer.Start();
        return done.Task;
    }

    void StartTest()
    {
        StopTest();
        var id = (VoiceDevice.SelectedItem as Choice<string>)?.Value;
        _test = new AudioInput { ThresholdDb = VoiceThreshold.Value };
        _test.Frame += f =>
        {
            if (_calibration is { } c) lock (c) c.Add(f.LevelDb);
            if (f.LevelDb > _testPeak) _testPeak = f.LevelDb;
            if (f.IsVoice) _testVoice = true;
        };
        try
        {
            _test.Start(string.IsNullOrEmpty(id) ? null : id);
            VoiceTestStatus.Text = $"In ascolto: {_test.DeviceName}. Il segno giallo è la soglia.";
            VoiceTestStatus.ClearValue(TextBlock.ForegroundProperty);
        }
        catch (Exception ex)
        {
            _test.Dispose();
            _test = null;
            VoiceTestStatus.Text = "Ingresso non disponibile: " + ex.Message;
            VoiceTestStatus.Foreground = (Brush)FindResource("RedBrush");
            return;
        }
        _meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _meterTimer.Tick += (_, _) =>
        {
            double level = MainViewModel.LevelOf(_testPeak);
            var track = (FrameworkElement)VoiceMeter.Parent;
            VoiceMeter.Width = Math.Max(0, track.ActualWidth * level);
            VoiceLamp.Fill = _testVoice ? (Brush)FindResource("GreenBrush") : (Brush)FindResource("LineBrush");
            VoiceLampText.Text = _testVoice ? "Voce" : "Silenzio";
            _testPeak = -90;
            _testVoice = false;
        };
        _meterTimer.Start();
    }

    void StopTest()
    {
        _meterTimer?.Stop();
        _meterTimer = null;
        _test?.Dispose();
        _test = null;
        VoiceMeter.Width = 0;
    }
}
