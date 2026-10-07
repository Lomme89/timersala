using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TimerSala.App.Theming;
using TimerSala.App.ViewModels;
using TimerSala.Core.Timing;

namespace TimerSala.App.Views;

/// <summary>
/// Movimento di controller e mini: il pulsante Avvia/Ferma sfuma da un colore all'altro con l'icona che entra
/// con un piccolo scatto, e le cifre fanno un solo «respiro» al passaggio verde → giallo → rosso.
/// </summary>
static class ControllerMotion
{
    static Color Start => UiTheme.ColorOf("StartBrush");
    static Color Stop => UiTheme.ColorOf("StopBrush");
    static Color Waiting => UiTheme.ColorOf("WaitBrush");

    public static void Attach(Window window, MainViewModel vm, Button startButton, FrameworkElement? icon, FrameworkElement? digits)
    {
        var lastPhase = vm.Phase;
        void Update(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.IsRunning):
                case nameof(MainViewModel.IsVoiceArmed):
                    Motion.FadeBackground(startButton, vm.IsVoiceArmed ? Waiting : vm.IsRunning ? Stop : Start);
                    if (icon is not null) Motion.Pop(icon);
                    break;
                case nameof(MainViewModel.Phase):
                    // solo quando il tempo peggiora: verde → giallo, giallo → rosso
                    if (digits is not null && vm.Phase is TimerPhase.Warning or TimerPhase.Overtime && vm.Phase > lastPhase && lastPhase != TimerPhase.Idle)
                        Motion.Breathe(digits);
                    lastPhase = vm.Phase;
                    break;
            }
        }
        window.Loaded += (_, _) => Motion.FadeBackground(startButton, vm.IsVoiceArmed ? Waiting : vm.IsRunning ? Stop : Start);
        vm.PropertyChanged += Update;
        window.Closed += (_, _) => vm.PropertyChanged -= Update;
    }
}
