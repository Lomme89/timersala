namespace TimerSala.Core.Timing;

/// <summary>Stato del timer salvato su disco per riprendere l'adunanza dopo un riavvio.</summary>
public sealed class TimerState
{
    public int SelectedIndex { get; set; } = -1;
    public Dictionary<int, double> ActualSeconds { get; set; } = [];
    public Dictionary<int, int> AdaptedTargets { get; set; } = [];
    public bool Running { get; set; }
    public int RunningIndex { get; set; } = -1;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public double CarriedSeconds { get; set; }
    public int TargetSeconds { get; set; }
    public DateTimeOffset SavedAt { get; set; }

    /// <summary>C'è qualcosa da ripristinare (una parte in corso o tempi già registrati).</summary>
    public bool HasProgress => Running || ActualSeconds.Count > 0;
}
