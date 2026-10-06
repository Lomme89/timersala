using System.Collections.ObjectModel;
using TimerSala.Core.Storage;

namespace TimerSala.App.ViewModels;

/// <summary>
/// Profili per più congregazioni nella stessa sala: ognuno con orari, schemi, stile, frasi pronte e settimane
/// particolari suoi. Schermo, finestre, microfono, telecomando, rete e telefoni restano quelli del PC.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Elenco dei profili (null nella prova delle finestre: un solo profilo).</summary>
    public ProfileCatalog? Profiles { get; private set; }
    public ProfileInfo? CurrentProfile { get; private set => Set(ref field, value); }
    public bool HasProfiles { get; private set => Set(ref field, value); }

    /// <summary>I profili sono disponibili (sempre nel programma vero; non nella prova delle finestre).</summary>
    public bool HasProfilesCatalog => Profiles is not null;
    public string ProfileName { get; private set => Set(ref field, value); } = "";
    public ObservableCollection<ProfileInfo> ProfileList { get; } = [];

    public void UseProfiles(ProfileCatalog catalog, ProfileInfo current)
    {
        Profiles = catalog;
        OnPropertyChanged(nameof(HasProfilesCatalog));
        CurrentProfile = current;
        catalog.SetLastUsed(current);
        RefreshProfiles();
    }

    public void RefreshProfiles()
    {
        if (Profiles is null) return;
        ProfileList.Clear();
        foreach (var p in Profiles.Profiles) ProfileList.Add(p);
        if (CurrentProfile is { } c) CurrentProfile = Profiles.Find(c.Id) ?? Profiles.Profiles[0];
        HasProfiles = Profiles.HasSeveral;
        ProfileName = CurrentProfile?.Name ?? "";
    }

    /// <summary>Passa a un'altra congregazione. Restituisce il motivo se non si può adesso.</summary>
    public string? SwitchProfile(ProfileInfo profile)
    {
        if (Profiles is null || CurrentProfile?.Id == profile.Id) return null;
        if (Timer.IsRunning || IsVoiceArmed) return "Ferma il timer prima di cambiare congregazione.";
        if (IsTemporaryMeeting) return IsTraining ? "Esci prima dall'addestramento." : "Torna prima all'adunanza della settimana.";

        var store = Profiles.Open(profile);
        var settings = Exchange.ForProfile(store.LoadSettings(), Settings);
        SaveSettings();
        _store = store;
        CurrentProfile = profile;
        Profiles.SetLastUsed(profile);
        ApplySettings(settings);
        Kind = NextMeetingKind(DateTime.Now);
        OnPropertyChanged(nameof(IsMidweek));
        OnPropertyChanged(nameof(IsWeekend));
        LoadWeek(DateOnly.FromDateTime(DateTime.Today));
        RefreshProfiles();
        ShowStatus($"Congregazione: {profile.Name}.");
        return null;
    }

    public ProfileInfo? AddProfile(string name)
    {
        if (Profiles is null || string.IsNullOrWhiteSpace(name)) return null;
        var p = Profiles.Add(name, Settings);
        RefreshProfiles();
        return p;
    }

    public void RenameProfile(ProfileInfo p, string name)
    {
        Profiles?.Rename(p, name);
        RefreshProfiles();
    }

    public bool RemoveProfile(ProfileInfo p)
    {
        if (Profiles is null || p.Id == CurrentProfile?.Id) return false;
        bool ok = Profiles.Remove(p);
        RefreshProfiles();
        return ok;
    }
}
