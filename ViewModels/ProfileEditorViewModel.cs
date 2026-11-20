using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinferLauncher_v_2.Models;

namespace NinferLauncher_v_2.ViewModels;

/// <summary>One editable row in the profile editor. On Save it is written back to
/// <see cref="ServerProfiles.All"/> and persisted to <c>profiles.json</c>.</summary>
public partial class ProfileEditItem : ObservableObject
{
    [ObservableProperty] string _name = "";
    [ObservableProperty] string _group = "untested";
    [ObservableProperty] string _maxContext = "";
    [ObservableProperty] string _kvDtype = "rk2v4-e8";
    [ObservableProperty] bool _mtpSpec;
    [ObservableProperty] string _draftTokens = "0";
    [ObservableProperty] string _vision = "none";
    [ObservableProperty] bool _isBench;
    [ObservableProperty] string _note = "";

    /// <summary>The profile this row edits – null while it is a brand-new, not-yet-saved profile.</summary>
    public ServerProfile? Original { get; set; }

    public static ProfileEditItem From(ServerProfile p) => new()
    {
        Name = p.Name,
        Group = p.Group.ToString().ToLowerInvariant(),
        MaxContext = p.MaxContext.ToString(),
        KvDtype = p.KvDtype,
        MtpSpec = p.MtpSpec,
        DraftTokens = p.DraftTokens.ToString(),
        Vision = p.Vision.ToString().ToLowerInvariant(),
        IsBench = p.IsBench,
        Note = p.Note ?? "",
        Original = p,
    };
}

/// <summary>The "Server profiles" page: pick a profile in the combo box, edit its
/// fields, save to <c>profiles.json</c>, or add a brand-new one.</summary>
public partial class ProfileEditorViewModel : ViewModelBase
{
    readonly SettingsViewModel _settings;

    public ProfileEditorViewModel(SettingsViewModel settings)
    {
        _settings = settings;
        foreach (var p in ServerProfiles.All)
            Items.Add(ProfileEditItem.From(p));
        if (Items.Count > 0)
            Selected = Items[0];
    }

    /// <summary>All editable profiles (the launcher's list + not-yet-saved new ones).</summary>
    public ObservableCollection<ProfileEditItem> Items { get; } = new();

    [ObservableProperty] ProfileEditItem? _selected;
    [ObservableProperty] string _status = "";

    public static string[] GroupOptions { get; } = { "recommended", "usable", "bench", "untested" };
    public static string[] VisionOptions { get; } = { "none", "tokens4096", "tokens8192" };
    public static string[] KvOptions { get; } = { "rk2v4-e8", "rk4v4-e8", "rk4v4", "rk8v4", "int8", "bf16" };
    public static string[] DraftTokenOptions { get; } = { "0", "1", "2", "3", "4", "5", "6", "7", "8" };

    /// <summary>Writes the selected profile back to <c>ServerProfiles</c> and <c>profiles.json</c>.</summary>
    [RelayCommand]
    void SaveProfile()
    {
        var it = Selected;
        if (it is null)
        {
            Status = "Select a profile first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(it.Name))
        {
            Status = "Profile name is required.";
            return;
        }
        if (!int.TryParse(it.MaxContext, out int maxCtx) || maxCtx <= 0)
        {
            Status = "Context must be a positive number of tokens.";
            return;
        }
        if (!int.TryParse(string.IsNullOrWhiteSpace(it.DraftTokens) ? "0" : it.DraftTokens, out int drafts)
            || drafts < 0)
        {
            Status = "Draft tokens must be a non-negative number.";
            return;
        }

        var group = it.Group.Trim().ToLowerInvariant() switch
        {
            "recommended" => ProfileGroup.Recommended,
            "usable" => ProfileGroup.Usable,
            "bench" => ProfileGroup.Bench,
            _ => ProfileGroup.Untested,
        };
        var vision = it.Vision.Trim().ToLowerInvariant() switch
        {
            "tokens4096" => VisionMode.Tokens4096,
            "tokens8192" => VisionMode.Tokens8192,
            _ => VisionMode.None,
        };

        var profile = new ServerProfile(
            it.Name.Trim(), maxCtx, it.MtpSpec, it.MtpSpec ? drafts : 0,
            string.IsNullOrWhiteSpace(it.KvDtype) ? "rk2v4-e8" : it.KvDtype.Trim(),
            vision, it.IsBench, group)
        {
            Note = string.IsNullOrWhiteSpace(it.Note) ? null : it.Note.Trim(),
        };

        ServerProfiles.AddOrReplace(profile, it.Original);
        it.Original = profile; // from now on the row edits the saved profile
        ServerProfiles.Persist();
        ServerProfiles.RefreshDisplayRows();
        _settings.RefreshProfileRows();
        Status = $"Profile \"{profile.Name}\" saved to profiles.json ✓";
    }

    /// <summary>Appends a blank profile for the user to fill in (saved with Save).</summary>
    [RelayCommand]
    void AddProfile()
    {
        var it = new ProfileEditItem
        {
            Name = $"New profile {Items.Count + 1}",
            Group = "untested",
            MaxContext = "32768",
            KvDtype = "rk2v4-e8",
            MtpSpec = false,
            DraftTokens = "0",
            Vision = "none",
            IsBench = false,
            Note = "",
        };
        Items.Add(it);
        Selected = it;
        Status = "Fill in the profile, then press Save.";
    }
}