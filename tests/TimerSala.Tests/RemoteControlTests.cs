using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using TimerSala.Core.Models;
using TimerSala.Core.Timing;
using TimerSala.Core.Web;

namespace TimerSala.Tests;

public class RemoteControlTests
{
    static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    sealed class Fixture : IAsyncDisposable
    {
        public MeetingTimer Timer { get; } = new();
        public MessageBoard Messages { get; } = new();
        public List<(string Action, string? Value)> Received { get; } = [];
        public TimerWebServer Server { get; }
        public HttpClient Http { get; }

        public Fixture(bool enabled = true)
        {
            Timer.LoadMeeting(MeetingTemplates.DefaultMidweek());
            Server = new TimerWebServer(Timer, () => new DisplayOptions(true, true, false), Messages,
                () => new RemoteConfig(enabled, "1234", ["Concludi"]),
                (a, v) =>
                {
                    Received.Add((a, v));
                    if (a == RemoteActions.Toggle) Timer.Toggle();
                    if (a == RemoteActions.Message) Messages.Show(v!, null);
                    return Task.CompletedTask;
                });
            int port = FreePort();
            Server.StartAsync(port, localOnly: true).GetAwaiter().GetResult();
            Http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        }

        public Task<HttpResponseMessage> Send(string pin, string action, string? value = null) =>
            Http.PostAsJsonAsync("/api/control", new { pin, action, value });

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await Server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Correct_pin_runs_commands_and_message_appears_in_state()
    {
        await using var f = new Fixture();
        Assert.Equal(HttpStatusCode.OK, (await f.Send("1234", "check")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Send("1234", RemoteActions.Toggle)).StatusCode);
        Assert.True(f.Timer.IsRunning);

        await f.Send("1234", RemoteActions.Message, "Concludi");
        using var state = JsonDocument.Parse(await f.Http.GetStringAsync("/api/state"));
        Assert.Equal("Concludi", state.RootElement.GetProperty("message").GetString());

        using var config = JsonDocument.Parse(await f.Http.GetStringAsync("/api/config"));
        Assert.True(config.RootElement.GetProperty("control").GetBoolean());
        Assert.Equal("Concludi", config.RootElement.GetProperty("presets")[0].GetString());
    }

    [Fact]
    public async Task Wrong_pin_is_rejected_and_locks_after_too_many_attempts()
    {
        await using var f = new Fixture();
        for (int i = 0; i < TimerWebServer.MaxPinFailures - 1; i++)
            Assert.Equal(HttpStatusCode.Forbidden, (await f.Send("0000", RemoteActions.Toggle)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Send("0000", RemoteActions.Toggle)).StatusCode);

        // bloccato: anche il PIN giusto viene rifiutato per un minuto
        Assert.Equal(HttpStatusCode.TooManyRequests, (await f.Send("1234", RemoteActions.Toggle)).StatusCode);
        Assert.Empty(f.Received);
        Assert.False(f.Timer.IsRunning);
    }

    [Fact]
    public async Task Disabled_control_and_unknown_actions_are_refused()
    {
        await using (var off = new Fixture(enabled: false))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await off.Send("1234", RemoteActions.Toggle)).StatusCode);
            using var config = JsonDocument.Parse(await off.Http.GetStringAsync("/api/config"));
            Assert.False(config.RootElement.GetProperty("control").GetBoolean());
        }
        await using var f = new Fixture();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Send("1234", "shutdown")).StatusCode);
    }

    [Fact]
    public void Message_expires_after_duration()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var board = new MessageBoard(clock);
        board.Show("Concludi", TimeSpan.FromSeconds(10));
        Assert.Equal("Concludi", board.Current);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Null(board.Current);

        board.Show("Fisso", null);
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal("Fisso", board.Current);
        board.Clear();
        Assert.Null(board.Current);
    }

    [Fact]
    public void Qr_code_is_a_png()
    {
        var png = QrCodes.Png("http://192.168.1.20:8090/?pin=1234");
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);
    }
}
