namespace TimerSala.Core.Timing;

/// <summary>Quando partono musica d'attesa e condivisione dell'audio in Zoom, una volta per adunanza.</summary>
public static class WaitingMusic
{
    /// <summary>Dopo l'invito ad accomodarsi (di solito 30 secondi prima) far partire la musica non ha più senso.</summary>
    public static readonly TimeSpan TooLate = TimeSpan.FromSeconds(30);

    /// <param name="left">Tempo che manca all'inizio.</param>
    /// <param name="minutes">Minuti prima dell'inizio scelti nelle impostazioni (0 = mai).</param>
    /// <param name="done">Già partita per questa adunanza.</param>
    public static bool Due(TimeSpan left, int minutes, bool done) =>
        !done && minutes > 0 && left > TooLate && left <= TimeSpan.FromMinutes(minutes);
}
