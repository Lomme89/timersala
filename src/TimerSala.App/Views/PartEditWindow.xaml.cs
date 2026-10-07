using System.Windows;
using System.Windows.Input;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

/// <summary>Modifica al volo titolo e durata di una parte, dal menu della parte.</summary>
public partial class PartEditWindow : Window
{
    public string PartTitle => TitleBox.Text.Trim();

    public int DurationSeconds { get; private set; }

    public PartEditWindow(PartItemViewModel part)
    {
        InitializeComponent();
        Interop.DarkTitleBar.HideIconWhenCreated(this);
        TitleBox.Text = part.Title;
        // le durate con i secondi (rare) si arrotondano al minuto solo se le si cambia
        int seconds = part.Part.DurationSeconds;
        MinutesBox.Value = Math.Max(1, (int)Math.Round(seconds / 60.0));
        _original = seconds;
        _originalMinutes = MinutesBox.Value;
        RunningHint.Visibility = part.IsRunning ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => { TitleBox.Focus(); TitleBox.SelectAll(); };
    }

    readonly int _original, _originalMinutes;

    void Apply_Click(object sender, RoutedEventArgs e)
    {
        // conferma il numero appena scritto
        if (Keyboard.FocusedElement is UIElement focused) focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        DurationSeconds = MinutesBox.Value == _originalMinutes ? _original : MinutesBox.Value * 60;
        DialogResult = true;
    }
}
