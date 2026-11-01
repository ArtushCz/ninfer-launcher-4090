using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;

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

/// <summary>Colour-coded section a profile is shown under in the profile dropdown.</summary>
public enum ProfileGroup
{
    Recommended, // green – "Recommended and tested"
    Usable,      // yellow – "Usable and tested"
    Bench,       // blue – "Benchmarks"
    Untested,    // red – "Not tested / not working"
}

/// <summary>Launch profile – pre-prepared combinations (measured RTX 4090 24 GB limits).</summary>
public sealed record ServerProfile(
    string Name,
    int MaxContext,
    bool MtpSpec,
    int DraftTokens,
    string KvDtype,
    VisionMode Vision,
    bool IsBench = false,
    ProfileGroup Group = ProfileGroup.Untested)
{
    public string ExeName => IsBench ? "ninfer_bench.exe" : "ninfer-serve.exe";

    /// <summary>Warning: contexts above 240K may not work on Windows (WDDM).</summary>
    public bool WarnHighContext => MaxContext > 245760;
}

/// <summary>One row of the profile dropdown: a profile with its group's faint "undertone" tint.</summary>
public sealed class ProfileRow
{
    public string Title { get; }
    public ServerProfile? Profile { get; }
    public int AllIndex { get; }
    public Brush Band { get; }   // faint group-coloured background

    internal ProfileRow(string title, Color color, ServerProfile profile, int allIndex)
    {
        Title = title;
        Profile = profile;
        AllIndex = allIndex;
        Band = new SolidColorBrush(color) { Opacity = 0.2 }; // faint group undertone
    }
}

public static class ServerProfiles
{
    /// <summary>Safe limit – contexts above this may stop working on Windows (WDDM).</summary>
    public const int SafeContextLimit = 245760;

    public static readonly ServerProfile[] All =
    {
        // ===== Default: 240K + MTP3 (vision 4k) =====
        new("Agent - 240K Context + MTP3", 245760, true, 3, "rk2v4-e8", VisionMode.Tokens4096, false, ProfileGroup.Recommended),
        // ===== Default-text: 64K Context + MTP4 =====
        new("Text - 64K Context + MTP4", 65_536, true, 4, "rk2v4-e8", VisionMode.None, false, ProfileGroup.Recommended),
        
        // ===== Fast • low context (less VRAM → faster prefill, higher tok/s) =====
        new("Fast 16K • rk2v4-e8 (2-bit) • MTP4", 16_384, true, 4, "rk2v4-e8", VisionMode.None, false, ProfileGroup.Usable),
        new("Fast 32K • rk2v4-e8 (2-bit) • MTP4", 32_768, true, 4, "rk2v4-e8", VisionMode.None, false, ProfileGroup.Usable),
        new("Fast 128K • rk2v4-e8 (2-bit) • MTP4", 131_072, true, 4, "rk2v4-e8", VisionMode.None, false, ProfileGroup.Usable),
        new("Fast 64K • int8 (INT8) • MTP4", 65_536, true, 4, "int8", VisionMode.None, false, ProfileGroup.Usable),
        
        // ===== Benchmarks (blue) — run via ninfer_bench.exe =====
        new("Bench 64K • rk2v4-e8 • MTP4", 65_536, true, 4, "rk2v4-e8", VisionMode.None, true, ProfileGroup.Bench),
        new("Bench 32K • rk2v4-e8 • MTP4", 32_768, true, 4, "rk2v4-e8", VisionMode.None, true, ProfileGroup.Bench),
        new("Bench 16K • rk2v4-e8 • MTP4", 16_384, true, 4, "rk2v4-e8", VisionMode.None, true, ProfileGroup.Bench),
        new("Bench 240K • rk2v4-e8 • MTP3", 245_760, true, 3, "rk2v4-e8", VisionMode.None, true, ProfileGroup.Bench),
        
        
        /*not verified*/
       // ===== Text-only • no speculation (MTP0) =====
        new("Text 460K • rk2v4-e8 (2-bit E8) • MTP0", 460_000, false, 0, "rk2v4-e8", VisionMode.None),
        new("Text 360K • rk4v4-e8 (4-bit E8) • MTP0", 360_000, false, 0, "rk4v4-e8", VisionMode.None),
        new("Text 360K • rk4v4 (Hadamard 4-bit) • MTP0", 360_000, false, 0, "rk4v4", VisionMode.None),
        new("Text 230K • rk8v4 (Hadamard 8-bit) • MTP0", 230_000, false, 0, "rk8v4", VisionMode.None),
        new("Text 160K • int8 (INT8) • MTP0", 160_000, false, 0, "int8", VisionMode.None),

        // ===== Text-only • MTP4 speculation =====
        new("Text 390K • rk2v4-e8 • MTP4", 390_000, true, 4, "rk2v4-e8", VisionMode.None),
        new("Text 280K • rk4v4-e8 • MTP4", 280_000, true, 4, "rk4v4-e8", VisionMode.None),
        new("Text 280K • rk4v4 (Hadamard 4-bit) • MTP4", 280_000, true, 4, "rk4v4", VisionMode.None),
        new("Text 170K • int8 (INT8) • MTP4", 170_000, true, 4, "int8", VisionMode.None),
        new("Text 120K • int8 (INT8) • MTP4", 120_000, true, 4, "int8", VisionMode.None),

        // ===== Vision 8k (8192 vision tokens) • MTP4 =====
        new("Vision8k 340K • rk2v4-e8 • MTP4", 340_000, true, 4, "rk2v4-e8", VisionMode.Tokens8192),
        new("Vision8k 240K • rk4v4-e8 • MTP4", 240_000, true, 4, "rk4v4-e8", VisionMode.Tokens8192),
        new("Vision8k 100K • int8 (INT8) • MTP4", 100_000, true, 4, "int8", VisionMode.Tokens8192),

        // ===== Vision 4k small (4096 vision tokens) • MTP4 =====
        new("Vision4k 360K • rk2v4-e8 • MTP4", 360_000, true, 4, "rk2v4-e8", VisionMode.Tokens4096),
        new("Vision4k 260K • rk4v4-e8 • MTP4", 260_000, true, 4, "rk4v4-e8", VisionMode.Tokens4096),
        
        

       
    };

    /// <summary>Dropdown rows – a flat list; each row carries its group's faint "undertone" tint.</summary>
    public static IReadOnlyList<ProfileRow> DisplayRows => _displayRows ??= BuildDisplayRows();
    static IReadOnlyList<ProfileRow>? _displayRows;

    static IReadOnlyList<ProfileRow> BuildDisplayRows()
    {
        var rows = new List<ProfileRow>(All.Length);
        for (int i = 0; i < All.Length; i++)
            rows.Add(new ProfileRow(All[i].Name, GroupColor(All[i].Group), All[i], i));
        return rows;
    }

    public static Color GroupColor(ProfileGroup g) => g switch
    {
        ProfileGroup.Recommended => Color.FromRgb(0x6A, 0x99, 0x55), // green
        ProfileGroup.Usable => Color.FromRgb(0xC8, 0xA2, 0x4B),      // amber
        ProfileGroup.Bench => Color.FromRgb(0x56, 0x9C, 0xD6),       // blue
        _ => Color.FromRgb(0xA5, 0x42, 0x42),                        // red
    };

    /// <summary>Arguments for the selected profile. Bench profiles target <c>ninfer_bench.exe</c>
    /// (a different CLI); everything else targets <c>ninfer-serve.exe</c>.
    /// <paramref name="serverDir"/> is the folder that holds the ninfer binaries – the bench
    /// reads its token-id corpus from <c>&lt;serverDir&gt;\bench\fixtures\bench_corpus.ids</c>.</summary>
    public static List<string> BuildArgs(ServerProfile profile, string modelPath, int port, string serverDir)
    {
        if (profile.IsBench)
            return BuildBenchArgs(profile, modelPath, serverDir);

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

    /// <summary>
    /// <c>ninfer_bench.exe</c> uses its own CLI (not the server's). Each speed-test profile
    /// measures prefill at <see cref="ServerProfile.MaxContext"/> tokens plus a 128-token
    /// decode (tg128). Corpus defaults to <c>&lt;serverDir&gt;\bench\fixtures\bench_corpus.ids</c>.
    /// </summary>
    private static List<string> BuildBenchArgs(ServerProfile profile, string modelPath, string serverDir)
    {
        string corpusPath = Path.Combine(serverDir, "bench", "fixtures", "bench_corpus.ids");

        var args = new List<string>
        {
            "--weights", modelPath,
            "--corpus", corpusPath,
            "--kv-dtype", MapBenchKvDtype(profile.KvDtype),
        };

        if (profile.MtpSpec && profile.DraftTokens > 0)
        {
            args.Add("--mtp-draft-tokens");
            args.Add(profile.DraftTokens.ToString());
            args.Add("--lm-head-draft");
        }

        // Prefill at the profile's context size + a 128-token decode (tg128).
        args.Add("-p");
        args.Add(profile.MaxContext.ToString());
        args.Add("-n");
        args.Add("128");

        // Context must be able to hold the prefill; lean on WDDM budgeting on the 24 GB card.
        args.Add("--max-ctx");
        args.Add((profile.MaxContext + 4096).ToString());
        args.Add("--prefill-chunk");
        args.Add("1024");
        args.Add("--wddm-evictable-budget");

        // Fewer repetitions for deep-context tests so the run finishes in a reasonable time.
        args.Add("--repetitions");
        args.Add(profile.MaxContext > 131072 ? "2" : "5");
        args.Add("--warmup");
        args.Add("1");

        return args;
    }

    /// <summary><c>ninfer_bench.exe</c> accepts <c>bf16 | int8 | rk8v4 | rk4v4 | rk4v4-e8 |
    /// rk2v4-e8</c> – pass the serve-side KV type straight through and map the few it lacks
    /// onto the nearest supported value.</summary>
    private static string MapBenchKvDtype(string kv) => kv switch
    {
        "bf16" or "int8" or "rk8v4" or "rk4v4" or "rk4v4-e8" or "rk2v4-e8" => kv,
        "rk2v4" => "rk2v4-e8",
        "e8" => "rk4v4-e8",
        _ => "int8",
    };
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