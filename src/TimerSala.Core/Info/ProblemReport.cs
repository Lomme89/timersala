using System.Text;
using TimerSala.Core.Storage;

namespace TimerSala.Core.Info;

/// <summary>Segnalazione su GitHub già compilata: versione, sistema, impostazioni principali (mai il PIN) ed errori recenti.</summary>
public static class ProblemReport
{
    public const string IssuesUrl = "https://github.com/Lomme89/timersala/issues/new";

    // i browser e GitHub accettano indirizzi lunghi, ma non troppo
    const int MaxUrlLength = 7500;

    public static string BuildUrl(bool isProblem, string appVersion, string system, AppSettings settings, string? errorLog)
    {
        var body = new StringBuilder();
        if (isProblem)
        {
            body.AppendLine("### Cosa è successo").AppendLine().AppendLine("(descrivi cosa stavi facendo e cosa non ha funzionato)").AppendLine();
            body.AppendLine("### Cosa ti aspettavi").AppendLine().AppendLine().AppendLine();
        }
        else
        {
            body.AppendLine("### L'idea").AppendLine().AppendLine("(cosa vorresti e in quale situazione in sala servirebbe)").AppendLine();
        }

        body.AppendLine("### Dati del programma").AppendLine();
        body.AppendLine($"- Versione: {(appVersion is "" ? "sviluppo" : appVersion)}");
        body.AppendLine($"- Sistema: {system}");
        if (isProblem)
        {
            body.AppendLine($"- Schermo della sala: {(settings.TimerWindowVisible ? "attivo" : "spento")}, layout {settings.DisplayLayout}, countdown {settings.CountdownStyle}");
            body.AppendLine($"- Timer in rete: {(settings.WebServerEnabled ? "attivo" : "spento")}, controllo remoto {(settings.RemoteControlEnabled ? "attivo" : "spento")}");
            body.AppendLine($"- Avvio con la voce: {(settings.VoiceStartEnabled ? "attivo" : "spento")}");
            body.AppendLine($"- Lingua dello schema: {settings.WolCode}");
        }
        string head = body.ToString();

        string errors = "";
        if (isProblem && !string.IsNullOrWhiteSpace(errorLog))
        {
            // solo la parte finale del registro, quanta ne sta nell'indirizzo
            var log = errorLog.Replace("\r\n", "\n").Trim();
            int budget = Math.Max(0, (MaxUrlLength - Escape(head).Length - 600) / 3);
            if (log.Length > budget) log = "…" + log[^budget..];
            if (log.Length > 1)
                errors = "\n### Errori recenti\n\n(puoi togliere quello che non vuoi pubblicare)\n\n```\n" + log + "\n```\n";
        }

        var title = isProblem ? "Problema: " : "Idea: ";
        var labels = isProblem ? "bug" : "enhancement";
        return $"{IssuesUrl}?labels={labels}&title={Escape(title)}&body={Escape(head + errors)}";
    }

    static string Escape(string s) => Uri.EscapeDataString(s);
}
