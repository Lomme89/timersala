using System.Net.Sockets;
using System.Text.Json;
using TimerSala.Core.Models;
using TimerSala.Core.Timing;
using TimerSala.Core.Web;

namespace TimerSala.Tests;

public class WebServerTests
{
    static int FreePort()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        int p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    [Fact]
    public async Task Serves_page_state_schedule_and_events()
    {
        var timer = new MeetingTimer();
        timer.LoadMeeting(MeetingTemplates.DefaultMidweek());
        timer.Select(1);
        timer.Start();

        await using var server = new TimerWebServer(timer, () => new DisplayOptions(true, true, false));
        int port = FreePort();
        await server.StartAsync(port, localOnly: true);
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        var page = await http.GetStringAsync("/");
        Assert.Contains("EventSource", page);

        using var state = JsonDocument.Parse(await http.GetStringAsync("/api/state"));
        Assert.Equal("normal", state.RootElement.GetProperty("phase").GetString());
        Assert.Equal("1. Discorso", state.RootElement.GetProperty("title").GetString());
        Assert.Equal("Tesori della Parola di Dio", state.RootElement.GetProperty("sectionLabel").GetString());

        using var schedule = JsonDocument.Parse(await http.GetStringAsync("/api/schedule"));
        Assert.True(schedule.RootElement.GetProperty("parts")[1].GetProperty("running").GetBoolean());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var resp = await http.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        using var reader = new StreamReader(await resp.Content.ReadAsStreamAsync(cts.Token));
        var line = await reader.ReadLineAsync(cts.Token);
        Assert.StartsWith("data: {", line);
    }
}
