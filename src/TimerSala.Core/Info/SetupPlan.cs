namespace TimerSala.Core.Info;

/// <summary>Cosa propone l'installer, confrontando questo file con la copia già installata.</summary>
public enum SetupKind { Install, Upgrade, Reinstall, Downgrade }

public static class SetupPlan
{
    public static SetupKind Kind(string? installedVersion, string thisVersion) =>
        installedVersion is null ? SetupKind.Install
        : SemVer.Compare(thisVersion, installedVersion) switch
        {
            > 0 => SetupKind.Upgrade,
            0 => SetupKind.Reinstall,
            _ => SetupKind.Downgrade,
        };

    /// <summary>
    /// L'installer compare aprendo il file scaricato; mai dalla copia installata, mai da una copia che si è scelto
    /// di usare senza installare, mai nelle build di sviluppo.
    /// </summary>
    public static bool Offer(string exePath, string? installFolder, string? portableExe, bool devBuild) =>
        !devBuild
        && !SamePath(Path.GetDirectoryName(exePath), installFolder)
        && !SamePath(exePath, portableExe);

    public static bool SamePath(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        static string Norm(string p) => Path.GetFullPath(p).TrimEnd('\\', '/');
        return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Scegliendo «D:\Programmi» si installa in «D:\Programmi\TimerSala»: mai sparso in una cartella altrui.</summary>
    public static string FolderFor(string picked) =>
        string.Equals(Path.GetFileName(picked.TrimEnd('\\', '/')), "TimerSala", StringComparison.OrdinalIgnoreCase)
            ? picked.TrimEnd('\\', '/')
            : Path.Combine(picked, "TimerSala");
}
