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

    /// <summary>Solo le sezioni pertinenti all'adunanza (più quelle già usate dalle parti).</summary>
    public IReadOnlyList<SectionOption> Sections { get; private set; } = [];

    public IReadOnlyList<int> QuickMinutes { get; }

    public string MeetingTitle { get; set { if (Set(ref field, value)) Track("titolo"); } }

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
        QuickMinutes = _kind == MeetingKind.Midweek ? [1, 3, 4, 5, 10, 15, 30] : [5, 10, 15, 30, 45, 60];
        var m = main.CurrentMeeting;
        MeetingTitle = m.Title;
        Heading = _kind == MeetingKind.Midweek ? "Adunanza infrasettimanale" : "Adunanza del fine settimana";
        WeekText = main.WeekTitle;
        _length = main.Settings.MeetingLengthMinutes;
        _start = main.Timer.MeetingStart;
        Parts.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (EditablePart p in e.NewItems) p.PropertyChanged += OnPartChanged;
            if (e.OldItems is not null)
                foreach (EditablePart p in e.OldItems) p.PropertyChanged -= OnPartChanged;
            NotifyTotal();
            Track(null);
        };
        _suspend++;
        Load(m);
        _suspend--;
        _baseline = Capture();
    }

    void OnPartChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditablePart.Minutes) or nameof(EditablePart.IsSong)) NotifyTotal();
        // le modifiche di fila allo stesso campo (per esempio mentre si scrive) diventano un solo passo da annullare
        if (e.PropertyName is nameof(EditablePart.Title) or nameof(EditablePart.Section) or nameof(EditablePart.Minutes)
            or nameof(EditablePart.IsSong) or nameof(EditablePart.Detail))
            Track($"{sender?.GetHashCode()}:{e.PropertyName}");
    }

    // ── annulla e ripeti ──

    sealed record State(string Title, List<EditablePart> Parts, int Selected);

    readonly Stack<State> _undo = new(), _redo = new();
    State? _baseline;
    string? _lastKey;
    DateTime _lastAt;
    int _suspend;

    State Capture() => new(MeetingTitle, Parts.Select(p => p.Copy()).ToList(), Selected is null ? -1 : Parts.IndexOf(Selected));

    /// <summary>Lo schema è cambiato: lo stato precedente diventa un passo da annullare.</summary>
    void Track(string? key)
    {
        if (_suspend > 0 || _baseline is null) return;
        var now = DateTime.UtcNow;
        bool merge = key is not null && key == _lastKey && now - _lastAt < TimeSpan.FromSeconds(1.5);
        if (!merge)
        {
            _undo.Push(_baseline);
            _redo.Clear();
        }
        _lastKey = key;
        _lastAt = now;
        _baseline = Capture();
        NotifyUndo();
    }

    void Restore(State s)
    {
        _suspend++;
        MeetingTitle = s.Title;
        Parts.Clear();
        foreach (var p in s.Parts) Parts.Add(p.Copy());
        Selected = s.Selected >= 0 && s.Selected < Parts.Count ? Parts[s.Selected] : Parts.FirstOrDefault();
        _suspend--;
        _baseline = Capture();
        _lastKey = null;
        NotifyUndo();
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ICommand UndoCommand => field ??= new RelayCommand(Undo);
    public ICommand RedoCommand => field ??= new RelayCommand(Redo);

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(Capture());
        Restore(_undo.Pop());
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(Capture());
        Restore(_redo.Pop());
    }

    void NotifyUndo()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    // ── sforamento ──

    readonly int _length;
    readonly DateTimeOffset? _start;

    Meeting Draft() => new() { Kind = _kind, Title = MeetingTitle, Parts = Parts.Select(p => p.ToPart()).ToList() };

    /// <summary>«Lo schema dura circa 108 minuti: 3 minuti oltre i 105, finirà verso le 20:48».</summary>
    public string OverrunText
    {
        get
        {
            var m = Draft();
            int over = MeetingPlan.OverrunMinutes(m, _length);
            if (over == 0) return "";
            int total = _length + over;
            var end = _start is { } s ? $", finirà verso le {s.AddMinutes(total):HH:mm}" : "";
            return $"Lo schema dura circa {total} minuti, contando {MeetingPlan.SongMinutes} minuti per ogni cantico: "
                   + $"{over} {(over == 1 ? "minuto" : "minuti")} oltre i {_length}{end}.";
        }
    }

    public bool HasOverrun => OverrunText.Length > 0;

    void Load(Meeting m)
    {
        PartSection[] relevant = _kind == MeetingKind.Midweek
            ? [PartSection.Opening, PartSection.Treasures, PartSection.Ministry, PartSection.Living, PartSection.Closing, PartSection.Other]
            : [PartSection.Opening, PartSection.PublicTalk, PartSection.Watchtower, PartSection.Closing, PartSection.Other];
        Sections = Enum.GetValues<PartSection>()
            .Where(s => relevant.Contains(s) || m.Parts.Any(p => p.Section == s))
            .Select(s => new SectionOption(s, SectionInfo.Label(s))).ToList();
        OnPropertyChanged(nameof(Sections));
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

    void NotifyTotal()
    {
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(OverrunText));
        OnPropertyChanged(nameof(HasOverrun));
    }

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
    public ICommand SetTimedCommand => field ??= new RelayCommand(() =>
    {
        if (Selected is not { } p) return;
        p.IsSong = false;
        if (p.Minutes <= 0) p.Minutes = 5;
    });

    public ICommand SetSongCommand => field ??= new RelayCommand(() => { if (Selected is { } p) p.IsSong = true; });

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

    void RestoreDownloaded() => Batch(RestoreDownloadedCore);

    void UseTemplate() => Batch(UseTemplateCore);

    /// <summary>Un'operazione con più modifiche diventa un solo passo da annullare.</summary>
    void Batch(Action action)
    {
        var before = Capture();
        _suspend++;
        try { action(); }
        finally { _suspend--; }
        _undo.Push(before);
        _redo.Clear();
        _baseline = Capture();
        _lastKey = null;
        NotifyUndo();
    }

    void RestoreDownloadedCore()
    {
        var week = _main.Week;
        var m = (_kind == MeetingKind.Midweek ? week.DownloadedMidweek : week.DownloadedWeekend)?.Clone()
                ?? (_kind == MeetingKind.Midweek ? MeetingTemplates.DefaultMidweek() : MeetingTemplates.DefaultWeekend());
        if (week.CircuitOverseerVisit) m = MeetingTemplates.ApplyOverseerVisit(m);
        MeetingTitle = m.Title;
        Load(m);
    }

    void UseTemplateCore()
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
