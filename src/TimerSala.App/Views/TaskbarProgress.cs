using System.Windows;
using System.Windows.Data;
using System.Windows.Shell;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

/// <summary>Avanzamento della parte sull'icona della barra delle applicazioni: verde, giallo e rosso come le cifre.</summary>
public static class TaskbarProgress
{
    public static void Attach(Window window, MainViewModel vm)
    {
        var info = new TaskbarItemInfo();
        BindingOperations.SetBinding(info, TaskbarItemInfo.ProgressStateProperty, new Binding(nameof(MainViewModel.TaskbarState)) { Source = vm });
        BindingOperations.SetBinding(info, TaskbarItemInfo.ProgressValueProperty, new Binding(nameof(MainViewModel.TaskbarProgress)) { Source = vm });
        window.TaskbarItemInfo = info;
    }
}
