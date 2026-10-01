using System.Text.Json.Serialization;

namespace TimerSala.Core.Timing;

[JsonConverter(typeof(JsonStringEnumConverter<TimerPhase>))]
public enum TimerPhase
{
    /// <summary>Nessun timer in corso: il display mostra l'orologio.</summary>
    Idle,
    Normal,
    Warning,
    Overtime,
}

[JsonConverter(typeof(JsonStringEnumConverter<TimerMode>))]
public enum TimerMode
{
    Part,
    Counsel,
    Manual,
    /// <summary>Conto alla rovescia prima dell'inizio dell'adunanza.</summary>
    Countdown,
}

/// <summary>Stato istantaneo del timer, condiviso tra schermo, controller e server web.</summary>
public sealed record TimerSnapshot
{
    public TimerPhase Phase { get; init; }
    public TimerMode Mode { get; init; }
    public bool IsRunning { get; init; }
    public string Title { get; init; } = "";
    public string? Section { get; init; }
    public int TargetSeconds { get; init; }
    public double ElapsedSeconds { get; init; }

    /// <summary>Secondi rimanenti (negativi in sforamento).</summary>
    public double RemainingSeconds => TargetSeconds - ElapsedSeconds;

    public string? NextTitle { get; init; }
    public string MeetingTitle { get; init; } = "";

    /// <summary>Ritardo accumulato sull'adunanza in secondi (positivo = in ritardo).</summary>
    public int DelaySeconds { get; init; }

    public DateTimeOffset Now { get; init; }

    public string Display => FormatRemaining(RemainingSeconds);

    public double Progress => TargetSeconds <= 0 ? 0 : Math.Clamp(ElapsedSeconds / TargetSeconds, 0, 1);

    public static string FormatRemaining(double remaining)
    {
        if (remaining >= 0)
        {
            // arrotonda per eccesso: "0:01" fino all'ultimo istante, poi "+0:00"
            int s = (int)Math.Ceiling(remaining - 0.0001);
            return $"{s / 60:00}:{s % 60:00}";
        }
        int o = (int)Math.Floor(-remaining);
        return $"+{o / 60:00}:{o % 60:00}";
    }

    public static string FormatDuration(double seconds)
    {
        int s = (int)Math.Round(Math.Abs(seconds));
        return $"{(seconds < 0 ? "-" : "")}{s / 60}:{s % 60:00}";
    }
}
