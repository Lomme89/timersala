using QRCoder;

namespace TimerSala.Core.Web;

public static class QrCodes
{
    /// <summary>Immagine PNG del codice QR per il testo indicato.</summary>
    public static byte[] Png(string text, int pixelsPerModule = 10)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule, [0, 0, 0], [255, 255, 255], drawQuietZones: true);
    }
}
