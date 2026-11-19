namespace TimerSala.Core.Timing;

/// <summary>Quando Zoom muta tutti e condivide l'audio della sala (la musica d'attesa), una volta per adunanza.</summary>
public static class WaitingMusic
{
    /// <summary>Dopo l'invito ad accomodarsi (di solito 30 secondi prima) condividere l'audio non ha più senso.</summary>
    public static readonly TimeSpan TooLate = TimeSpan.FromSeconds(30);

    /// <param name="left">Tempo che manca all'inizio.</param>
    /// <param name="minutes">Minuti prima dell'inizio scelti nelle impostazioni (0 = mai).</param>
    /// <param name="done">Già partita per questa adunanza.</param>
    public static bool Due(TimeSpan left, int minutes, bool done) =>
        !done && minutes > 0 && left > TooLate && left <= TimeSpan.FromMinutes(minutes);
}
