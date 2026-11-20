using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NinferLauncher_v_2.Models;
using NinferLauncher_v_2.Services;

namespace NinferLauncher_v_2.ViewModels;

/// <summary>One row of the model-file dropdown: a <c>.ninfer</c> file found under the
/// server directory (relative path) or a custom path picked from disk (absolute path).</summary>
public sealed class ModelFileRow
{
    public string Title { get; }
    /// <summary>Relative (server-dir) or absolute path stored as <c>ModelFile</c>.</summary>
    public string Path { get; }

    public ModelFileRow(string path, string? title = null)
    {
        Path = path;
        Title = title ?? path;
    }
}

/// <summary>Shared settings – used by both the Settings page and the Launcher page.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] string _serverDir;
    [ObservableProperty] string _modelFile;
    [ObservableProperty] string _port;
    [ObservableProperty] string _token;
    [ObservableProperty] int _profileIndex;
    [ObservableProperty] bool _wddmEvictableBudget = true;
    [ObservableProperty] string _statusMessage = "";

    Avalonia.Controls.Window? _window;

    /// <summary>All <c>.ninfer</c> files found under the server directory (+ custom entries).</summary>
    public ObservableCollection<ModelFileRow> ModelRows { get; } = new();

    private ModelFileRow? _selectedModelRow;
    /// <summary>Selected model-file row. Picking a row stores its path as
    /// <see cref="ModelFile"/> (relative for server-dir files, absolute for custom picks).</summary>
    public ModelFileRow? SelectedModelRow
    {
        get => _selectedModelRow;
        set
        {
            _selectedModelRow = value;
            OnPropertyChanged(nameof(SelectedModelRow));
            if (value is not null && !string.Equals(value.Path, ModelFile, StringComparison.Ordinal))
                ModelFile = value.Path;
        }
    }

    /// <summary>Dropdown rows (section headers + profiles), shared by both pages.
    /// Re-assigned by <see cref="RefreshProfileRows"/> after the profile list changes
    /// (e.g. from the Server profiles page).</summary>
    [ObservableProperty] IReadOnlyList<ProfileRow> _profileRows = ServerProfiles.DisplayRows;

    private ProfileRow? _selectedRow;
    /// <summary>Currently highlighted dropdown row. Picking a profile row updates
    /// <see cref="ProfileIndex"/>; clicking a section header keeps the current selection.</summary>
    public ProfileRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (value is { Profile: not null })
                ProfileIndex = value.AllIndex;
            else if (value is not null)
                ResyncSelectedRow();
        }
    }

    public string FullModelPath => Path.Combine(ServerDir, ModelFile);
    public string UrlText => $"http://127.0.0.1:{Port}";
    public bool IsPortValid => int.TryParse(Port, out _);

    public ServerProfile SelectedProfile =>
        ServerProfiles.All[Math.Clamp(ProfileIndex, 0, ServerProfiles.All.Length - 1)];

    /// <summary>Human-friendly description of the selected profile (used by the info cards).</summary>
    public string InfoContext => $"{SelectedProfile.MaxContext:N0} tokens";

    public string InfoMtp => SelectedProfile.MtpSpec
        ? $"MTP speculation – {SelectedProfile.DraftTokens} draft token(s) per step"
        : "none";

    public string InfoKv => SelectedProfile.KvDtype switch
    {
        "rk2v4-e8" => "rk2v4-e8 – 2-bit residuals + 4-bit e8 scales (most compressed KV, lowest VRAM)",
        "rk4v4-e8" => "rk4v4-e8 – 4-bit residuals + 4-bit e8 scales",
        "rk4v4" => "rk4v4 – 4-bit residuals + 4-bit scales (Hadamard-rotated)",
        "rk8v4" => "rk8v4 – 8-bit residuals + 4-bit scales (Hadamard-rotated)",
        "int8" => "int8 – 8-bit residuals + 8-bit scales (highest KV precision)",
        _ => SelectedProfile.KvDtype,
    };

    public string InfoVision => SelectedProfile.Vision switch
    {
        VisionMode.Tokens8192 => "8192 vision tokens",
        VisionMode.Tokens4096 => "4096 vision tokens",
        _ => "text only (no vision)",
    };

    public string InfoGroupNote => SelectedProfile.Group switch
    {
        ProfileGroup.Recommended => "Recommended & tested – safe daily-driver configuration on a 24 GB card.",
        ProfileGroup.Usable => "Usable & tested – smaller context keeps VRAM low for long, fast-running sessions.",
        ProfileGroup.Bench => "Benchmark – starts ninfer_bench.exe (prefill + 128-token decode), not a server.",
        _ => "Not tested – lowered VRAM footprint (~3 GB KV budget). Verify VRAM usage before long sessions.",
    };

    public Avalonia.Media.Brush GroupBrush =>
        new Avalonia.Media.SolidColorBrush(ServerProfiles.GroupColor(SelectedProfile.Group));

    /// <summary>Same group color at 10 % opacity – card backgrounds.</summary>
    public Avalonia.Media.Brush GroupBrushFaint =>
        new Avalonia.Media.SolidColorBrush(ServerProfiles.GroupColor(SelectedProfile.Group)) { Opacity = 0.10 };

    public void LoadFromStore()
    {
        var s = SettingsStore.Load();
        ServerDir = s.ServerDir;
        ModelFile = s.ModelFile;
        Port = s.Port;
        Token = s.Token;
        ProfileIndex = s.ProfileIndex;
        WddmEvictableBudget = s.WddmEvictableBudget;
        ResyncSelectedRow();
        ScanModelFiles();
        OnPropertyChanged(nameof(FullModelPath));
        OnPropertyChanged(nameof(UrlText));
    }

    /// <returns>false if the settings are invalid.</returns>
    public bool Save()
    {
        if (string.IsNullOrWhiteSpace(ServerDir))
        {
            StatusMessage = "Enter a server directory.";
            return false;
        }
        if (!int.TryParse(Port, out var port) || port is < 1 or > 65535)
        {
            StatusMessage = "Enter a valid port (1–65535).";
            return false;
        }
        SettingsStore.Save(new ServerSettings
        {
            ServerDir = ServerDir,
            ModelFile = ModelFile,
            Port = Port,
            Token = Token,
            ProfileIndex = ProfileIndex,
            WddmEvictableBudget = WddmEvictableBudget,
        });
        StatusMessage = "Settings saved ✓";
        return true;
    }

    partial void OnServerDirChanged(string value)
    {
        OnPropertyChanged(nameof(FullModelPath));
        ScanModelFiles();
    }
    partial void OnModelFileChanged(string value)
    {
        OnPropertyChanged(nameof(FullModelPath));
        EnsureModelRow();
    }
    partial void OnPortChanged(string value) => OnPropertyChanged(nameof(UrlText));
    partial void OnProfileIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(InfoContext));
        OnPropertyChanged(nameof(InfoMtp));
        OnPropertyChanged(nameof(InfoKv));
        OnPropertyChanged(nameof(InfoVision));
        OnPropertyChanged(nameof(InfoGroupNote));
        OnPropertyChanged(nameof(GroupBrush));
        OnPropertyChanged(nameof(GroupBrushFaint));
        ResyncSelectedRow();
    }

    /// <summary>Re-points the ComboBox at the profile row matching <see cref="ProfileIndex"/>.</summary>
    void ResyncSelectedRow()
    {
        var idx = Math.Clamp(ProfileIndex, 0, ServerProfiles.All.Length - 1);
        var row = ProfileRows.FirstOrDefault(r => r.Profile is not null && r.AllIndex == idx);
        if (row is not null && !ReferenceEquals(_selectedRow, row))
        {
            _selectedRow = row;
            OnPropertyChanged(nameof(SelectedRow));
        }
    }

    [RelayCommand]
    void SaveSettings() => Save();

    /// <summary>Rebuilds the profile dropdown after <c>ServerProfiles.All</c> changed
    /// (profile added / edited in the Server profiles page).</summary>
    public void RefreshProfileRows()
    {
        ProfileRows = ServerProfiles.DisplayRows;
        ResyncSelectedRow();
        OnPropertyChanged(nameof(SelectedRow));
    }

    /// <summary>Gives the view-model a window so the OS file picker can be opened.</summary>
    public void AttachWindow(Window window) => _window = window;

    /// <summary>Rebuilds the model-file dropdown: every <c>.ninfer</c> file under
    /// <see cref="ServerDir"/> (recursively, relative paths) + the current
    /// <see cref="ModelFile"/> if it is not among the found files (custom path).</summary>
    void ScanModelFiles()
    {
        var previous = ModelFile;
        ModelRows.Clear();
        if (!string.IsNullOrWhiteSpace(ServerDir) && Directory.Exists(ServerDir))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(ServerDir, "*.ninfer", SearchOption.AllDirectories)
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    string rel = Path.GetRelativePath(ServerDir, file);
                    ModelRows.Add(new ModelFileRow(rel, rel.Replace('\\', '/')));
                }
            }
            catch
            {
                // inaccessible sub-folders – partial list is fine
            }
        }
        if (!string.IsNullOrWhiteSpace(previous))
            EnsureModelRow();
        ResyncSelectedModelRow();
    }

    /// <summary>If <see cref="ModelFile"/> points outside the scanned set (custom / absolute
    /// path), add it as its own row so the selection keeps working.</summary>
    void EnsureModelRow()
    {
        if (string.IsNullOrWhiteSpace(ModelFile))
            return;
        if (FindModelRow(ModelFile) is null)
            ModelRows.Add(new ModelFileRow(ModelFile));
        ResyncSelectedModelRow();
    }

    /// <summary>Case-insensitive match by resolved full path.</summary>
    ModelFileRow? FindModelRow(string path)
    {
        string? norm(string p)
        {
            try
            {
                var full = Path.IsPathRooted(p) ? p : Path.Combine(ServerDir, p);
                return Path.GetFullPath(full);
            }
            catch
            {
                return p;
            }
        }
        var target = norm(path);
        return ModelRows.FirstOrDefault(r =>
            string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(norm(r.Path), target, StringComparison.OrdinalIgnoreCase));
    }

    void ResyncSelectedModelRow()
    {
        var row = string.IsNullOrWhiteSpace(ModelFile) ? null : FindModelRow(ModelFile);
        if (row is not null && !ReferenceEquals(_selectedModelRow, row))
        {
            _selectedModelRow = row;
            OnPropertyChanged(nameof(SelectedModelRow));
        }
    }

    /// <summary>OS file picker (filtered to <c>*.ninfer</c>) for a custom model location.
    /// The chosen absolute path is stored as <see cref="ModelFile"/> and kept as a row.</summary>
    [RelayCommand]
    private async Task PickModelFile()
    {
        if (_window == null)
            return;
        var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select .ninfer model file",
            AllowMultiple = false,
            FileTypeFilter = new System.Collections.Generic.List<FilePickerFileType>
            {
                new FilePickerFileType("Ninfer models") { Patterns = new[] { "*.ninfer" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        });
        if (files is { Count: > 0 })
            ModelFile = files[0].Path.LocalPath;
    }
}