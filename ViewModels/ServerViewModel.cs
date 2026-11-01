using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using NinferLauncher_v_2.Models;
using NinferLauncher_v_2.Services;

namespace NinferLauncher_v_2.ViewModels;

public static class Palette
{
    public static readonly Avalonia.Media.SolidColorBrush Dimmed = new(Avalonia.Media.Color.Parse("#6E7279"));
    public static readonly Avalonia.Media.SolidColorBrush Running = new(Avalonia.Media.Color.Parse("#6A9955"));
    public static readonly Avalonia.Media.SolidColorBrush Error = new(Avalonia.Media.Color.Parse("#A54242"));
}

/// <summary>Server launcher: settings, start/stop/restart and a live terminal.</summary>
public partial class ServerViewModel : ViewModelBase
{
    readonly SettingsViewModel _settings;
    readonly ServerService _service = new();
    Avalonia.Controls.Window? _window;

    public ObservableCollection<TerminalLine> Terminal { get; } = new();

    [ObservableProperty] bool _isRunning;
    [ObservableProperty] bool _canStop;
    [ObservableProperty] string _statusText = "Stopped";
    [ObservableProperty] ISolidColorBrush _statusBrush = Palette.Dimmed;

    /// <summary>Live throughput readout: "Prefill: X tok/s (avg Y) • Decode: A tok/s (avg B)".</summary>
    [ObservableProperty] string _tpsStatusText = "⚡ Prefill: —  •  Decode: —";
    double _pfCur, _dcCur, _pfAvg, _dcAvg;

    public SettingsViewModel Settings => _settings;

    public string UrlText => _settings.UrlText;

    public ServerViewModel(SettingsViewModel settings)
    {
        _settings = settings;
        _service.OutputLine += line =>
            Dispatcher.UIThread.Post(() => HandleServerLine(line));
        _service.StateChanged += () =>
            Dispatcher.UIThread.Post(UpdateStateUi);
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
            $"⚡ Prefill: {Fmt(_pfCur)} tok/s{(_pfAvg > 0 ? $" (avg {Fmt(_pfAvg)})" : "")}" +
            $"  •  Decode: {Fmt(_dcCur)} tok/s{(_dcAvg > 0 ? $" (avg {Fmt(_dcAvg)})" : "")}";
    }

    void ResetTps()
    {
        _pfCur = _dcCur = _pfAvg = _dcAvg = 0;
        TpsStatusText = "⚡ Prefill: —  •  Decode: —";
    }

    static string Fmt(double v) => v >= 1000
        ? v.ToString("0", System.Globalization.CultureInfo.CurrentCulture)
        : v.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);

    public void AttachWindow(Avalonia.Controls.Window window) => _window = window;

    /// <summary>Application shutdown – stops the server and releases the process.</summary>
    public void OnWindowClosing()
    {
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
        if (IsRunning)
            return;
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
        if (!File.Exists(_settings.FullModelPath))
        {
            AppendLine($"[ERROR] Model not found: {_settings.FullModelPath}", true);
            return;
        }

        var profile = _settings.SelectedProfile;
        AppendLine($"[LAUNCHER] Profile: {profile.Name}");
        if (profile.WarnHighContext)
            AppendLine($"[WARN] Profile {profile.MaxContext / 1000}K tokens is above 240K – it may not work on Windows (WDDM). If the server is not listening on the port, run a lower profile.", true);
        ResetTps();
        AppendLine($"[LAUNCHER] Directory: {dir}");
        AppendLine($"[LAUNCHER] Start: {profile.ExeName} (port {port})");
        AppendLine("-------------------------------------------------------------");
        _service.Start(dir, _settings.FullModelPath, port, profile);
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

    [RelayCommand]
    private void ClearLog() => Terminal.Clear();
}