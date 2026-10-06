using System.Net;

namespace TimerSala.Core.Web;

/// <summary>
/// Cartoncino da stampare con il codice QR per *seguire* il timer (mai quello con il PIN): una pagina A4 con due copie
/// da ritagliare.
/// </summary>
public static class PrintCard
{
    public static string Html(string url)
    {
        var qr = Convert.ToBase64String(QrCodes.Png(url));
        var shown = WebUtility.HtmlEncode(url.Replace("http://", ""));
        var card = $"""
            <section class="card">
              <h1>Il timer sul tuo telefono</h1>
              <img src="data:image/png;base64,{qr}" alt="Codice QR del timer">
              <div>
                <ol>
                  <li>Collegati al <b>Wi-Fi della sala</b>.</li>
                  <li>Inquadra il codice con la <b>fotocamera</b>, oppure scrivi nel browser:</li>
                </ol>
                <p class="url">{shown}</p>
                <p class="note">Solo per vedere il timer: per comandarlo serve il PIN, che resta all'acustica.</p>
              </div>
            </section>
            """;
        return $$"""
            <!doctype html>
            <html lang="it"><head><meta charset="utf-8"><title>TimerSala · cartoncino</title>
            <style>
              @page { size: A4; margin: 12mm; }
              * { box-sizing: border-box; }
              body { margin: 0; font-family: "Segoe UI", system-ui, sans-serif; color: #141414; background: #f1ece3; }
              .bar { padding: 14px 20px; display: flex; gap: 12px; align-items: center; justify-content: center; }
              .bar button { font: 600 16px "Segoe UI", sans-serif; padding: 10px 22px; border-radius: 999px; border: 0; background: #141414; color: #fff; cursor: pointer; }
              .sheet { width: 186mm; margin: 0 auto; background: #fff; }
              .card { height: 136mm; padding: 10mm 14mm; display: grid; grid-template-columns: 62mm 1fr; column-gap: 10mm; align-content: start; align-items: center; border-bottom: 1px dashed #999; }
              .card h1 { grid-column: 1 / -1; font-size: 26pt; margin: 0 0 6mm; letter-spacing: -.01em; }
              .card img { width: 62mm; height: 62mm; image-rendering: pixelated; }
              .card ol { margin: 0; padding-left: 6mm; font-size: 15pt; line-height: 1.45; }
              .url { font: 700 18pt Consolas, monospace; margin: 4mm 0 0; overflow-wrap: anywhere; }
              .note { color: #555; font-size: 11pt; margin: 4mm 0 0; }
              @media print { body { background: #fff; } .bar { display: none; } .sheet { width: auto; } }
            </style></head>
            <body>
              <div class="bar"><span>Due copie da ritagliare lungo la linea.</span><button onclick="print()">Stampa</button></div>
              <div class="sheet">{{card}}{{card}}</div>
            </body></html>
            """;
    }
}
