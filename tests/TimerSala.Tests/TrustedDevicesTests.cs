using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using TimerSala.Core.Models;
using TimerSala.Core.Timing;
using TimerSala.Core.Web;

namespace TimerSala.Tests;

public class TrustedDevicesTests
{
    static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    [Fact]
    public async Task After_the_pin_the_device_uses_its_own_code_until_revoked()
    {
        var timer = new MeetingTimer();
        timer.LoadMeeting(MeetingTemplates.DefaultMidweek());
        var devices = new List<TrustedDevice>();
        int saves = 0;
        var registry = new DeviceRegistry(() => devices, () => saves++);
        string pin = "1234";
        await using var server = new TimerWebServer(timer, () => new DisplayOptions(true, true, false), null,
            () => new RemoteConfig(true, pin, []), (a, _) => { if (a == RemoteActions.Toggle) timer.Toggle(); return Task.CompletedTask; })
        { Devices = registry };
        int port = FreePort();
        await server.StartAsync(port, localOnly: true);
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile/15E148 Safari/604.1");

        // il PIN giusto dà un codice al dispositivo
        var check = await http.PostAsJsonAsync("/api/control", new { pin, action = "check" });
        using var body = JsonDocument.Parse(await check.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("token").GetString()!;
        Assert.Single(devices);
        Assert.Equal("iPhone · Safari", devices[0].Name);
        Assert.NotEqual(token, devices[0].TokenHash); // sul PC resta solo l'impronta

        // con il codice niente PIN, anche se il PIN cambia
        pin = "9999";
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/control", new { token, action = RemoteActions.Toggle })).StatusCode);
        Assert.True(timer.IsRunning);

        // revocato: il codice non vale più e la pagina deve chiedere il PIN
        registry.Revoke(devices[0].Id);
        var denied = await http.PostAsJsonAsync("/api/control", new { token, action = RemoteActions.Toggle });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("\"auth\"", await denied.Content.ReadAsStringAsync());
        Assert.True(timer.IsRunning);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/129.0 Mobile Safari/537.36", "Telefono Android · Chrome")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Version/17.0 Mobile/15E148 Safari/604.1", "iPad · Safari")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/129.0 Safari/537.36 Edg/129.0", "PC Windows · Edge")]
    [InlineData(null, "Dispositivo")]
    public void Device_names_are_readable(string? ua, string expected) =>
        Assert.Equal(expected, DeviceRegistry.NameFromUserAgent(ua));
}

public class PrintCardTests
{
    [Fact]
    public void Card_has_the_view_address_and_never_the_pin()
    {
        var html = TimerSala.Core.Web.PrintCard.Html("http://192.168.1.23:8090");
        Assert.Contains("192.168.1.23:8090", html);
        Assert.Contains("data:image/png;base64,", html);
        Assert.DoesNotContain("pin=", html);
        Assert.Equal(2, html.Split("class=\"card\"").Length - 1); // due copie da ritagliare
    }
}
