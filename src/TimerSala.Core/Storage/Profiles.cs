using System.Text.Json;
using TimerSala.Core.Models;

namespace TimerSala.Core.Storage;

/// <summary>Una congregazione che usa la sala. Il profilo con Id vuoto è quello di sempre (dati nella cartella principale).</summary>
public sealed record ProfileInfo(string Id, string Name);

/// <summary>
/// Profili per più congregazioni nella stessa sala: ognuno con impostazioni e schemi suoi, in una cartella sua
/// (profili\&lt;id&gt;). Il primo profilo resta nella cartella principale, così chi ha una sola congregazione non vede differenze.
/// </summary>
public sealed class ProfileCatalog
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    sealed class Data
    {
        public List<ProfileInfo> Profiles { get; set; } = [];
        public string? LastUsed { get; set; }
    }

    readonly string _root;
    Data _data;

    public ProfileCatalog(string root)
    {
        _root = root;
        Directory.CreateDirectory(root);
        _data = Read() ?? new Data();
        if (!_data.Profiles.Any(p => p.Id == "")) _data.Profiles.Insert(0, new ProfileInfo("", "Congregazione"));
    }

    string FilePath => Path.Combine(_root, "profili.json");

    Data? Read()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath), Json) : null; }
        catch { return null; }
    }

    void Save() => File.WriteAllText(FilePath, JsonSerializer.Serialize(_data, Json));

    public IReadOnlyList<ProfileInfo> Profiles => _data.Profiles;
    public bool HasSeveral => _data.Profiles.Count > 1;

    public ProfileInfo LastUsed => _data.Profiles.FirstOrDefault(p => p.Id == _data.LastUsed) ?? _data.Profiles[0];

    public void SetLastUsed(ProfileInfo p)
    {
        _data.LastUsed = p.Id;
        Save();
    }

    public string FolderOf(ProfileInfo p) => p.Id == "" ? _root : Path.Combine(_root, "profili", p.Id);

    public DataStore Open(ProfileInfo p) => new(FolderOf(p));

    /// <summary>Nuovo profilo: parte dalle impostazioni indicate (di solito quelle del profilo attuale), senza schemi.</summary>
    public ProfileInfo Add(string name, AppSettings startFrom)
    {
        var p = new ProfileInfo(Guid.NewGuid().ToString("N")[..8], name.Trim());
        var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(startFrom, Json), Json)!;
        // le settimane particolari e gli eventi sono della congregazione di partenza
        copy.OverseerVisits = [];
        copy.AssemblyWeeks = [];
        copy.Memorials = [];
        copy.SpecialTalkWeeks = [];
        copy.RecentEvents = [];
        Open(p).SaveSettings(copy);
        _data.Profiles.Add(p);
        Save();
        return p;
    }

    public void Rename(ProfileInfo p, string name)
    {
        int i = _data.Profiles.FindIndex(x => x.Id == p.Id);
        if (i < 0 || string.IsNullOrWhiteSpace(name)) return;
        _data.Profiles[i] = p with { Name = name.Trim() };
        Save();
    }

    /// <summary>Toglie un profilo e i suoi dati. Il primo profilo non si può togliere.</summary>
    public bool Remove(ProfileInfo p)
    {
        if (p.Id == "" || _data.Profiles.RemoveAll(x => x.Id == p.Id) == 0) return false;
        if (_data.LastUsed == p.Id) _data.LastUsed = "";
        Save();
        try { Directory.Delete(FolderOf(p), recursive: true); } catch { }
        return true;
    }

    public ProfileInfo? Find(string id) => _data.Profiles.FirstOrDefault(p => p.Id == id);

    /// <summary>
    /// Il profilo dell'adunanza più vicina adesso: quella di oggi che inizia tra poco o è in corso (da 3 ore prima
    /// a 2 ore dopo l'inizio). Se nessuna, null: si resta sull'ultimo usato.
    /// </summary>
    public ProfileInfo? ChooseFor(DateTime now)
    {
        if (!HasSeveral) return null;
        var monday = WeekMath.MondayOf(DateOnly.FromDateTime(now));
        ProfileInfo? best = null;
        double bestDistance = double.MaxValue;
        foreach (var p in _data.Profiles)
        {
            var s = Open(p).LoadSettings();
            foreach (var kind in new[] { MeetingKind.Midweek, MeetingKind.Weekend })
            {
                if (!s.HasMeeting(kind, monday)) continue;
                var start = s.StartOf(kind, monday);
                var delta = (start - now).TotalMinutes;
                if (delta < -120 || delta > 180) continue;
                double distance = Math.Abs(delta);
                if (distance < bestDistance) { best = p; bestDistance = distance; }
            }
        }
        return best;
    }
}
