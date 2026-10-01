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
public sealed record DisplayOptions(bool ShowClockWhenIdle, bool ShowNextPartWhenIdle, bool ShowDelay)
{
    public string Theme { get; init; } = "dark";
    public string Layout { get; init; } = "classic";
    public string Font { get; init; } = "";
    public bool ColoredDigits { get; init; } = true;
    public bool ShowTitle { get; init; } = true;
    public bool ShowSection { get; init; } = true;
    public bool ShowBar { get; init; } = true;
    public bool ShowNext { get; init; } = true;
    public bool ShowClock { get; init; } = true;
    public bool Flash { get; init; } = true;

    public static DisplayOptions From(Storage.AppSettings s) => new(s.ShowClockWhenIdle, s.ShowNextPartWhenIdle, s.ShowDelayOnDisplay)
    {
        Theme = s.DisplayTheme.ToString().ToLowerInvariant(),
        Layout = s.DisplayLayout.ToString().ToLowerInvariant(),
        Font = s.DisplayFont,
        ColoredDigits = s.ColoredDigits,
        ShowTitle = s.ShowTitle,
        ShowSection = s.ShowSection,
        ShowBar = s.ShowProgressBar,
        ShowNext = s.ShowNextPart,
        ShowClock = s.ShowClockWhileRunning,
        Flash = s.FlashOnOvertime,
    };
}

/// <summary>Configurazione del controllo remoto.</summary>
public sealed record RemoteConfig(bool Enabled, string Pin, IReadOnlyList<string> Presets);

/// <summary>Comandi accettati dal controllo remoto.</summary>
public static class RemoteActions
{
    public const string Toggle = "toggle", Next = "next", Previous = "previous", AddMinute = "plus", RemoveMinute = "minus",
        Select = "select", Message = "message", ClearMessage = "clearMessage";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Toggle, Next, Previous, AddMinute, RemoveMinute, Select, Message, ClearMessage };
}

/// <summary>
/// Server web integrato: permette di vedere il timer da qualsiasi dispositivo in rete
/// (telefono, tablet, PC) aprendo http://indirizzo-pc:porta.
/// </summary>
public sealed class TimerWebServer : IAsyncDisposable
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    readonly MeetingTimer _timer;
    readonly Func<DisplayOptions> _options;
    readonly MessageBoard _messages;
    readonly Func<RemoteConfig> _remote;
    readonly Func<string, string?, Task> _onCommand;
    readonly object _pinLock = new();
    int _failures;
    DateTime _lockedUntil;
    WebApplication? _app;

    /// <summary>Dopo questo numero di PIN errati il controllo si blocca per un minuto.</summary>
    public const int MaxPinFailures = 5;

    public int Port { get; private set; }
    public bool IsRunning => _app is not null;

    public TimerWebServer(MeetingTimer timer, Func<DisplayOptions> options, MessageBoard? messages = null,
        Func<RemoteConfig>? remote = null, Func<string, string?, Task>? onCommand = null)
    {
        _timer = timer;
        _options = options;
        _messages = messages ?? new MessageBoard();
        _remote = remote ?? (() => new RemoteConfig(false, "", []));
        _onCommand = onCommand ?? ((_, _) => Task.CompletedTask);
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
        app.MapGet("/api/config", () =>
        {
            var r = _remote();
            return Results.Json(new { control = r.Enabled, presets = r.Enabled ? r.Presets : [] }, Json);
        });
        app.MapPost("/api/control", HandleControl);

        await app.StartAsync();
        _app = app;
        Port = port;
    }

    public sealed record ControlRequest(string? Pin, string? Action, string? Value);

    async Task<IResult> HandleControl(ControlRequest req)
    {
        var r = _remote();
        if (!r.Enabled) return Results.Json(new { error = "Controllo remoto disattivato sul PC." }, Json, statusCode: 403);

        lock (_pinLock)
        {
            if (DateTime.UtcNow < _lockedUntil)
                return Results.Json(new { error = "Troppi tentativi errati: riprova tra un minuto." }, Json, statusCode: 429);
            if (!PinMatches(req.Pin, r.Pin))
            {
                if (++_failures >= MaxPinFailures)
                {
                    _lockedUntil = DateTime.UtcNow.AddMinutes(1);
                    _failures = 0;
                }
                return Results.Json(new { error = "PIN errato." }, Json, statusCode: 403);
            }
            _failures = 0;
        }

        // "check" verifica solo il PIN (usato dalla pagina all'accesso)
        if (req.Action == "check") return Results.Json(new { ok = true }, Json);
        if (req.Action is null || !RemoteActions.All.Contains(req.Action))
            return Results.Json(new { error = "Comando sconosciuto." }, Json, statusCode: 400);

        await _onCommand(req.Action, req.Value);
        return Results.Json(new { ok = true }, Json);
    }

    static bool PinMatches(string? given, string expected)
    {
        if (string.IsNullOrEmpty(expected) || given is null) return false;
        var a = System.Text.Encoding.UTF8.GetBytes(given.Trim());
        var b = System.Text.Encoding.UTF8.GetBytes(expected);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
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
            message = _messages.Current,
            style = new
            {
                theme = o.Theme,
                layout = o.Layout,
                font = o.Font,
                colored = o.ColoredDigits,
                showTitle = o.ShowTitle,
                showSection = o.ShowSection,
                showBar = o.ShowBar,
                showNext = o.ShowNext,
                showClock = o.ShowClock,
                flash = o.Flash,
            },
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
