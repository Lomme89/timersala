using System.Windows;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

/// <summary>Chiede come riprendere un'adunanza rimasta in corso alla chiusura precedente.</summary>
public partial class RestoreWindow : Window
{
    public MainViewModel.RestoreChoice Choice { get; private set; } = MainViewModel.RestoreChoice.StartOver;

    public RestoreWindow(string description)
    {
        InitializeComponent();
        Interop.DarkTitleBar.HideIconWhenCreated(this);
        Description.Text = description;
    }

    void Count_Click(object sender, RoutedEventArgs e) => Close(MainViewModel.RestoreChoice.CountDowntime);
    void Resume_Click(object sender, RoutedEventArgs e) => Close(MainViewModel.RestoreChoice.ResumeAsWas);
    void StartOver_Click(object sender, RoutedEventArgs e) => Close(MainViewModel.RestoreChoice.StartOver);

    void Close(MainViewModel.RestoreChoice choice)
    {
        Choice = choice;
        DialogResult = true;
    }
}
