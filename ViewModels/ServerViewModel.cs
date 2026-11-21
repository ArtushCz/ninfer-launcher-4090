using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NinferLauncher_v_2.Models;
using NinferLauncher_v_2.Services;

namespace NinferLauncher_v_2.ViewModels;

public static class Palette
{
    public static readonly Avalonia.Media.SolidColorBrush Dimmed = new(Avalonia.Media.Color.Parse("#6E7279"));
    public static readonly Avalonia.Media.SolidColorBrush Running = new(Avalonia.Media.Color.Parse("#6A9955"));
    public static readonly Avalonia.Media.SolidColorBrush Warning = new(Avalonia.Media.Color.Parse("#E5B94E"));
    public static readonly Avalonia.Media.SolidColorBrush Error = new(Avalonia.Media.Color.Parse("#A54242"));
}

/// <summary>Server launcher: settings, start/stop/restart and a live terminal.</summary>
public partial class ServerViewModel : ViewModelBase
{
    readonly SettingsViewModel _settings;
    readonly ServerService _service = new();
    Avalonia.Controls.Window? _window;

    public ObservableCollection<TerminalLine> Terminal { get; } = new();

    /// <summary>Anchor line for range selection (a Shift+click extends the range from here).</summary>
    TerminalLine? _selectionAnchor;

    [ObservableProperty] bool _isRunning;
    [ObservableProperty] bool _canStop;
    [ObservableProperty] string _statusText = "Stopped";
    [ObservableProperty] ISolidColorBrush _statusBrush = Palette.Dimmed;

    /// <summary>Live throughput readout: "Prefill: X tok/s (avg Y) • Decode: A tok/s (avg B)".</summary>
    [ObservableProperty] string _tpsStatusText = "⚌ Prefill: —  •  Decode: —";
    double _pfCur, _dcCur, _pfAvg, _dcAvg;

    public SettingsViewModel Settings => _settings;

    public string UrlText => _settings.UrlText;

    public ServerViewModel(SettingsViewModel settings)
    {
        _settings = settings;
        _hw = new HardwareMonitor();
        _service.OutputLine += line =>
            Dispatcher.UIThread.Post(() => HandleServerLine(line));
        _service.StateChanged += () =>
            Dispatcher.UIThread.Post(UpdateStateUi);
        _hw.SampleReady += s =>
            Dispatcher.UIThread.Post(() => UpdateHw(s));
    }

    /// <summary>Received server lines → terminal (filters noise) + tok/s chip.</summary>
    void HandleServerLine(string line)
    {
        if (LogFilter.IsThroughputLine(line))
        {
            if (LogFilter.TryParseThroughput(line, out var prefill, out var decode))
                UpdateTps(prefill, decode);
            return;
        }
        if (LogFilter.IsNoisy(line))
            return;
        AppendLine(line, line.Contains("rror", StringComparison.Ordinal));
    }

    /// <summary>Updates the live tok/s readout (chip in the terminal header).</summary>
    void UpdateTps(double prefill, double decode)
    {
        _pfCur = prefill;
        _dcCur = decode;
        // EMA (decaying average) from non-zero samples
        if (prefill > 0) _pfAvg = _pfAvg <= 0 ? prefill : _pfAvg + (prefill - _pfAvg) * 0.35;
        if (decode > 0) _dcAvg = _dcAvg <= 0 ? decode : _dcAvg + (decode - _dcAvg) * 0.35;
        TpsStatusText =
            $"⚌ Prefill: {Fmt(_pfCur)} tok/s{(_pfAvg > 0 ? $" (avg {Fmt(_pfAvg)})" : "")}" +
            $"  •  Decode: {Fmt(_dcCur)} tok/s{(_dcAvg > 0 ? $" (avg {Fmt(_dcAvg)})" : "")}";
    }

    void ResetTps()
    {
        _pfCur = _dcCur = _pfAvg = _dcAvg = 0;
        TpsStatusText = "⚌ Prefill: —  •  Decode: —";
    }

    static string Fmt(double v) => v >= 1000
        ? v.ToString("0", System.Globalization.CultureInfo.CurrentCulture)
        : v.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);

    // ====== Hardware monitor: GPU memory used/max, GPU usage %, RAM usage %, RAM used/max ======
    HardwareMonitor _hw = null!; // created in the ctor (needs _service)

    [ObservableProperty] double _gpuMemPct;
    [ObservableProperty] double _gpuUsePct;
    [ObservableProperty] double _ramPct;
    [ObservableProperty] double _ramMemPct;
    [ObservableProperty] string _gpuMemText = "— / — GB";
    [ObservableProperty] string _gpuUseText = "—";
    [ObservableProperty] string _ramPctText = "—";
    [ObservableProperty] string _ramMemText = "— / — GB";

    /// <summary>Bar fill colour: green up to 75 %, yellow up to 90 %, red above (VRAM pressure).</summary>
    static ISolidColorBrush BarBrush(double pct) =>
        pct >= 90 ? Palette.Error : pct >= 75 ? Palette.Warning : Palette.Running;

    public ISolidColorBrush GpuMemBarBrush => BarBrush(GpuMemPct);
    public ISolidColorBrush GpuUseBarBrush => BarBrush(GpuUsePct);
    public ISolidColorBrush RamPctBarBrush => BarBrush(RamPct);
    public ISolidColorBrush RamMemBarBrush => BarBrush(RamMemPct);

    static string Gb(double bytes) =>
        (bytes / 1073741824.0).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);

    void UpdateHw(HwSample s)
    {
        GpuMemPct = s.GpuTotalBytes > 0 ? s.GpuUsedBytes / s.GpuTotalBytes * 100 : 0;
        GpuUsePct = s.GpuUtilPct;
        RamPct = s.RamLoadPct;
        RamMemPct = s.RamTotalBytes > 0 ? s.RamUsedBytes / s.RamTotalBytes * 100 : 0;
        // the VRAM total can be unknown (WMI/perf counters only report a sentinel
        // on some builds) - then show the used amount only
        GpuMemText = s.GpuTotalBytes > 0
            ? $"{Gb(s.GpuUsedBytes)} / {Gb(s.GpuTotalBytes)} GB"
            : (s.GpuUsedBytes > 0 ? $"{Gb(s.GpuUsedBytes)} GB" : "—");
        GpuUseText = s.GpuUtilPct > 0
            ? s.GpuUtilPct.ToString("0", System.Globalization.CultureInfo.CurrentCulture) + "%"
            : "—";
        RamPctText = s.RamTotalBytes > 0
            ? s.RamLoadPct.ToString("0", System.Globalization.CultureInfo.CurrentCulture) + "%"
            : "—";
        RamMemText = s.RamTotalBytes > 0
            ? $"{Gb(s.RamUsedBytes)} / {Gb(s.RamTotalBytes)} GB"
            : "— / — GB";
        OnPropertyChanged(nameof(GpuMemBarBrush));
        OnPropertyChanged(nameof(GpuUseBarBrush));
        OnPropertyChanged(nameof(RamPctBarBrush));
        OnPropertyChanged(nameof(RamMemBarBrush));
    }

    /// <summary>The window is stored for clipboard access (Avalonia 12 exposes the clipboard on the top-level).</summary>
    public void AttachWindow(Avalonia.Controls.Window window)
    {
        _window = window;
    }

    /// <summary>Application shutdown – stops the server and releases the process.</summary>
    public void OnWindowClosing()
    {
        _hw.Dispose();
        _service.Stop();
        _service.Dispose();
    }

    public void AppendLine(string text, bool isError = false)
    {
        // Server log lines already carry their own "[date time]" stamp, and launcher
        // lines start with a "[TAG]" marker – only add a time for lines that lack one.
        var needsTime = text.Length == 0 || text[0] != '[';
        Terminal.Add(new TerminalLine(needsTime ? $"[{DateTime.Now:HH:mm:ss}] {text}" : text, isError));
        while (Terminal.Count > 5000)
            Terminal.RemoveAt(0);
    }

    void UpdateStateUi()
    {
        var s = _service.State;
        IsRunning = s is ServerState.Running or ServerState.Starting;
        CanStop = IsRunning || s == ServerState.Stopping;
        StatusText = s switch
        {
            ServerState.Running => "Server running",
            ServerState.Starting => "Starting…",
            ServerState.Stopping => "Stopping…",
            _ => _service.LastError != null ? "Error" : "Stopped",
        };
        StatusBrush = s switch
        {
            ServerState.Running or ServerState.Starting => Palette.Running,
            _ => _service.LastError != null ? Palette.Error : Palette.Dimmed,
        };
        if (s == ServerState.Stopped && !string.IsNullOrEmpty(_service.LastError))
        {
            AppendLine($"[ERROR] {_service.LastError}", true);
            _service.LastError = null;
        }
    }
[RelayCommand]
    private void StartServer()
    {
        var profile = _settings.SelectedProfile;

        // A benchmark loads the full model (~16 GB) on top of whatever is already on the
        // GPU. If a server is still running there isn't enough VRAM – require it to stop first.
        if (IsRunning)
        {
            if (profile.IsBench)
                AppendLine("[BENCH] A server is already running and holding VRAM. Stop it first, then run the benchmark.", true);
            else
                AppendLine("[LAUNCHER] A server is already running.", true);
            return;
        }

        if (!int.TryParse(_settings.Port, out var port))
        {
            AppendLine("[ERROR] Enter a valid port.", true);
            return;
        }
        var dir = _settings.ServerDir;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            AppendLine($"[ERROR] Server directory does not exist: {dir}", true);
            return;
        }
        // vLLM profiles load a HuggingFace-style model DIRECTORY; Ninfer profiles a .ninfer file.
        bool modelOk = profile.Engine == Engine.Vllm
            ? Directory.Exists(_settings.FullModelPath)
            : File.Exists(_settings.FullModelPath);
        if (!modelOk)
        {
            AppendLine($"[ERROR] Model not found: {_settings.FullModelPath}" +
                       (profile.Engine == Engine.Vllm ? " (vLLM expects a model directory)" : ""), true);
            return;
        }

        AppendLine($"[LAUNCHER] Profile: {profile.Name}");
        if (profile.WarnHighContext && profile.Engine == Engine.Ninfer)
            AppendLine($"[WARN] Profile {profile.MaxContext / 1000}K tokens is above 240K – it may not work on Windows (WDDM). If the server is not listening on the port, run a lower profile.", true);
        ResetTps();
        AppendLine($"[LAUNCHER] Directory: {dir}");
        if (profile.Engine == Engine.Vllm)
        {
            // Log the exact resolved vLLM command so the user can compare with their bat script.
            string vllmCmd = string.Join(' ', ServerProfiles.BuildArgs(
                profile, _settings.FullModelPath, port, dir, _settings.WddmEvictableBudget));
            AppendLine($"[LAUNCHER] Start: python\\python.exe {vllmCmd}");
        }
        else
        {
            AppendLine(profile.IsBench
                ? $"[LAUNCHER] Start: {profile.ExeName} (benchmark – freeing VRAM first)"
                : $"[LAUNCHER] Start: {profile.ExeName} (port {port})");
        }
        AppendLine("-------------------------------------------------------------");
        _service.Start(dir, _settings.FullModelPath, port, profile, _settings.WddmEvictableBudget);
    }

    [RelayCommand]
    private void StopServer()
    {
        if (!IsRunning)
            return;
        AppendLine("[LAUNCHER] Stopping server…");
        _service.Stop();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task RestartServer()
    {
        if (IsRunning)
        {
            _service.Stop();
            await System.Threading.Tasks.Task.Delay(1500);
        }
        StartServer();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task BrowseDir()
    {
        if (_window == null)
            return;
        var provider = _window.StorageProvider;
        var folders = await provider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions());
        if (folders is { Count: > 0 })
        {
            var path = folders[0].Path.LocalPath;
            _settings.ServerDir = path;
            AppendLine($"[LAUNCHER] Server directory: {path}");
        }
    }

    /// <summary>Opens the server UI (<see cref="SettingsViewModel.UrlText"/>) in the default web browser.</summary>
    [RelayCommand]
    private void OpenUrl()
    {
        if (!_settings.IsPortValid)
        {
            AppendLine("[ERROR] Enter a valid port first.", true);
            return;
        }
        var url = _settings.UrlText;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            AppendLine($"[LAUNCHER] Opening {url} in the browser…");
        }
        catch (Exception ex)
        {
            AppendLine($"[ERROR] Could not open {url}: {ex.Message}", true);
        }
    }

    [RelayCommand]
    private void ClearLog() => Terminal.Clear();

    /// <summary>Copies the whole terminal log to the clipboard.
    /// (Individual lines can also be selected with the mouse and copied – Avalonia
    /// TextBlocks support native selection.)</summary>
    [RelayCommand]
    private async void CopyLog()
    {
        if (Terminal.Count == 0)
            return;
        string text = string.Join(Environment.NewLine, Terminal.Select(l => l.Text));
        var clipboard = GetClipboard();
        if (clipboard is null)
        {
            AppendLine("[ERROR] Clipboard is not available.", true);
            return;
        }
        // TEMP diagnostics: pinpoint where the clipboard write hangs.
        System.Diagnostics.Debug.WriteLine("[CLIP] CopyLog start (" + Terminal.Count + " lines)");
        var ok = false;
        try
        {
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text);
            ok = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[CLIP] CopyLog FAILED: " + ex);
        }
        System.Diagnostics.Debug.WriteLine("[CLIP] CopyLog done ok=" + ok);
        Dispatcher.UIThread.Post(() =>
            AppendLine(ok
                ? $"[LAUNCHER] Copied {Terminal.Count} lines to the clipboard."
                : "[ERROR] Clipboard write failed – another app is holding the clipboard open.", true));
    }

    /// <summary>Terminal selection start: a plain press anchors on this line and highlights it;
    /// a Shift+press keeps the existing anchor and highlights the inclusive range to this line.
    /// Nothing is copied until the selection is committed on pointer release.</summary>
    public void BeginLineSelection(TerminalLine line, bool extend)
    {
        if (extend && _selectionAnchor is not null)
        {
            HighlightRange(_selectionAnchor, line);
            return;
        }

        _selectionAnchor = line;
        ClearLineSelection();
        line.IsSelected = true;
    }

    public void UpdateLineSelection(TerminalLine line)
    {
        if (_selectionAnchor is null)
            return;
        HighlightRange(_selectionAnchor, line);
    }

    /// <summary>Commit on pointer release: copies the currently highlighted lines to the
    /// clipboard (a single line for a plain click, the dragged/extended range otherwise).</summary>
    public void CommitLineSelection()
    {
        int lo = int.MaxValue, hi = int.MinValue;
        for (int i = 0; i < Terminal.Count; i++)
        {
            if (!Terminal[i].IsSelected)
                continue;
            if (i < lo) lo = i;
            if (i > hi) hi = i;
        }

        if (lo > hi)
            return;
        CopyRange(lo, hi);
    }

    /// <summary>Highlights the inclusive range between two terminal lines, clearing the rest.</summary>
    void HighlightRange(TerminalLine anchor, TerminalLine line)
    {
        int start = Terminal.IndexOf(anchor);
        int end = Terminal.IndexOf(line);
        if (start < 0 || end < 0)
            return;

        int lo = Math.Min(start, end);
        int hi = Math.Max(start, end);

        ClearLineSelection();
        for (int i = lo; i <= hi; i++)
            Terminal[i].IsSelected = true;
    }

    /// <summary>Clears the selection highlight from every terminal line.</summary>
    public void ClearLineSelection()
    {
        foreach (var l in Terminal)
            l.IsSelected = false;
    }

    /// <summary>Copies the inclusive line range to the clipboard and reports it in the log.</summary>
    async void CopyRange(int lo, int hi)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = lo; i <= hi; i++)
        {
            if (i > lo)
                sb.Append(Environment.NewLine);
            sb.Append(Terminal[i].Text);
        }
        string text = sb.ToString();

        var clipboard = GetClipboard();
        if (clipboard is null)
        {
            AppendLine("[ERROR] Clipboard is not available.", true);
            return;
        }

        var ok = false;
        try
        {
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text);
            ok = true;
        }
        catch
        {
        }
        Dispatcher.UIThread.Post(() =>
            AppendLine(ok
                ? $"[LAUNCHER] Copied {hi - lo + 1} selected line(s) to the clipboard."
                : "[ERROR] Clipboard write failed – another app is holding the clipboard open.", true));
    }

    /// <summary>Resolves the platform <see cref="Avalonia.Input.Platform.IClipboard"/>.
    /// Avalonia 12 exposes the clipboard on the window/top-level, so the attached window is
    /// used when available; a reflection lookup of the service locator is a fallback for the
    /// case where the window isn't attached yet.</summary>
    Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        if (_window?.Clipboard is { } windowClipboard)
            return windowClipboard;

        try
        {
            var locatorType = typeof(Avalonia.Input.Platform.IClipboard).Assembly
                .GetType("Avalonia.AvaloniaLocator");
            if (locatorType is null)
                return null;
            // CurrentMutable is the concrete AvaloniaLocator; resolve its public GetService(Type)
            // from the type itself (not the runtime instance type) for reliability.
            object? locator = locatorType
                .GetProperty("CurrentMutable", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?
                .GetValue(null);
            if (locator is null)
                return null;
            object? service = locatorType
                .GetMethod("GetService", [typeof(object)])!
                .Invoke(locator, [typeof(Avalonia.Input.Platform.IClipboard)]);
            return service as Avalonia.Input.Platform.IClipboard;
        }
        catch
        {
            return null;
        }
    }
}
