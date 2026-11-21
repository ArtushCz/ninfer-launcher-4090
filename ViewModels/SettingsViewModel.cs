using Avalonia.Controls;
using Avalonia.Media;
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

    /// <summary>True when the selected profile runs vLLM – the WDDM
    /// <c>--wddm-evictable-budget</c> knob is a Ninfer-only flag, so the checkbox is
    /// hidden for vLLM profiles.</summary>
    [ObservableProperty] bool _showWddmOption = true;

    /// <summary>The engine of the visible NINFER / VLLM switcher on the server page.
    /// Always mirrors the selected profile's engine: clicking a switcher side selects
    /// a profile of that engine; picking a profile in the dropdown moves the switcher.</summary>
    [ObservableProperty] Engine _selectedEngine = Engine.Ninfer;

    /// <summary>Last used profile index per engine – switching back restores it.</summary>
    int _lastNinferIndex = -1;
    int _lastVllmIndex = -1;

    /// <summary>Server directory + model path per engine â€“ each engine remembers its
    /// own folders when the NINFER / VLLM switcher flips (Ninfer keeps its server dir
    /// + <c>.ninfer</c> file, vLLM its vLLM dir + HuggingFace model folder).</summary>
    readonly Dictionary<Engine, string> _serverDirByEngine = new();
    public IReadOnlyDictionary<Engine, string> ServerDirByEngine => _serverDirByEngine;
    public IReadOnlyDictionary<Engine, string> ModelFileByEngine => _modelFileByEngine;
    readonly Dictionary<Engine, string> _modelFileByEngine = new();

    /// <summary>True while <see cref="LoadFromStore"/> seeds the properties â€“ the
    /// property-change handlers must not write into the per-engine slots then.</summary>
    bool _loadingFromStore;
    bool _syncingEngineSlots;
    bool _switchingEngine;
    string _seededEngineDir = "";
    string _seededEngineModel = "";

    // switcher visuals – the active side gets a dark background, green border, white label
    static readonly IBrush ToggleActiveBackground = new SolidColorBrush(Color.Parse("#33373E"));
    static readonly IBrush ToggleIdleBackground = new SolidColorBrush(Color.Parse("#282A2E"));
    static readonly IBrush ToggleActiveLabel = Brushes.White;
    static readonly IBrush ToggleIdleLabel = new SolidColorBrush(Color.Parse("#8A8E94"));
    static readonly IBrush ToggleActiveBorder = new SolidColorBrush(Color.Parse("#6A9955"));
    static readonly IBrush ToggleIdleBorder = new SolidColorBrush(Color.Parse("#373B41"));

    public bool CanSelectNinferEngine => SelectedEngine != Engine.Ninfer;
    public bool CanSelectVllmEngine => SelectedEngine != Engine.Vllm;

    public IBrush NinferToggleBackground => SelectedEngine == Engine.Ninfer ? ToggleActiveBackground : ToggleIdleBackground;
    public IBrush VllmToggleBackground => SelectedEngine == Engine.Vllm ? ToggleActiveBackground : ToggleIdleBackground;
    public IBrush NinferToggleLabelBrush => SelectedEngine == Engine.Ninfer ? ToggleActiveLabel : ToggleIdleLabel;
    public IBrush VllmToggleLabelBrush => SelectedEngine == Engine.Vllm ? ToggleActiveLabel : ToggleIdleLabel;
    public IBrush NinferToggleBorder => SelectedEngine == Engine.Ninfer ? ToggleActiveBorder : ToggleIdleBorder;
    public IBrush VllmToggleBorder => SelectedEngine == Engine.Vllm ? ToggleActiveBorder : ToggleIdleBorder;

    void RaiseEngineToggleChanged()
    {
        OnPropertyChanged(nameof(CanSelectNinferEngine));
        OnPropertyChanged(nameof(CanSelectVllmEngine));
        OnPropertyChanged(nameof(NinferToggleBackground));
        OnPropertyChanged(nameof(VllmToggleBackground));
        OnPropertyChanged(nameof(NinferToggleLabelBrush));
        OnPropertyChanged(nameof(VllmToggleLabelBrush));
        OnPropertyChanged(nameof(NinferToggleBorder));
        OnPropertyChanged(nameof(VllmToggleBorder));
    }

    [RelayCommand]
    private void SelectNinferEngine() => SelectedEngine = Engine.Ninfer;

    [RelayCommand]
    private void SelectVllmEngine() => SelectedEngine = Engine.Vllm;

    partial void OnSelectedEngineChanged(Engine value)
    {
        if (value == SelectedProfile.Engine)
        {
            // the dropdown already selected a profile of this engine – visuals only
            RaiseEngineToggleChanged();
            return;
        }
        // The switcher flipped the engine. By the time this callback runs the backing
        // field already holds the NEW engine, so the visible dir/model still belong to
        // the other (previous) engine – stash them there, not under `value`.
        var fromEngine = value == Engine.Vllm ? Engine.Ninfer : Engine.Vllm;
        _serverDirByEngine[fromEngine] = ServerDir;
        _modelFileByEngine[fromEngine] = ModelFile;
        int remembered = value == Engine.Vllm ? _lastVllmIndex : _lastNinferIndex;
        int idx = remembered >= 0 && remembered < ServerProfiles.All.Length &&
                  ServerProfiles.All[remembered].Engine == value
            ? remembered
            : ServerProfiles.All.Select((p, i) => (p, i)).FirstOrDefault(t => t.p.Engine == value).i;
        if (idx < 0)
        {
            SelectedEngine = fromEngine; // revert – no profile of that engine
            StatusMessage = value == Engine.Vllm
                ? "No vLLM profile in profiles.json – add one (engine: vllm) to switch to vLLM."
                : "No Ninfer profile in profiles.json – add one to switch to Ninfer.";
            return;
        }
        _switchingEngine = true;
        ProfileIndex = idx; // stash/restore in OnProfileIndexChanged is skipped
        _switchingEngine = false;
        // restore the target engine's remembered dir/model
        RestoreEngineValues(value);
        StatusMessage = string.IsNullOrWhiteSpace(ServerDir)
            ? (value == Engine.Vllm
                ? "Set the vLLM server directory – the vLLM folder is remembered separately from the Ninfer one."
                : "Set the Ninfer server directory – the Ninfer folder is remembered separately from the vLLM one.")
            : "";
    }

    /// <summary>Restores <paramref name="engine"/>'s remembered server directory and
    /// model into the visible properties via direct backing-field writes (no
    /// re-entrant change handlers), raises the derived properties, and refreshes the
    /// model dropdown so the ComboBox lists the restored engine's entries instead of
    /// the previous engine's.</summary>
    void RestoreEngineValues(Engine engine)
    {
        _serverDir = _serverDirByEngine[engine];
        _modelFile = _modelFileByEngine[engine];
        OnPropertyChanged(nameof(ServerDir));
        OnPropertyChanged(nameof(ModelFile));
        OnPropertyChanged(nameof(FullModelPath));
        ScanModelFiles();
    }

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

    /// <summary>True when the selected profile targets vLLM – switches the model
    /// row from a <c>.ninfer</c> file to a model directory.</summary>
    public bool IsVllmProfile => SelectedProfile.Engine == Engine.Vllm;

    /// <summary>MODEL row caption – a file for Ninfer, a directory for vLLM.</summary>
    public string ModelCardTitle => IsVllmProfile ? "MODEL DIRECTORY" : "MODEL FILE";

    /// <summary>Shows the file picker (Ninfer <c>.ninfer</c>) / folder picker (vLLM).</summary>
    public bool ShowModelFilePicker => !IsVllmProfile;
    public bool ShowModelFolderPicker => IsVllmProfile;

    /// <summary>Directory card caption – makes it visible which kind of directory
    /// the selected engine expects (Ninfer binaries vs the vLLM folder).</summary>
    public string ServerCardTitle => IsVllmProfile
        ? "VLLM SERVER DIRECTORY (folder with python\\)"
        : "NINFER SERVER DIRECTORY";

    /// <summary>Engine line of the profile info card – the explicit switch indicator
    /// between the Ninfer engine and vLLM.</summary>
    public string InfoEngine => SelectedProfile.Engine == Engine.Vllm
        ? $"vLLM – {SelectedProfile.ExeName}"
        : $"Ninfer – {SelectedProfile.ExeName}";

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
        // per-engine memory: the saved values belong to the loaded profile's engine;
        // a new file also carries VllmServerDir, an old file only has the single
        // ServerDir, in which case both engines start with it.
        _serverDirByEngine[Engine.Ninfer] = s.ServerDir;
        _modelFileByEngine[Engine.Ninfer] = s.ModelFile;
        if (!string.IsNullOrWhiteSpace(s.VllmServerDir))
        {
            _serverDirByEngine[Engine.Vllm] = s.VllmServerDir;
            _modelFileByEngine[Engine.Vllm] = s.VllmModelFile ?? "";
        }
        else
        {
            _serverDirByEngine[Engine.Vllm] = s.ServerDir;
            _modelFileByEngine[Engine.Vllm] = s.ModelFile;
        }
        var loadedEngine = ServerProfiles.All[Math.Clamp(s.ProfileIndex, 0, ServerProfiles.All.Length - 1)].Engine;
        _seededEngineDir = loadedEngine == Engine.Vllm ? _serverDirByEngine[Engine.Vllm] : _serverDirByEngine[Engine.Ninfer];
        _seededEngineModel = loadedEngine == Engine.Vllm ? _modelFileByEngine[Engine.Vllm] : _modelFileByEngine[Engine.Ninfer];
        _loadingFromStore = true;
        ServerDir = _seededEngineDir;
        ModelFile = _seededEngineModel;
        Port = s.Port;
        Token = s.Token;
        ProfileIndex = s.ProfileIndex;
        WddmEvictableBudget = s.WddmEvictableBudget;
        _loadingFromStore = false;
        ResyncSelectedRow();
        ScanModelFiles();
        OnPropertyChanged(nameof(FullModelPath));
        OnPropertyChanged(nameof(UrlText));
        // Re-sync engine-dependent UI flags (the index assignment above may not raise a
        // change event when the saved index equals the property's initial value).
        ShowWddmOption = SelectedProfile.Engine != Engine.Vllm;
        OnPropertyChanged(nameof(IsVllmProfile));
        OnPropertyChanged(nameof(ModelCardTitle));
        OnPropertyChanged(nameof(ShowModelFilePicker));
        OnPropertyChanged(nameof(ShowModelFolderPicker));
        OnPropertyChanged(nameof(InfoEngine));
        OnPropertyChanged(nameof(ServerCardTitle));
        // engine switcher: the index assignment above may not raise a change event
        // when the saved index equals the property's initial value
        if (SelectedProfile.Engine == Engine.Vllm)
            _lastVllmIndex = ProfileIndex;
        else
            _lastNinferIndex = ProfileIndex;
        if (SelectedEngine != SelectedProfile.Engine)
            SelectedEngine = SelectedProfile.Engine;
        else
            RaiseEngineToggleChanged();
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
        // persist both engines' folders: the visible page's values under the active
        // engine, the other engine's remembered values alongside (VllmServerDir /
        // VllmModelFile â€“ absent in settings.json files written by older versions)
        _serverDirByEngine[SelectedProfile.Engine] = ServerDir;
        _modelFileByEngine[SelectedProfile.Engine] = ModelFile;
        bool isVllm = SelectedProfile.Engine == Engine.Vllm;
        var other = isVllm ? Engine.Ninfer : Engine.Vllm;
        SettingsStore.Save(new ServerSettings
        {
            ServerDir = isVllm ? _serverDirByEngine[other] : ServerDir,
            ModelFile = isVllm ? _modelFileByEngine[other] : ModelFile,
            Port = Port,
            Token = Token,
            ProfileIndex = ProfileIndex,
            WddmEvictableBudget = WddmEvictableBudget,
            VllmServerDir = isVllm ? ServerDir : _serverDirByEngine[Engine.Vllm],
            VllmModelFile = isVllm ? ModelFile : _modelFileByEngine[Engine.Vllm],
        });
        StatusMessage = "Settings saved ✓";
        return true;
    }

    partial void OnServerDirChanged(string value)
    {
        if (!_loadingFromStore && !_syncingEngineSlots)
            _serverDirByEngine[SelectedProfile.Engine] = value;
        OnPropertyChanged(nameof(FullModelPath));
        ScanModelFiles();
    }
    partial void OnModelFileChanged(string value)
    {
        if (!_loadingFromStore && !_syncingEngineSlots)
            _modelFileByEngine[SelectedProfile.Engine] = value;
        OnPropertyChanged(nameof(FullModelPath));
        EnsureModelRow();
    }
    partial void OnPortChanged(string value) => OnPropertyChanged(nameof(UrlText));
    partial void OnProfileIndexChanged(int value)
    {
        // per-engine memory: stash the current dir / model under the previous engine
        // (the switcher still reflects it) and restore the new engine's own values
        // when the engine actually changes
        if (!_loadingFromStore && !_switchingEngine)
        {
            var fromEngine = SelectedEngine;
            _serverDirByEngine[fromEngine] = ServerDir;
            _modelFileByEngine[fromEngine] = ModelFile;

            if (SelectedProfile.Engine != fromEngine)
            {
                // direct backing-field writes avoid re-entrant handlers;
                // also refreshes the model dropdown for the restored directory
                RestoreEngineValues(SelectedProfile.Engine);

                StatusMessage = string.IsNullOrWhiteSpace(ServerDir)
                    ? (SelectedProfile.Engine == Engine.Vllm
                        ? "Set the vLLM server directory â€“ the vLLM folder is remembered separately from the Ninfer one."
                        : "Set the Ninfer server directory â€“ the Ninfer folder is remembered separately from the vLLM one.")
                    : "";
            }
        }
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(InfoContext));
        OnPropertyChanged(nameof(InfoMtp));
        OnPropertyChanged(nameof(InfoKv));
        OnPropertyChanged(nameof(InfoVision));
        OnPropertyChanged(nameof(InfoGroupNote));
        OnPropertyChanged(nameof(InfoEngine));
        OnPropertyChanged(nameof(ServerCardTitle));
        OnPropertyChanged(nameof(GroupBrush));
        OnPropertyChanged(nameof(GroupBrushFaint));
        ShowWddmOption = SelectedProfile.Engine != Engine.Vllm;
        OnPropertyChanged(nameof(IsVllmProfile));
        OnPropertyChanged(nameof(ModelCardTitle));
        OnPropertyChanged(nameof(ShowModelFilePicker));
        OnPropertyChanged(nameof(ShowModelFolderPicker));
        ResyncSelectedRow();
        // keep the engine switcher in sync with the picked profile
        if (SelectedProfile.Engine == Engine.Vllm)
            _lastVllmIndex = value;
        else
            _lastNinferIndex = value;
        if (SelectedEngine != SelectedProfile.Engine)
            SelectedEngine = SelectedProfile.Engine;
        else
            RaiseEngineToggleChanged();
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
        // the edited profile may have changed engine – keep the switcher truthful
        if (SelectedEngine != SelectedProfile.Engine)
            SelectedEngine = SelectedProfile.Engine;
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

    /// <summary>OS folder picker for a vLLM model directory (HuggingFace-style layout,
    /// e.g. <c>F:\LLM\qwen3-vl-8b-abliterated</c>). The chosen absolute path is stored as
    /// <see cref="ModelFile"/> and kept as a custom row, exactly like the file picker.</summary>
    [RelayCommand]
    private async Task PickModelFolder()
    {
        if (_window == null)
            return;
        var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select vLLM model directory",
        });
        if (folders is { Count: > 0 })
            ModelFile = folders[0].Path.LocalPath;
    }
}