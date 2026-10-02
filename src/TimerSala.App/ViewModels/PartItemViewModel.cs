using System.Windows.Media;
using TimerSala.Core.Models;

using System.Windows.Input;

namespace TimerSala.App.ViewModels;

public sealed partial class PartItemViewModel(MeetingPart part, int index) : ObservableObject
{
    public MeetingPart Part { get; } = part;
    public int Index { get; } = index;

    public string Title => Part.Title;
    public string? Detail => Part.Detail;
    public bool HasDetail => !string.IsNullOrWhiteSpace(Part.Detail);
    public bool IsSong => Part.IsSong;
    public bool IsTimed => Part.IsTimed;
    public bool HasCounsel => Part.HasCounsel;
    public Brush SectionBrush => BrushCache.Get(SectionInfo.Color(Part.Section));

    public string DurationText => Part.IsSong ? "" : AdaptedSeconds is { } a ? FormatMinutes(a) : FormatMinutes(Part.DurationSeconds);

    /// <summary>Durata adattata (studio adattivo), se diversa da quella prevista.</summary>
    public int? AdaptedSeconds
    {
        get;
        set { if (Set(ref field, value)) { OnPropertyChanged(nameof(DurationText)); OnPropertyChanged(nameof(IsAdapted)); } }
    }

    public bool IsAdapted => AdaptedSeconds is not null;

    public string? ActualText { get; set => Set(ref field, value); }
    public bool IsOver { get; set => Set(ref field, value); }
    public bool IsSelected { get; set => Set(ref field, value); }
    public bool IsRunning { get; set => Set(ref field, value); }

    public void RefreshDuration() => OnPropertyChanged(nameof(DurationText));

    static string FormatMinutes(int seconds) =>
        seconds % 60 == 0 ? $"{seconds / 60} min" : $"{seconds / 60}:{seconds % 60:00}";
}

static class BrushCache
{
    static readonly Dictionary<string, SolidColorBrush> Cache = [];

    public static SolidColorBrush Get(string hex)
    {
        if (!Cache.TryGetValue(hex, out var b))
        {
            b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            Cache[hex] = b;
        }
        return b;
    }
}
