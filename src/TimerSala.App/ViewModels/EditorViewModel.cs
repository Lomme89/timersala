using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using TimerSala.Core.Models;

namespace TimerSala.App.ViewModels;

public sealed record SectionOption(PartSection Value, string Label)
{
    public Brush Brush => BrushCache.Get(SectionInfo.Color(Value));
}

public sealed class EditablePart : ObservableObject
{
    public string Title
    {
        get;
        set { if (Set(ref field, value)) OnPropertyChanged(nameof(DisplayTitle)); }
    } = "";

    public PartSection Section
    {
        get;
        set { if (Set(ref field, value)) OnPropertyChanged(nameof(SectionBrush)); }
    }

    public double Minutes
    {
        get;
        set { if (Set(ref field, Math.Clamp(Math.Round(value, 2), 0, 240))) OnPropertyChanged(nameof(DurationText)); }
    }

    public bool IsSong
    {
        get;
        set { if (Set(ref field, value)) { OnPropertyChanged(nameof(DurationText)); OnPropertyChanged(nameof(IsTimed)); } }
    }

    public string? Detail { get; set => Set(ref field, value); }
    public bool HasCounsel { get; set; }

    public bool IsTimed { get => !IsSong; set => IsSong = !value; }
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "(senza titolo)" : Title;
    public Brush SectionBrush => BrushCache.Get(SectionInfo.Color(Section));
    public string DurationText => IsSong ? "♪" : $"{Minutes:0.##} min";

    public static EditablePart From(MeetingPart p) => new()
    {
        Title = p.Title,
        Section = p.Section,
        Minutes = Math.Round(p.DurationSeconds / 60.0, 2),
        IsSong = p.IsSong,
        HasCounsel = p.HasCounsel,
        Detail = p.Detail,
    };

    public EditablePart Copy() => new()
    {
        Title = Title,
        Section = Section,
        Minutes = Minutes,
        IsSong = IsSong,
        HasCounsel = HasCounsel,
        Detail = Detail,
    };

    public MeetingPart ToPart() => new()
    {
        Title = Title.Trim(),
        Section = Section,
        DurationSeconds = IsSong ? 0 : (int)Math.Round(Math.Max(0, Minutes) * 60),
        IsSong = IsSong,
        HasCounsel = HasCounsel && !IsSong,
        Detail = string.IsNullOrWhiteSpace(Detail) ? null : Detail.Trim(),
    };
}

public sealed class EditorViewModel : ObservableObject
{
    readonly MainViewModel _main;
    readonly MeetingKind _kind;

    public ObservableCollection<EditablePart> Parts { get; } = [];

    public IReadOnlyList<SectionOption> Sections { get; } =
        Enum.GetValues<PartSection>().Select(s => new SectionOption(s, SectionInfo.Label(s))).ToList();

    public IReadOnlyList<int> QuickMinutes { get; } = [1, 2, 3, 4, 5, 6, 10, 15, 30, 60];

    public string MeetingTitle { get; set => Set(ref field, value); }

    public EditablePart? Selected
    {
        get;
        set { if (Set(ref field, value)) OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => Selected is not null;
    public string Heading { get; }
    public string WeekText { get; }

    public EditorViewModel(MainViewModel main)
    {
        _main = main;
        _kind = main.Kind;
        var m = main.CurrentMeeting;
        MeetingTitle = m.Title;
        Heading = _kind == MeetingKind.Midweek ? "Adunanza infrasettimanale" : "Adunanza del fine settimana";
        WeekText = main.WeekTitle;
        Parts.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (EditablePart p in e.NewItems) p.PropertyChanged += OnPartChanged;
            if (e.OldItems is not null)
                foreach (EditablePart p in e.OldItems) p.PropertyChanged -= OnPartChanged;
            NotifyTotal();
        };
        Load(m);
    }

    void OnPartChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditablePart.Minutes) or nameof(EditablePart.IsSong)) NotifyTotal();
    }

    void Load(Meeting m)
    {
        Parts.Clear();
        foreach (var p in m.Parts) Parts.Add(EditablePart.From(p));
        Selected = Parts.FirstOrDefault(p => !p.IsSong) ?? Parts.FirstOrDefault();
    }

    public string TotalText
    {
        get
        {
            double total = Parts.Where(p => !p.IsSong).Sum(p => p.Minutes);
            int songs = Parts.Count(p => p.IsSong);
            return $"{Parts.Count(p => !p.IsSong)} parti · {total:0.#} min" + (songs > 0 ? $" · {songs} cantici" : "");
        }
    }

    void NotifyTotal() => OnPropertyChanged(nameof(TotalText));

    // ── comandi ──

    public ICommand AddPartCommand => field ??= new RelayCommand(() =>
        Insert(new EditablePart { Title = "Nuova parte", Section = Selected?.Section ?? PartSection.Other, Minutes = 5 }));

    public ICommand AddSongCommand => field ??= new RelayCommand(() =>
        Insert(new EditablePart { Title = "Cantico", Section = Selected?.Section ?? PartSection.Other, IsSong = true }));

    public ICommand DuplicateCommand => field ??= new RelayCommand(() => { if (Selected is not null) Insert(Selected.Copy()); });

    public ICommand RemoveCommand => field ??= new RelayCommand(Remove);
    public ICommand MoveUpCommand => field ??= new RelayCommand(() => Move(-1));
    public ICommand MoveDownCommand => field ??= new RelayCommand(() => Move(+1));
    public ICommand IncreaseCommand => field ??= new RelayCommand(() => { if (Selected is { } p) p.Minutes = Math.Floor(p.Minutes) + 1; });
    public ICommand DecreaseCommand => field ??= new RelayCommand(() => { if (Selected is { } p) p.Minutes = Math.Max(1, Math.Ceiling(p.Minutes) - 1); });
    public ICommand SetMinutesCommand => field ??= new RelayCommand(v => { if (Selected is { } p && v is int m) { p.Minutes = m; p.IsSong = false; } });
    public ICommand RestoreDownloadedCommand => field ??= new RelayCommand(RestoreDownloaded);
    public ICommand UseTemplateCommand => field ??= new RelayCommand(UseTemplate);

    void Insert(EditablePart p)
    {
        int at = Selected is null ? Parts.Count : Parts.IndexOf(Selected) + 1;
        Parts.Insert(at, p);
        Selected = p;
    }

    void Remove()
    {
        if (Selected is null) return;
        int i = Parts.IndexOf(Selected);
        Parts.RemoveAt(i);
        Selected = Parts.Count == 0 ? null : Parts[Math.Min(i, Parts.Count - 1)];
    }

    void Move(int dir)
    {
        if (Selected is null) return;
        int i = Parts.IndexOf(Selected), j = i + dir;
        if (j < 0 || j >= Parts.Count) return;
        MoveItem(i, j);
    }

    /// <summary>Sposta una parte (usato anche dal trascinamento).</summary>
    public void MoveItem(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= Parts.Count || to >= Parts.Count) return;
        var item = Parts[from];
        Parts.Move(from, to);
        Selected = item;
    }

    void RestoreDownloaded()
    {
        var week = _main.Week;
        var m = (_kind == MeetingKind.Midweek ? week.DownloadedMidweek : week.DownloadedWeekend)?.Clone()
                ?? (_kind == MeetingKind.Midweek ? MeetingTemplates.DefaultMidweek() : MeetingTemplates.DefaultWeekend());
        if (week.CircuitOverseerVisit) m = MeetingTemplates.ApplyOverseerVisit(m);
        MeetingTitle = m.Title;
        Load(m);
    }

    void UseTemplate()
    {
        var m = _kind == MeetingKind.Midweek ? MeetingTemplates.DefaultMidweek() : MeetingTemplates.DefaultWeekend();
        MeetingTitle = m.Title;
        Load(m);
    }

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
