namespace TimerSala.Core.Wol;

/// <summary>Parametri della Biblioteca online (wol.jw.org) per una lingua.</summary>
public sealed record WolLanguage(string Name, string Code, string Rsconf, string Lib)
{
    public static readonly IReadOnlyList<WolLanguage> Presets =
    [
        new("Italiano", "it", "r6", "lp-i"),
        new("English", "en", "r1", "lp-e"),
        new("Español", "es", "r4", "lp-s"),
        new("Français", "fr", "r30", "lp-f"),
        new("Deutsch", "de", "r10", "lp-x"),
        new("Português (Brasil)", "pt", "r5", "lp-t"),
    ];

    public static WolLanguage Italian => Presets[0];

    public string MeetingsUrl(int isoYear, int isoWeek) =>
        $"https://wol.jw.org/{Code}/wol/meetings/{Rsconf}/{Lib}/{isoYear}/{isoWeek}";

    public string DailyUrl(DateOnly date) =>
        $"https://wol.jw.org/{Code}/wol/dt/{Rsconf}/{Lib}/{date.Year}/{date.Month}/{date.Day}";

    public override string ToString() => Name;
}
