namespace TimerSala.Core.Models;

/// <summary>Adunanza per la modalità addestramento: le stesse parti, con durate ridotte a un decimo.</summary>
public static class TrainingMeeting
{
    public const int SpeedUp = 10;

    /// <summary>Copia dell'adunanza con le parti cronometrate dieci volte più brevi (minimo 15 secondi, a passi di 5).</summary>
    public static Meeting From(Meeting source)
    {
        var m = source.Clone();
        m.Title = "Prova · " + source.Title;
        foreach (var p in m.Parts.Where(p => p.IsTimed && p.DurationSeconds > 0))
            p.DurationSeconds = Math.Max(15, (int)Math.Round(p.DurationSeconds / (double)SpeedUp / 5) * 5);
        return m;
    }
}
