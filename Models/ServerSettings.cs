using System;
using System.Collections.Generic;

namespace NinferLauncher_v_2.Models;

/// <summary>Saved settings (persisted to %AppData%\NinferLauncher\settings.json).</summary>
public class ServerSettings
{
    public string ServerDir { get; set; } = @"F:\LLM\ninfer-rtx4090-windows-x64-v1.2.0";
    public string ModelFile { get; set; } = "Qwen3.8-27B-Uncensored.ninfer";
    public string Port { get; set; } = "11434";
    public string Token { get; set; } = "";
    public int ProfileIndex { get; set; }
}

/// <summary>Vision token mode (see CustomProfiles.txt: Text-Only / Vision 4k / Vision 8k).</summary>
public enum VisionMode
{
    None,
    Tokens4096,
    Tokens8192,
}

/// <summary>Launch profile – pre-prepared combinations from CustomProfiles.txt (measured RTX 4090 24 GB limits, "Recommended Safe Context").</summary>
public sealed record ServerProfile(
    string Name,
    int MaxContext,
    bool MtpSpec,
    int DraftTokens,
    string KvDtype,
    VisionMode Vision,
    bool IsBench = false)
{
    public string ExeName => IsBench ? "ninfer_bench.exe" : "ninfer-serve.exe";

    /// <summary>Warning: contexts above 240K may not work on Windows (WDDM).</summary>
    public bool WarnHighContext => MaxContext > 245760;
}

public static class ServerProfiles
{
    /// <summary>Safe limit – contexts above this may stop working on Windows (WDDM).</summary>
    public const int SafeContextLimit = 245760;

    public static readonly ServerProfile[] All =
    {
        // ===== Default: 240K + MTP3 (vision 4k) =====
        new("240K Context + MTP3 [rk2v4-e8] – default", 245760, true, 3, "rk2v4-e8", VisionMode.Tokens4096),
        new("Bench: 240K Context + MTP3 [rk2v4-e8]", 245760, true, 3, "rk2v4-e8", VisionMode.Tokens4096, true),

        // ===== Text-only • no speculation (MTP0) =====
        new("Text 500K • rk2v4-e8 (2-bit E8) • MTP0", 500_000, false, 0, "rk2v4-e8", VisionMode.None),
        new("Text 400K • rk4v4-e8 (4-bit E8) • MTP0", 400_000, false, 0, "rk4v4-e8", VisionMode.None),
        new("Text 400K • rk4v4 (Hadamard 4-bit) • MTP0", 400_000, false, 0, "rk4v4", VisionMode.None),
        new("Text 270K • rk8v4 (Hadamard 8-bit) • MTP0", 270_000, false, 0, "rk8v4", VisionMode.None),
        new("Text 200K • int8 (INT8) • MTP0", 200_000, false, 0, "int8", VisionMode.None),

        // ===== Text-only • MTP4 speculation =====
        new("Text 430K • rk2v4-e8 • MTP4", 430_000, true, 4, "rk2v4-e8", VisionMode.None),
        new("Text 320K • rk4v4-e8 • MTP4", 320_000, true, 4, "rk4v4-e8", VisionMode.None),
        new("Text 320K • rk4v4 (Hadamard 4-bit) • MTP4", 320_000, true, 4, "rk4v4", VisionMode.None),
        new("Text 210K • rk8v4 (Hadamard 8-bit) • MTP4", 210_000, true, 4, "rk8v4", VisionMode.None),
        new("Text 160K • int8 (INT8) • MTP4", 160_000, true, 4, "int8", VisionMode.None),

        // ===== Vision 8k (8192 vision tokens) • MTP4 =====
        new("Vision8k 380K • rk2v4-e8 • MTP4", 380_000, true, 4, "rk2v4-e8", VisionMode.Tokens8192),
        new("Vision8k 280K • rk4v4-e8 • MTP4", 280_000, true, 4, "rk4v4-e8", VisionMode.Tokens8192),
        new("Vision8k 140K • int8 (INT8) • MTP4", 140_000, true, 4, "int8", VisionMode.Tokens8192),

        // ===== Vision 4k small (4096 vision tokens) • MTP4 =====
        new("Vision4k 400K • rk2v4-e8 • MTP4", 400_000, true, 4, "rk2v4-e8", VisionMode.Tokens4096),
        new("Vision4k 300K • rk4v4-e8 • MTP4", 300_000, true, 4, "rk4v4-e8", VisionMode.Tokens4096),
    };

    /// <summary>Arguments for ninfer-serve.exe / ninfer_bench.exe based on the selected profile.</summary>
    public static List<string> BuildArgs(ServerProfile profile, string modelPath, int port)
    {
        var args = new List<string>
        {
            modelPath,
            "--port", port.ToString(),
        };
        if (profile.Vision != VisionMode.None)
        {
            args.Add("--vision");
            args.Add("--vision-max-tokens");
            args.Add(profile.Vision == VisionMode.Tokens8192 ? "8192" : "4096");
        }
        args.AddRange(new[]
        {
            "--kv-dtype", profile.KvDtype,
            "--max-context", profile.MaxContext.ToString(),
            "--kv-capacity", profile.MaxContext.ToString(),
            "--wddm-evictable-budget",
            "--preserve-thinking",
            "--max-concurrency", "1",
            "--max-pending-requests", "16",
            "--pending-timeout-ms", "600000",
            "--prefill-chunk", "1024",
        });
        if (profile.MtpSpec)
            args.AddRange(new[] { "--spec", "mtp", "--draft-tokens", profile.DraftTokens.ToString(), "--lm-head-draft" });
        return args;
    }
}

/// <summary>A single line of output in the terminal.</summary>
public class TerminalLine
{
    public string Text { get; }
    public bool IsError { get; }
    public Avalonia.Media.ISolidColorBrush Brush { get; }

    static readonly Avalonia.Media.SolidColorBrush TextBrush = new(Avalonia.Media.Color.Parse("#C5C8C6"));
    static readonly Avalonia.Media.SolidColorBrush ErrorBrush = new(Avalonia.Media.Color.Parse("#A54242"));

    public TerminalLine(string text, bool isError = false)
    {
        Text = text;
        IsError = isError;
        Brush = isError ? ErrorBrush : TextBrush;
    }
}