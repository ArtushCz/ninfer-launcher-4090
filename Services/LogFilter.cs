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

    /// <summary>vLLM's periodic engine stats line, e.g.
    /// <c>Avg prompt throughput: 12.34 tokens/s, Avg generation throughput: 56.78 tokens/s, …</c></summary>
    static readonly Regex VllmThroughputRx = new(
        @"avg\s+prompt\s+throughput:\s+([\d.,]+)\s*tokens/s.*?avg\s+generation\s+throughput:\s+([\d.,]+)\s*tokens/s",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>True if the line is a "throughput" metric (no text for the terminal)
    /// – matches both the Ninfer engine line and vLLM's periodic stats line.</summary>
    public static bool IsThroughputLine(string line) =>
        line.Contains("throughput", StringComparison.Ordinal);

    public static bool TryParseThroughput(string line, out double prefill, out double decode)
    {
        prefill = decode = 0;
        if (ThroughputRx.IsMatch(line))
        {
            var m = ThroughputRx.Match(line);
            prefill = double.Parse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            decode = double.Parse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            return true;
        }
        if (VllmThroughputRx.IsMatch(line))
        {
            var m = VllmThroughputRx.Match(line);
            prefill = ParseLocaleSafe(m.Groups[1].Value);
            decode = ParseLocaleSafe(m.Groups[2].Value);
            return true;
        }
        return false;
    }

    /// <summary>vLLM formats numbers with the system culture – accept a comma decimal point.</summary>
    static double ParseLocaleSafe(string value) =>
        double.Parse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Lines that should not go to the terminal – the useful state is already provided by the tok/s chip, or they are pure noise.</summary>
    public static bool IsNoisy(string line)
    {
        if (line.Contains("prompt-cache", StringComparison.Ordinal)) return true;
        if (line.Contains("warming up", StringComparison.Ordinal)) return true;
        if (line.Contains("KV capacity", StringComparison.Ordinal)) return true;
        if (line.Contains("state pools", StringComparison.Ordinal)) return true;
        // weight-loading progress (0 % … 100 %) – replaces "model loaded in Xs";
        // the "weights" check covers Ninfer, "checkpoint shards" covers vLLM's safetensors bar
        if ((line.Contains("weights", StringComparison.Ordinal) ||
             line.Contains("checkpoint shards", StringComparison.Ordinal)) &&
            line.Contains("%", StringComparison.Ordinal)) return true;
        return false;
    }
}