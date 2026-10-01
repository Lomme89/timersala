using System.Collections.ObjectModel;
using TimerSala.Core.Models;

using System.Windows.Input;

namespace TimerSala.App.ViewModels;

public sealed record SectionOption(PartSection Value, string Label);

public sealed partial class EditablePart : ObservableObject
{
    public string Title { get; set => Set(ref field, value); } = "";
    public PartSection Section { get; set => Set(ref field, value); }
    public double Minutes { get; set => Set(ref field, value); }
    public bool IsSong { get; set => Set(ref field, value); }
    public bool HasCounsel { get; set => Set(ref field, value); }
    public string? Detail { get; set; }

    public static EditablePart From(MeetingPart p) => new()
    {
        Title = p.Title,
        Section = p.Section,
        Minutes = Math.Round(p.DurationSeconds / 60.0, 2),
        IsSong = p.IsSong,
        HasCounsel = p.HasCounsel,
        Detail = p.Detail,
    };

    public MeetingPart ToPart() => new()
    {
        Title = Title.Trim(),
        Section = Section,
        DurationSeconds = IsSong ? 0 : (int)Math.Round(Math.Max(0, Minutes) * 60),
        IsSong = IsSong,
        HasCounsel = HasCounsel && !IsSong,
        Detail = Detail,
    };
}

public sealed partial class EditorViewModel : ObservableObject
{
    readonly MainViewModel _main;
    readonly MeetingKind _kind;

    public ObservableCollection<EditablePart> Parts { get; } = [];

    public IReadOnlyList<SectionOption> Sections { get; } =
        Enum.GetValues<PartSection>().Select(s => new SectionOption(s, SectionInfo.Label(s))).ToList();

    public string MeetingTitle { get; set => Set(ref field, value); }
    public EditablePart? Selected { get; set => Set(ref field, value); }

    public string Heading { get; }

    public EditorViewModel(MainViewModel main)
    {
        _main = main;
        _kind = main.Kind;
        var m = main.CurrentMeeting;
        MeetingTitle = m.Title;
        Heading = $"{(_kind == MeetingKind.Midweek ? "Adunanza infrasettimanale" : "Adunanza del fine settimana")} · {main.WeekTitle}";
        Load(m);
    }

    void Load(Meeting m)
    {
        Parts.Clear();
        foreach (var p in m.Parts) Parts.Add(EditablePart.From(p));
        Selected = Parts.FirstOrDefault();
    }

    public string TotalText =>
        $"Totale parti: {Parts.Where(p => !p.IsSong).Sum(p => p.Minutes):0.#} min";

    public void NotifyTotal() => OnPropertyChanged(nameof(TotalText));

    public ICommand AddPartCommand => field ??= new RelayCommand(AddPart);


    void AddPart()
    {
        var section = Selected?.Section ?? PartSection.Other;
        Insert(new EditablePart { Title = "Nuova parte", Section = section, Minutes = 5 });
    }

    public ICommand AddSongCommand => field ??= new RelayCommand(AddSong);


    void AddSong()
    {
        var section = Selected?.Section ?? PartSection.Other;
        Insert(new EditablePart { Title = "Cantico", Section = section, IsSong = true });
    }

    void Insert(EditablePart p)
    {
        int at = Selected is null ? Parts.Count : Parts.IndexOf(Selected) + 1;
        Parts.Insert(at, p);
        Selected = p;
        NotifyTotal();
    }

    public ICommand RemoveCommand => field ??= new RelayCommand(Remove);


    void Remove()
    {
        if (Selected is null) return;
        int i = Parts.IndexOf(Selected);
        Parts.RemoveAt(i);
        Selected = Parts.Count == 0 ? null : Parts[Math.Min(i, Parts.Count - 1)];
        NotifyTotal();
    }

    public ICommand MoveUpCommand => field ??= new RelayCommand(MoveUp);


    void MoveUp() => Move(-1);
    public ICommand MoveDownCommand => field ??= new RelayCommand(MoveDown);

    void MoveDown() => Move(+1);

    void Move(int dir)
    {
        if (Selected is null) return;
        int i = Parts.IndexOf(Selected), j = i + dir;
        if (j < 0 || j >= Parts.Count) return;
        var item = Selected;
        Parts.Move(i, j);
        Selected = item;
    }

    public ICommand RestoreDownloadedCommand => field ??= new RelayCommand(RestoreDownloaded);


    void RestoreDownloaded()
    {
        var week = _main.Week;
        var m = (_kind == MeetingKind.Midweek ? week.DownloadedMidweek : week.DownloadedWeekend)?.Clone()
                ?? (_kind == MeetingKind.Midweek ? MeetingTemplates.DefaultMidweek() : MeetingTemplates.DefaultWeekend());
        if (week.CircuitOverseerVisit) m = MeetingTemplates.ApplyOverseerVisit(m);
        MeetingTitle = m.Title;
        Load(m);
        NotifyTotal();
    }

    public ICommand UseTemplateCommand => field ??= new RelayCommand(UseTemplate);


    void UseTemplate()
    {
        var m = _kind == MeetingKind.Midweek ? MeetingTemplates.DefaultMidweek() : MeetingTemplates.DefaultWeekend();
        MeetingTitle = m.Title;
        Load(m);
        NotifyTotal();
    }

    public bool HasDownloaded => (_kind == MeetingKind.Midweek ? _main.Week.DownloadedMidweek : _main.Week.DownloadedWeekend) is not null;

    public void Save()
    {
        var meeting = new Meeting
        {
            Kind = _kind,
            Title = string.IsNullOrWhiteSpace(MeetingTitle) ? _main.CurrentMeeting.Title : MeetingTitle.Trim(),
            Parts = Parts.Where(p => !string.IsNullOrWhiteSpace(p.Title)).Select(p => p.ToPart()).ToList(),
        };
        _main.SaveEditedMeeting(meeting);
    }
}
