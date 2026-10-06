using TimerSala.Core.Info;
using TimerSala.Core.Storage;

namespace TimerSala.Tests;

public class ProblemReportTests
{
    [Fact]
    public void Report_never_contains_the_pin_and_stays_short()
    {
        var s = new AppSettings { RemotePin = "48213" };
        var log = string.Concat(Enumerable.Repeat("System.Exception: qualcosa è andato storto in TimerSala\n   at Riga.Lunga()\n", 500));
        var url = ProblemReport.BuildUrl(true, "2.1.0", "Windows 11", s, log);

        var body = Uri.UnescapeDataString(url);
        Assert.StartsWith(ProblemReport.IssuesUrl, url);
        Assert.DoesNotContain("48213", body);
        Assert.Contains("Versione: 2.1.0", body);
        Assert.Contains("Errori recenti", body);
        Assert.True(url.Length <= 8000, $"indirizzo lungo {url.Length}");
    }

    [Fact]
    public void An_idea_has_no_error_log()
    {
        var url = ProblemReport.BuildUrl(false, "2.1.0", "Windows 11", new AppSettings(), "errore");
        Assert.DoesNotContain("Errori", Uri.UnescapeDataString(url));
        Assert.Contains("labels=enhancement", url);
    }
}
