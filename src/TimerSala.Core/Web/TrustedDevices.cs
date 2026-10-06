using System.Security.Cryptography;
using System.Text;

namespace TimerSala.Core.Web;

/// <summary>Un telefono o tablet autorizzato al controllo: dopo il primo PIN non lo chiede più, finché non viene revocato.</summary>
public sealed class TrustedDevice
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Impronta del codice dato al dispositivo (il codice vero resta solo sul telefono).</summary>
    public string TokenHash { get; set; } = "";

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}

/// <summary>
/// Elenco dei dispositivi fidati. Thread-safe: il server web lo usa dai suoi thread. L'elenco vero è quello delle
/// impostazioni (<paramref name="list"/>), salvato con <paramref name="changed"/>.
/// </summary>
public sealed class DeviceRegistry(Func<List<TrustedDevice>> list, Action changed, TimeProvider? clock = null)
{
    readonly object _lock = new();
    readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public const int MaxDevices = 30;

    /// <summary>Autorizza un nuovo dispositivo e restituisce il suo codice (da conservare sul telefono).</summary>
    public string Register(string name)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var now = _clock.GetUtcNow();
        lock (_lock)
        {
            var devices = list();
            devices.Add(new TrustedDevice
            {
                Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant(),
                Name = name,
                TokenHash = Hash(token),
                Created = now,
                LastSeen = now,
            });
            // i più vecchi (non visti da più tempo) lasciano il posto
            if (devices.Count > MaxDevices)
                devices.Remove(devices.OrderBy(d => d.LastSeen).First());
        }
        changed();
        return token;
    }

    /// <summary>Il dispositivo con questo codice, se è ancora autorizzato.</summary>
    public TrustedDevice? Validate(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var hash = Hash(token);
        bool save = false;
        TrustedDevice? found;
        lock (_lock)
        {
            found = list().FirstOrDefault(d => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(d.TokenHash), Encoding.ASCII.GetBytes(hash)));
            if (found is not null)
            {
                var now = _clock.GetUtcNow();
                // l'ultimo uso si salva al massimo una volta l'ora: niente scritture a ogni comando
                save = now - found.LastSeen > TimeSpan.FromHours(1);
                found.LastSeen = now;
            }
        }
        if (save) changed();
        return found;
    }

    public bool Revoke(string id)
    {
        bool removed;
        lock (_lock) removed = list().RemoveAll(d => d.Id == id) > 0;
        if (removed) changed();
        return removed;
    }

    public void RevokeAll()
    {
        lock (_lock) list().Clear();
        changed();
    }

    static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Nome leggibile dal browser: «iPhone · Safari», «Android · Chrome», «Windows · Edge».</summary>
    public static string NameFromUserAgent(string? ua)
    {
        ua ??= "";
        string device = ua.Contains("iPhone") ? "iPhone"
            : ua.Contains("iPad") || (ua.Contains("Macintosh") && ua.Contains("Mobile")) ? "iPad"
            : ua.Contains("Android") ? (ua.Contains("Mobile") ? "Telefono Android" : "Tablet Android")
            : ua.Contains("Windows") ? "PC Windows"
            : ua.Contains("Macintosh") ? "Mac"
            : ua.Contains("Linux") ? "Linux"
            : "Dispositivo";
        string browser = ua.Contains("Edg/") ? "Edge"
            : ua.Contains("SamsungBrowser") ? "Samsung Internet"
            : ua.Contains("Firefox") || ua.Contains("FxiOS") ? "Firefox"
            : ua.Contains("CriOS") || ua.Contains("Chrome") ? "Chrome"
            : ua.Contains("Safari") ? "Safari"
            : "";
        return browser.Length > 0 ? $"{device} · {browser}" : device;
    }
}
