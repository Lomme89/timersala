using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TimerSala.Core.Models;
using TimerSala.Core.Timing;

namespace TimerSala.Core.Web;

/// <summary>Opzioni di visualizzazione condivise con la pagina web.</summary>
public sealed record DisplayOptions(bool ShowClockWhenIdle, bool ShowNextPartWhenIdle, bool ShowDelay);

/// <summary>
/// Server web integrato: permette di vedere il timer da qualsiasi dispositivo in rete
/// (telefono, tablet, PC) aprendo http://indirizzo-pc:porta.
/// </summary>
public sealed class TimerWebServer : IAsyncDisposable
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    readonly MeetingTimer _timer;
    readonly Func<DisplayOptions> _options;
    WebApplication? _app;

    public int Port { get; private set; }
    public bool IsRunning => _app is not null;

    public TimerWebServer(MeetingTimer timer, Func<DisplayOptions> options)
    {
        _timer = timer;
        _options = options;
    }

    public async Task StartAsync(int port, bool localOnly = false)
    {
        await StopAsync();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
        {
            if (localOnly) k.Listen(IPAddress.Loopback, port);
            else k.ListenAnyIP(port);
        });
        var app = builder.Build();

        app.MapGet("/", () => Results.Content(ReadResource("wwwroot/index.html"), "text/html; charset=utf-8"));
        app.MapGet("/api/state", () => Results.Json(BuildState(), Json));
        app.MapGet("/api/schedule", () => Results.Json(BuildSchedule(), Json));
        app.MapGet("/api/events", StreamEvents);

        await app.StartAsync();
        _app = app;
        Port = port;
    }

    async Task StreamEvents(HttpContext ctx)
    {
        ctx.Response.Headers.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";
        var ct = ctx.RequestAborted;
        string? last = null;
        var lastSent = DateTime.MinValue;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var json = JsonSerializer.Serialize(BuildState(), Json);
                if (json != last || DateTime.UtcNow - lastSent > TimeSpan.FromSeconds(10))
                {
                    await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                    last = json;
                    lastSent = DateTime.UtcNow;
                }
                await Task.Delay(200, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    public object BuildState()
    {
        var s = _timer.GetSnapshot();
        var o = _options();
        SectionInfo.TryParse(s.Section, out var section);
        return new
        {
            phase = s.Phase.ToString().ToLowerInvariant(),
            mode = s.Mode.ToString().ToLowerInvariant(),
            running = s.IsRunning,
            title = s.Title,
            sectionLabel = s.Section is null ? null : SectionInfo.Label(section),
            sectionColor = s.Section is null ? null : SectionInfo.Color(section),
            display = s.Display,
            progress = Math.Round(s.Progress, 3),
            target = s.TargetSeconds,
            next = s.NextTitle,
            meeting = s.MeetingTitle,
            delay = s.DelaySeconds,
            clock = s.Now.ToString("HH:mm"),
            seconds = s.Now.ToString("ss"),
            showClock = o.ShowClockWhenIdle,
            showNext = o.ShowNextPartWhenIdle,
            showDelay = o.ShowDelay,
        };
    }

    object BuildSchedule()
    {
        var m = _timer.Meeting;
        int running = _timer.RunningIndex;
        int selected = _timer.SelectedIndex;
        return new
        {
            title = m.Title,
            parts = m.Parts.Select((p, i) => new
            {
                title = p.Title,
                section = SectionInfo.Label(p.Section),
                color = SectionInfo.Color(p.Section),
                minutes = p.DurationSeconds / 60,
                song = p.IsSong,
                actual = _timer.ActualFor(i) is { } t ? TimerSnapshot.FormatDuration(t.TotalSeconds) : null,
                over = _timer.ActualFor(i) is { } t2 && t2.TotalSeconds > p.DurationSeconds + 0.5,
                running = i == running,
                selected = i == selected,
            }),
        };
    }

    static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Risorsa mancante: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public async Task StopAsync()
    {
        if (_app is null) return;
        var app = _app;
        _app = null;
        try { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); await app.StopAsync(cts.Token); } catch { }
        await app.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    /// <summary>Indirizzi IPv4 locali con cui il PC è raggiungibile in rete.</summary>
    public static IReadOnlyList<string> LocalAddresses()
    {
        var list = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = ua.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue;
                    list.Add(ip);
                }
            }
        }
        catch (NetworkInformationException) { }
        return list.Distinct().ToList();
    }
}
