using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TimerSala.Core.Web;

/// <summary>Un indirizzo IPv4 locale con la scheda di rete a cui appartiene.</summary>
public sealed record LocalAddress(string Ip, string Adapter, bool IsPreferred, bool IsVirtual)
{
    public override string ToString() => $"{Ip} — {Adapter}";
}

/// <summary>Individua l'indirizzo con cui il PC è raggiungibile nella rete locale.</summary>
public static class NetworkInfo
{
    static readonly string[] VirtualHints =
        ["virtualbox", "vmware", "hyper-v", "vethernet", "wsl", "docker", "loopback", "bluetooth", "tap-", "tun", "vpn", "zerotier", "tailscale", "hamachi", "npcap"];

    /// <summary>
    /// Indirizzi IPv4 locali, dal più probabile al meno probabile: prima quello che Windows usa
    /// per uscire in rete (scheda con gateway predefinito), poi le altre schede fisiche, infine quelle virtuali.
    /// </summary>
    public static IReadOnlyList<LocalAddress> Addresses()
    {
        string? routed = RoutedAddress();
        var list = new List<(LocalAddress Addr, int Rank)>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var props = ni.GetIPProperties();
                bool hasGateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                string label = $"{ni.Name} {ni.Description}".ToLowerInvariant();
                bool isVirtual = VirtualHints.Any(label.Contains);
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = ua.Address.ToString();
                    if (ip.StartsWith("169.254.") || ip.StartsWith("127.")) continue;
                    // una VPN o una scheda virtuale non è mai preferita a una scheda fisica
                    bool preferred = ip == routed && !isVirtual;
                    int rank = preferred ? 0 : hasGateway && !isVirtual ? 1 : !isVirtual ? 2 : 3;
                    list.Add((new LocalAddress(ip, ni.Name, preferred, isVirtual), rank));
                }
            }
        }
        catch (NetworkInformationException) { }
        var ordered = list.OrderBy(x => x.Rank).Select(x => x.Addr).DistinctBy(a => a.Ip).ToList();
        // segna come preferito il primo dell'elenco anche se il percorso verso internet non è disponibile
        if (ordered.Count > 0 && !ordered[0].IsPreferred && !ordered[0].IsVirtual)
            ordered[0] = ordered[0] with { IsPreferred = true };
        return ordered;
    }

    /// <summary>
    /// L'indirizzo che il sistema sceglierebbe per raggiungere internet (nessun pacchetto viene inviato).
    /// Null se non c'è una rete con gateway.
    /// </summary>
    public static string? RoutedAddress()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 9)); // indirizzo di documentazione: solo calcolo del percorso
            return (s.LocalEndPoint as IPEndPoint)?.Address is { } a && !IPAddress.IsLoopback(a) && !a.Equals(IPAddress.Any) ? a.ToString() : null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    /// <summary>Host da usare nei collegamenti: "auto", "hostname" o un IP scelto.</summary>
    public static string ResolveHost(string? mode)
    {
        if (string.Equals(mode, "hostname", StringComparison.OrdinalIgnoreCase)) return Environment.MachineName;
        var all = Addresses();
        if (!string.IsNullOrEmpty(mode) && !string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase) && all.Any(a => a.Ip == mode))
            return mode;
        return all.FirstOrDefault()?.Ip ?? "localhost";
    }
}
