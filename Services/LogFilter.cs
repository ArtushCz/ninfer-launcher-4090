using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NinferLauncher_v_2.Services;

/// <summary>
/// Filters ninfer-serve.exe output: strips noise (prompt-cache, load-progress,
/// KV capacity, state pools, warming up) and, instead of writing "throughput"
/// lines (prefill/decode tok/s) every 5 s to the terminal, parses them.
/// </summary>
public static class LogFilter
{
    static readonly Regex ThroughputRx = new(
        @"throughput\s+interval=[\d.]+s\s+prefill=([\d.]+)tok/s\s+decode=([\d.]+)tok/s",
        RegexOptions.Compiled);

    /// <summary>True if the line is a "throughput" metric (no text for the terminal).</summary>
    public static bool IsThroughputLine(string line) =>
        line.Contains("throughput", StringComparison.Ordinal);

    public static bool TryParseThroughput(string line, out double prefill, out double decode)
    {
        prefill = decode = 0;
        if (!ThroughputRx.IsMatch(line))
            return false;
        var m = ThroughputRx.Match(line);
        prefill = double.Parse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        decode = double.Parse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Lines that should not go to the terminal – the useful state is already provided by the tok/s chip, or they are pure noise.</summary>
    public static bool IsNoisy(string line)
    {
        if (line.Contains("prompt-cache", StringComparison.Ordinal)) return true;
        if (line.Contains("warming up", StringComparison.Ordinal)) return true;
        if (line.Contains("KV capacity", StringComparison.Ordinal)) return true;
        if (line.Contains("state pools", StringComparison.Ordinal)) return true;
        // weight-loading progress (0 % … 100 %) – replaces "model loaded in Xs"
        if (line.Contains("weights", StringComparison.Ordinal) && line.Contains("%", StringComparison.Ordinal)) return true;
        return false;
    }
}