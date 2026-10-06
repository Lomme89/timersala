using TimerSala.Core.Models;

namespace TimerSala.Core.Timing;

/// <summary>Tipi di parte con un avviso giallo proprio.</summary>
public enum PartCategory { Student, Talk, Other }

/// <summary>Quando diventare gialli: secondi fissi prima della fine, oppure una percentuale della parte.</summary>
public sealed record PartWarning(int Value, bool Percent = false)
{
    /// <summary>Secondi rimanenti a cui scatta il giallo, per una parte che dura <paramref name="targetSeconds"/>.</summary>
    public int SecondsFor(int targetSeconds) => Percent ? (int)Math.Round(targetSeconds * Math.Clamp(Value, 0, 100) / 100.0) : Value;
}

public static class PartCategories
{
    /// <summary>
    /// Studenti: le parti con il consiglio. Discorsi: il discorso pubblico e le parti che si chiamano «discorso»
    /// (anche quello del sorvegliante). Il resto è «altre parti».
    /// </summary>
    public static PartCategory Classify(MeetingPart part) =>
        part.HasCounsel ? PartCategory.Student
        : part.Section == PartSection.PublicTalk || part.Title.Contains("discorso", StringComparison.OrdinalIgnoreCase) ? PartCategory.Talk
        : PartCategory.Other;
}
