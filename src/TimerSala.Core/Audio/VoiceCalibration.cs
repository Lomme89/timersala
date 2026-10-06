namespace TimerSala.Core.Audio;

/// <summary>Esito della taratura: la soglia proposta (o null) e una frase da mostrare.</summary>
public sealed record CalibrationResult(double? ThresholdDb, string Message);

/// <summary>
/// Taratura dell'avvio con la voce: qualche secondo di sala in silenzio, poi qualche secondo di voce al microfono.
/// La soglia va a metà strada tra il rumore (i momenti più forti del silenzio) e la voce (il suo livello tipico),
/// con almeno 6 dB di margine da entrambi.
/// </summary>
public static class VoiceCalibration
{
    public const double Margin = 6;

    public static CalibrationResult Compute(IReadOnlyList<double> silenceDb, IReadOnlyList<double> voiceDb)
    {
        if (silenceDb.Count < 10 || voiceDb.Count < 10)
            return new(null, "Non è arrivato audio dall'ingresso scelto: controlla il cavo o l'ingresso.");

        double noise = Percentile(silenceDb, 0.9);
        // della voce contano i momenti in cui si parla davvero, non le pause tra le parole
        var spoken = voiceDb.Where(d => d > noise + 3).ToList();
        if (spoken.Count < voiceDb.Count / 4)
            return new(null, "La voce non si distingue dal silenzio: avvicinati al microfono o alza il volume dell'ingresso, poi riprova.");
        double voice = Percentile(spoken, 0.5);
        if (voice - noise < 2 * Margin)
            return new(null, $"Voce e silenzio sono troppo vicini ({voice - noise:0} dB): l'avvio con la voce non sarebbe affidabile. Alza il volume dell'ingresso o abbassa il rumore.");

        double threshold = Math.Round(Math.Clamp((noise + voice) / 2, noise + Margin, voice - Margin));
        return new(threshold, $"Soglia impostata a {threshold:0} dB (silenzio fino a {noise:0} dB, voce intorno a {voice:0} dB).");
    }

    static double Percentile(IReadOnlyList<double> values, double p)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[(int)Math.Clamp(Math.Round(p * (sorted.Count - 1)), 0, sorted.Count - 1)];
    }
}
