using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using TimerSala.Core.Web;

namespace TimerSala.App.ViewModels;

/// <summary>Un dispositivo fidato nell'elenco del riquadro della rete.</summary>
public sealed record TrustedDeviceItem(string Id, string Name, string LastSeenText);

/// <summary>Dispositivi fidati: dopo il primo PIN un telefono comanda senza chiederlo più, finché non lo si revoca dal PC.</summary>
public sealed partial class MainViewModel
{
    static readonly CultureInfo ItCulture = CultureInfo.GetCultureInfo("it-IT");

    DeviceRegistry? _devices;

    public ObservableCollection<TrustedDeviceItem> TrustedDevices { get; } = [];

    /// <summary>Interruttore al volo nel riquadro della rete: spento, i telefoni vedono il timer ma non lo comandano.</summary>
    public bool RemoteCommandsEnabled
    {
        get => Settings.RemoteControlEnabled;
        set
        {
            if (Settings.RemoteControlEnabled == value) return;
            Settings.RemoteControlEnabled = value;
            SaveSettings();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ControlUrl));
            ShowStatus(value ? "Comandi dal telefono attivi." : "Comandi dal telefono bloccati: i telefoni vedono soltanto il timer.");
        }
    }
    public bool HasTrustedDevices { get; private set => Set(ref field, value); }

    public ICommand RevokeDeviceCommand => field ??= new RelayCommand(p =>
    {
        if (p is string id && _devices?.Revoke(id) == true) ShowStatus("Dispositivo revocato: per comandare dovrà inserire di nuovo il PIN.");
    });

    public ICommand RevokeAllDevicesCommand => field ??= new RelayCommand(() =>
    {
        if (_devices is null || TrustedDevices.Count == 0) return;
        if (!Confirm("Revocare tutti i dispositivi? Per comandare il timer dovranno inserire di nuovo il PIN.")) return;
        _devices.RevokeAll();
        ShowStatus("Tutti i dispositivi sono stati revocati.");
    });

    /// <summary>Il registro usa sempre l'elenco delle impostazioni correnti; ogni modifica si salva sul thread dell'interfaccia.</summary>
    DeviceRegistry CreateDeviceRegistry() => _devices = new DeviceRegistry(() => Settings.TrustedDevices,
        () => Application.Current?.Dispatcher.BeginInvoke(() => { SaveSettings(); RefreshTrustedDevices(); }));

    void RefreshTrustedDevices()
    {
        var now = DateTimeOffset.UtcNow;
        var items = Settings.TrustedDevices.ToList().OrderByDescending(d => d.LastSeen)
            .Select(d => new TrustedDeviceItem(d.Id, d.Name, LastSeen(d.LastSeen, now))).ToList();
        TrustedDevices.Clear();
        foreach (var i in items) TrustedDevices.Add(i);
        HasTrustedDevices = TrustedDevices.Count > 0;
    }

    static string LastSeen(DateTimeOffset when, DateTimeOffset now)
    {
        var local = when.ToLocalTime();
        if (now - when < TimeSpan.FromHours(1)) return "usato poco fa";
        if (local.Date == DateTime.Today) return $"usato oggi alle {local:HH:mm}";
        if (local.Date == DateTime.Today.AddDays(-1)) return "usato ieri";
        return "usato il " + local.ToString("d MMMM", ItCulture);
    }
}
