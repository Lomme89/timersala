using System.Collections.ObjectModel;
using System.Windows.Input;
using TimerSala.Core.Models;

namespace TimerSala.App.ViewModels;

/// <summary>Una riga dell'evento: titolo e minuti.</summary>
public sealed class FreeEventRow : ObservableObject
{
    public string Title { get; set => Set(ref field, value); } = "";
    public int Minutes { get; set => Set(ref field, value); } = 30;
}

/// <summary>Finestra «Evento fuori programma»: titolo, ora d'inizio, parti; gli ultimi eventi come suggerimenti.</summary>
public sealed class FreeEventViewModel : ObservableObject
{
    public FreeEventViewModel(MainViewModel main)
    {
        Recent = main.Settings.RecentEvents;
        // ora d'inizio proposta: la prossima mezz'ora
        var now = DateTime.Now;
        var next = now.Date.AddHours(now.Hour).AddMinutes(now.Minute < 30 ? 30 : 60);
        Start = TimeOnly.FromDateTime(next);
        Rows.Add(new FreeEventRow { Title = "Discorso", Minutes = 30 });
    }

    public string Title { get; set => Set(ref field, value); } = "";
    public TimeOnly? Start { get; set => Set(ref field, value); }
    public ObservableCollection<FreeEventRow> Rows { get; } = [];
    public IReadOnlyList<FreeEvent> Recent { get; }
    public bool HasRecent => Recent.Count > 0;

    public ICommand AddRowCommand => field ??= new RelayCommand(() => Rows.Add(new FreeEventRow { Title = $"Parte {Rows.Count + 1}", Minutes = 10 }));
    public ICommand RemoveRowCommand => field ??= new RelayCommand(p => { if (p is FreeEventRow r && Rows.Count > 1) Rows.Remove(r); });

    public ICommand UseRecentCommand => field ??= new RelayCommand(p =>
    {
        if (p is not FreeEvent e) return;
        Title = e.Title;
        Rows.Clear();
        foreach (var part in e.Parts) Rows.Add(new FreeEventRow { Title = part.Title, Minutes = part.Minutes });
    });

    public FreeEvent ToEvent() =>
        new(string.IsNullOrWhiteSpace(Title) ? "Evento" : Title.Trim(), Rows.Select(r => new FreeEventPart(r.Title, r.Minutes)).ToList());
}
