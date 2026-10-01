using System.Text.Json.Serialization;

namespace TimerSala.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<PartSection>))]
public enum PartSection
{
    Opening,
    Treasures,
    Ministry,
    Living,
    PublicTalk,
    Watchtower,
    Closing,
    Other,
}

[JsonConverter(typeof(JsonStringEnumConverter<MeetingKind>))]
public enum MeetingKind
{
    Midweek,
    Weekend,
}

public sealed class MeetingPart
{
    public string Title { get; set; } = "";

    public PartSection Section { get; set; } = PartSection.Other;

    /// <summary>Tempo assegnato in secondi. 0 per i cantici (non cronometrati).</summary>
    public int DurationSeconds { get; set; }

    public bool IsSong { get; set; }

    /// <summary>Dopo la parte il presidente dà un consiglio (parti degli studenti).</summary>
    public bool HasCounsel { get; set; }

    /// <summary>Informazioni aggiuntive (es. scrittura, lezione).</summary>
    public string? Detail { get; set; }

    [JsonIgnore]
    public bool IsTimed => !IsSong && DurationSeconds > 0;

    public MeetingPart Clone() => (MeetingPart)MemberwiseClone();

    public override string ToString() => $"{Title} ({DurationSeconds / 60} min)";
}

public sealed class Meeting
{
    public MeetingKind Kind { get; set; }

    public string Title { get; set; } = "";

    public List<MeetingPart> Parts { get; set; } = [];

    [JsonIgnore]
    public int TotalTimedSeconds => Parts.Where(p => p.IsTimed).Sum(p => p.DurationSeconds);

    public Meeting Clone() => new()
    {
        Kind = Kind,
        Title = Title,
        Parts = Parts.Select(p => p.Clone()).ToList(),
    };
}

public sealed class WeekSchedule
{
    /// <summary>Lunedì della settimana.</summary>
    public DateOnly WeekStart { get; set; }

    /// <summary>Etichetta della settimana come sul sito (es. "5-11 OTTOBRE").</summary>
    public string? WeekLabel { get; set; }

    public string? BibleReading { get; set; }

    public Meeting Midweek { get; set; } = new() { Kind = MeetingKind.Midweek };

    public Meeting Weekend { get; set; } = new() { Kind = MeetingKind.Weekend };

    public bool CircuitOverseerVisit { get; set; }

    /// <summary>Copia originale scaricata (prima di modifiche manuali o della visita del sorvegliante).</summary>
    public Meeting? DownloadedMidweek { get; set; }

    public Meeting? DownloadedWeekend { get; set; }

    public string? SourceUrl { get; set; }

    public DateTimeOffset? FetchedAt { get; set; }

    public bool EditedManually { get; set; }

    public Meeting Get(MeetingKind kind) => kind == MeetingKind.Midweek ? Midweek : Weekend;

    public void Set(MeetingKind kind, Meeting meeting)
    {
        if (kind == MeetingKind.Midweek) Midweek = meeting; else Weekend = meeting;
    }
}
