using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

namespace NinferLauncher_v_2.Services;

/// <summary>One hardware sample: GPU VRAM used/total, GPU utilisation, system RAM used/total.</summary>
public sealed record HwSample(
    double GpuUsedBytes, double GpuTotalBytes, double GpuUtilPct,
    double RamUsedBytes, double RamTotalBytes, double RamLoadPct);

/// <summary>Polls hardware every 2 seconds. GPU VRAM + utilisation come from
/// <c>nvidia-smi</c> (ships with the driver â€“ exact per-driver numbers; the Windows
/// performance counters / WMI GPU classes double-count across engine instances and
/// report sentinel "totals", which showed 40 GB "used" on a 24 GB card).
/// Without nvidia-smi the WMI per-process <c>GPUProcessMemory</c> sum is used as a
/// rough fallback. System RAM from the native <c>GlobalMemoryStatusEx</c> API
/// (the same "in use" number as Task Manager).</summary>
public sealed class HardwareMonitor : IDisposable
{
    public event Action<HwSample>? SampleReady;

    readonly System.Threading.Timer _timer;
    readonly object _pollGate = new();
    readonly string? _nvidiaSmi;

    public HardwareMonitor()
    {
        _nvidiaSmi = FindNvidiaSmi();
        _timer = new System.Threading.Timer(_ => SafePoll(), null, 500, 2000);
    }

    /// <summary>nvidia-smi.exe ships with the NVIDIA driver â€“ System32 or the NVSMI folder.</summary>
    static string? FindNvidiaSmi()
    {
        string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string p1 = Path.Combine(sys, "nvidia-smi.exe");
        if (File.Exists(p1))
            return p1;
        string p2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        if (File.Exists(p2))
            return p2;
        return null;
    }

    void SafePoll()
    {
        if (!Monitor.TryEnter(_pollGate))
            return;
        try
        {
            SampleReady?.Invoke(Poll());
        }
        catch { /* keep polling */ }
        finally
        {
            Monitor.Exit(_pollGate);
        }
    }

    HwSample Poll()
    {
        var (ramUsed, ramTotal, ramLoad) = Ram();
        if (_nvidiaSmi is not null && TryPollNvidiaSmi(out double gUsed, out double gTotal, out double gUtil))
            return new HwSample(gUsed, gTotal, gUtil, ramUsed, ramTotal, ramLoad);
        return WmiGpuFallback(ramUsed, ramTotal, ramLoad);
    }

    /// <summary>nvidia-smi: one short-lived process, exact VRAM + utilisation of the first (NVIDIA) GPU.</summary>
    bool TryPollNvidiaSmi(out double usedBytes, out double totalBytes, out double utilPct)
    {
        usedBytes = totalBytes = utilPct = 0;
        try
        {
            var psi = new ProcessStartInfo(_nvidiaSmi,
                "--query-gpu=memory.total,memory.used,utilization.gpu --format=csv,noheader,nounits")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null)
                return false;
            string outp = p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            if (!p.WaitForExit(3000))
            {
                try { p.Kill(true); } catch { }
                return false;
            }
            string line = outp.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Contains(',')) ?? "";
            string[] f = line.Split(',');
            if (f.Length < 3 || !int.TryParse(f[0].Trim(), out int totalMb)
             || !int.TryParse(f[1].Trim(), out int usedMb)
             || !int.TryParse(f[2].Trim(), out int util))
                return false;
            totalBytes = totalMb * 1048576.0;
            usedBytes = Math.Min(usedMb, totalMb) * 1048576.0;
            utilPct = Math.Clamp(util, 0, 100);
            return true;
        }
        catch
        {
            return false;
        }
    }
/// <summary>No NVIDIA tooling: sum the per-PROCESS WMI dedicated-usage counters
    /// (per-process â€“ unlike the per-engine counters, a process's VRAM is not counted
    /// once per engine). The total stays 0 = the UI shows "used only".</summary>
    HwSample WmiGpuFallback(double ramUsed, double ramTotal, double ramLoad)
    {
        double used = 0, util = 0;
        try
        {
            using var mem = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Name, DedicatedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory");
            var perAdapter = new System.Collections.Generic.Dictionary<string, double>();
            foreach (ManagementObject o in mem.Get())
            {
                double kb = ToDouble(o["DedicatedUsage"]);
                if (kb <= 0)
                    continue;
                string name = o["Name"]?.ToString() ?? "";
                int l = name.IndexOf("_luid_", StringComparison.Ordinal);
                string key = l >= 0 ? name[l..Math.Min(name.Length, l + 24)] : "all";
                // per-PROCESS instances â€“ summing per adapter is exact
                perAdapter[key] = (perAdapter.TryGetValue(key, out double v) ? v : 0) + kb;
            }
            foreach (double v in perAdapter.Values)
                if (v > used)
                    used = v * 1024.0; // largest adapter's dedicated usage (the dGPU)
        }
        catch { }
        try
        {
            using var eng = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
            var perAdapter = new System.Collections.Generic.Dictionary<string, double>();
            foreach (ManagementObject o in eng.Get())
            {
                double u = ToDouble(o["UtilizationPercentage"]);
                if (u <= 0)
                    continue;
                string name = o["Name"]?.ToString() ?? "";
                int l = name.IndexOf("_luid_", StringComparison.Ordinal);
                int e = name.IndexOf("_eng_", StringComparison.Ordinal);
                string key = l >= 0 && e > l ? name[l..e] : "all";
                if (u > (perAdapter.TryGetValue(key, out double v) ? v : 0))
                    perAdapter[key] = u;
            }
            foreach (double v in perAdapter.Values)
                if (v > util)
                    util = v;
        }
        catch { }
        return new HwSample(used, 0, Math.Clamp(util, 0, 100), ramUsed, ramTotal, ramLoad);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX stat);

    /// <summary>System RAM â€“ "in use" like Task Manager (total â€“ available, not total â€“ free).</summary>
    static (double Used, double Total, double LoadPct) Ram()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref m) || m.ullTotalPhys == 0)
            return (0, 0, 0);
        double total = m.ullTotalPhys;
        double used = Math.Max(0, total - m.ullAvailPhys);
        return (used, total, Math.Clamp(used / total * 100, 0, 100));
    }

    static double ToDouble(object? v)
    {
        try
        {
            if (v is null) return 0;
            return Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }
        catch { return 0; }
    }

    public void Dispose() => _timer.Dispose();
}