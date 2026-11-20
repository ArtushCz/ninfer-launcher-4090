using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using NinferLauncher_v_2.Models;

namespace NinferLauncher_v_2.Services;

public enum ServerState
{
    Stopped,
    Starting,
    Running,
    Stopping,
}

/// <summary>
/// Manages the Ninfer server process (equivalent to RunNinfer.bat) –
/// starts ninfer-serve.exe / ninfer_bench.exe with the profile arguments,
/// captures stdout/stderr and can kill the process (including the process tree).
/// Events are raised on worker threads!
/// </summary>
public class ServerService : IDisposable
{
    Process? _process;
    readonly object _sync = new();

    public ServerState State { get; private set; } = ServerState.Stopped;
    public string? LastError { get; set; }

    /// <summary>New output line (stdout/stderr) – raised from a background thread.</summary>
    public event Action<string>? OutputLine;
    /// <summary>Server state change – may be raised from any thread.</summary>
    public event Action? StateChanged;

    public void Start(string dir, string modelPath, int port, ServerProfile profile, bool wddmEvictableBudget)
    {
        lock (_sync)
        {
            KillProcess();
            LastError = null;

            var exe = Path.Combine(dir, profile.ExeName);
            if (!File.Exists(exe))
            {
                LastError = $"File not found: {exe}";
                SetState(ServerState.Stopped);
                return;
            }

            if (profile.IsBench)
            {
                string? corpusError = EnsureBenchCorpus(dir);
                if (corpusError != null)
                {
                    LastError = corpusError;
                    SetState(ServerState.Stopped);
                    return;
                }

                // Free as much VRAM as possible before loading the model: clear any orphaned
                // Ninfer processes, then give the driver a moment to release the memory.
                KillOrphanNinfer();
                Thread.Sleep(1200);
            }

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = dir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var a in ServerProfiles.BuildArgs(profile, modelPath, port, dir, wddmEvictableBudget))
                psi.ArgumentList.Add(a);

            var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) OutputLine?.Invoke(e.Data);
            };
            p.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) OutputLine?.Invoke(e.Data);
            };
            p.Exited += (_, _) => SetState(ServerState.Stopped);

            try
            {
                p.Start();
                _process = p;
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                SetState(ServerState.Running);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                SetState(ServerState.Stopped);
            }
        }
    }

    /// <summary>Stages the bundled bench corpus next to the server binaries so
    /// <c>ninfer_bench.exe</c> can find it (its default is <c>bench/fixtures/bench_corpus.ids</c>
    /// relative to the working directory). Returns an error message, or null on success.</summary>
    static string? EnsureBenchCorpus(string serverDir)
    {
        string fixturesDir = Path.Combine(serverDir, "bench", "fixtures");
        string corpusDest = Path.Combine(fixturesDir, "bench_corpus.ids");
        if (File.Exists(corpusDest))
            return null; // already staged

        string baseDir = AppContext.BaseDirectory;
        string corpusSrc = Path.Combine(baseDir, "bench_corpus.ids");
        if (!File.Exists(corpusSrc))
            return "Bench corpus not found: expected 'bench_corpus.ids' next to the launcher. Rebuild/redeploy so the corpus is included.";

        try
        {
            Directory.CreateDirectory(fixturesDir);
            File.Copy(corpusSrc, corpusDest, overwrite: true);
            // The manifest is optional; stage it in case a bench build reads it.
            string manifestSrc = Path.Combine(baseDir, "bench_corpus.manifest.json");
            if (File.Exists(manifestSrc))
                File.Copy(manifestSrc, Path.Combine(fixturesDir, "bench_corpus.manifest.json"), overwrite: true);
            return null;
        }
        catch (Exception ex)
        {
            return $"Could not stage the bench corpus: {ex.Message}";
        }
    }

    /// <summary>Best-effort kill of any stray ninfer-serve / ninfer_bench processes so a
    /// benchmark can load the model into otherwise-occupied VRAM.</summary>
    static void KillOrphanNinfer()
    {
        foreach (var name in new[] { "ninfer-serve.exe", "ninfer_bench.exe" })
        {
            try
            {
                Process.Start(new ProcessStartInfo("taskkill", $"/f /im {name}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
            }
            catch
            {
                // not running / taskkill unavailable – fine
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            KillProcess();
        }
        // like in the BAT file – also kill orphaned processes
        Task.Run(() =>
        {
            foreach (var name in new[] { "ninfer-serve", "ninfer_bench" })
            {
                try
                {
                    Process.Start(new ProcessStartInfo("taskkill", $"/f /im {name}.exe")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    });
                }
                catch
                {
                    // process is not running – that's fine
                }
            }
        });
        SetState(ServerState.Stopped);
    }

    public bool IsRunning => _process is { HasExited: false };

    void KillProcess()
    {
        var p = _process;
        _process = null;
        try
        {
            if (p is { HasExited: false })
                p.Kill(true); // kills the process tree too
        }
        catch
        {
            // already exited / was not started
        }
    }

    void SetState(ServerState s)
    {
        if (State == s)
            return;
        State = s;
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            KillProcess();
        }
    }
}