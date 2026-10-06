using System.Diagnostics;
using System.IO;

namespace TimerSala.App.Views;

/// <summary>
/// Cartoncino da stampare con il codice QR per *seguire* il timer (mai quello con il PIN): una pagina A4 con due copie
/// da ritagliare, aperta nel browser per stamparla o salvarla in PDF.
/// </summary>
public static class PrintCardWindow
{
    /// <summary>Salva il cartoncino in una pagina temporanea e la apre nel browser.</summary>
    public static void Open(string url)
    {
        var path = Path.Combine(Path.GetTempPath(), "TimerSala-cartoncino.html");
        File.WriteAllText(path, Core.Web.PrintCard.Html(url));
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }
}
