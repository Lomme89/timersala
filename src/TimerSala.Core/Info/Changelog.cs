using System.Text.RegularExpressions;

namespace TimerSala.Core.Info;

/// <summary>Una versione del CHANGELOG: «v2.1.0-beta.1 — 6 ottobre 2026» e il testo che segue.</summary>
public sealed record ChangelogEntry(string Version, string Heading, string Body);

/// <summary>Legge CHANGELOG.md (lo stesso file da cui nascono le note delle release e la pagina Novità del sito).</summary>
public static partial class Changelog
{
    [GeneratedRegex(@"^## v(?<v>\d+\.\d+\.\d+(?:-[0-9A-Za-z.]+)?)\b.*$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    public static IReadOnlyList<ChangelogEntry> Parse(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        var matches = HeadingRegex().Matches(text);
        var list = new List<ChangelogEntry>();
        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            int start = m.Index + m.Length;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            list.Add(new ChangelogEntry(m.Groups["v"].Value, m.Value[3..].Trim(), text[start..end].Trim()));
        }
        return list;
    }

    /// <summary>
    /// Le novità da mostrare dopo un aggiornamento: le versioni più recenti di <paramref name="lastSeen"/> fino a
    /// <paramref name="current"/> compresa. Senza <paramref name="lastSeen"/> solo quella attuale.
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> Since(IReadOnlyList<ChangelogEntry> entries, string? lastSeen, string current)
    {
        if (lastSeen is null)
            return entries.Where(e => SemVer.Compare(e.Version, current) == 0).ToList();
        return entries.Where(e => SemVer.Compare(e.Version, lastSeen) > 0 && SemVer.Compare(e.Version, current) <= 0).ToList();
    }
}

/// <summary>Confronto di versioni «2.1.0», «2.1.0-beta.2»: un'anteprima viene prima della stabile con lo stesso numero.</summary>
public static class SemVer
{
    public static int Compare(string a, string b)
    {
        static (Version Core, string[] Pre) Split(string v)
        {
            v = v.TrimStart('v', 'V').Split('+')[0];
            int dash = v.IndexOf('-');
            var core = Version.TryParse(dash < 0 ? v : v[..dash], out var parsed) ? parsed : new Version(0, 0, 0);
            return (core, dash < 0 ? [] : v[(dash + 1)..].Split('.'));
        }

        var (ca, pa) = Split(a);
        var (cb, pb) = Split(b);
        int c = ca.CompareTo(cb);
        if (c != 0) return c;
        if (pa.Length == 0 || pb.Length == 0) return pb.Length.CompareTo(pa.Length); // la stabile è più recente
        for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            bool na = int.TryParse(pa[i], out int ia), nb = int.TryParse(pb[i], out int ib);
            c = na && nb ? ia.CompareTo(ib) : string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0) return c;
        }
        return pa.Length.CompareTo(pb.Length);
    }
}
