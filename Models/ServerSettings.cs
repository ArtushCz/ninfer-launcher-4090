using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Avalonia.Media;

namespace NinferLauncher_v_2.Models;

/// <summary>Saved settings (persisted to a local settings.json next to the exe).</summary>
public class ServerSettings
{
    public string ServerDir { get; set; } = @"F:\LLM\ninfer-rtx4090-windows-x64-v1.2.0";
    public string ModelFile { get; set; } = "Qwen3.8-27B-Uncensored.ninfer";
    public string Port { get; set; } = "11434";
    public string Token { get; set; } = "";
    public int ProfileIndex { get; set; }

    /// <summary>Checked on the server page → pass <c>--wddm-evictable-budget</c> at startup:
    /// the WDDM driver can reclaim evictable VRAM pages under pressure instead of failing.</summary>
    public bool WddmEvictableBudget { get; set; } = true;
}

/// <summary>Vision token mode (see CustomProfiles.txt: Text-Only / Vision 4k / Vision 8k).</summary>
[JsonConverter(typeof(LowercaseEnumConverter<VisionMode>))]
public enum VisionMode
{
    None,
    Tokens4096,
    Tokens8192,
}

/// <summary>Colour-coded section a profile is shown under in the profile dropdown.</summary>
[JsonConverter(typeof(LowercaseEnumConverter<ProfileGroup>))]
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
    /// <summary>Optional comment from profiles.json (not used by the launcher).</summary>
    public string? Note { get; set; }

    public string ExeName => IsBench ? "ninfer_bench.exe" : "ninfer-serve.exe";

    /// <summary>Warning: contexts above 240K may not work on Windows (WDDM).</summary>
    public bool WarnHighContext => MaxContext > ServerProfiles.SafeContextLimit;
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

    /// <summary>User-editable profile list (lives next to the exe, like settings.json).</summary>
    public const string ProfilesFileName = "profiles.json";

    /// <summary>Built-in profiles – used when <c>profiles.json</c> is missing, empty or invalid.</summary>
    public static readonly ServerProfile[] Builtin =
    {
        // ===== Default: 240K + MTP3 (vision 4k) =====
        new("Agent - 240K Context + MTP3", 245760, true, 3, "rk2v4-e8", VisionMode.Tokens4096, false, ProfileGroup.Recommended),
        // ===== Full context: 256K • no speculation (MTP0) =====
        new("Full context 256K • rk2v4-e8 (2-bit) • MTP0", 262144, false, 0, "rk2v4-e8", VisionMode.None, false, ProfileGroup.Recommended),
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

/// <summary>Active profile list: from <c>profiles.json</c> next to the exe,
    /// falling back to <see cref="Builtin"/> when the file is missing or invalid.</summary>
    public static ServerProfile[] All { get; private set; } = Builtin;

    /// <summary>Parse is case-insensitive so hand-edited files (lower-case keys) work too.</summary>
    static readonly JsonSerializerOptions ParseOptions = new() { PropertyNameCaseInsensitive = true };
    static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // allow every BMP printable code point – the file stays human readable, no \uXXXX escapes
        Encoder = JavaScriptEncoder.Create(new[] { new System.Text.Unicode.UnicodeRange(0x20, 0xFFE0) }),
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    static ServerProfiles()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, ProfilesFileName);
            if (File.Exists(path))
            {
                var parsed = JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(path), ParseOptions);
                if (parsed?.Profiles is { Count: > 0 })
                {
                    var loaded = new List<ServerProfile>(parsed.Profiles.Count);
                    foreach (var p in parsed.Profiles)
                    {
                        if (string.IsNullOrWhiteSpace(p.Name) || p.MaxContext <= 0 ||
                            string.IsNullOrWhiteSpace(p.KvDtype))
                            continue; // skip malformed entries, keep the rest
                        var prof = new ServerProfile(
                            p.Name.Trim(), p.MaxContext, p.MtpSpec, p.DraftTokens,
                            p.KvDtype, p.Vision, p.IsBench, p.Group);
                        prof.Note = p.Note;
                        loaded.Add(prof);
                    }

                    if (loaded.Count > 0)
                    {
                        All = loaded.ToArray();
                        return;
                    }
                }
            }
            else
            {
                WriteProfilesFile(path); // first run: create an editable file from the built-ins
            }
        }
        catch
        {
            // unreadable / invalid file – keep the built-in profiles
        }
    }

    /// <summary>Help text written at the top of <c>profiles.json</c>.</summary>
    internal const string ReadmeText =
        "Ninfer launcher – server profiles. Edit this file and restart the launcher.\r\n" +
        "  group   = dropdown section: recommended (green, tested) | usable (amber, tested)\r\n" +
        "            | bench (blue, run via ninfer_bench.exe) | untested (red)\r\n" +
        "  vision  = none | tokens4096 | tokens8192\r\n" +
        "  mtpSpec = true/false – MTP speculation (draftTokens = drafts per step; false means MTP0)\r\n" +
        "  isBench = true → run ninfer_bench.exe instead of ninfer-serve.exe\r\n";

    /// <summary>Writes the built-in profiles as a human-editable <c>profiles.json</c>.</summary>
    internal static void WriteProfilesFile(string path)
    {
        try
        {
            var doc = new ProfileFile
            {
            Readme = ReadmeText,
            Profiles = Builtin.Select(p => new ProfileDto
            {
                Name = p.Name,
                Group = p.Group,
                MaxContext = p.MaxContext,
                KvDtype = p.KvDtype,
                MtpSpec = p.MtpSpec,
                DraftTokens = p.DraftTokens,
                Vision = p.Vision,
                IsBench = p.IsBench,
                Note = p.Note,
            }).ToList(),
            };
            File.WriteAllText(path, SerializeDoc(doc));
        }
        catch
        {
            // read-only directory – run without a local profiles file
        }
    }

    /// <summary>Persists the current <see cref="All"/> list back to <c>profiles.json</c>
    /// (used by the profile editor). Fails silently if the directory is read-only.</summary>
    public static void Persist()
    {
        try
        {
            var doc = new ProfileFile
            {
                Readme = ReadmeText,
                Profiles = All.Select(p => new ProfileDto
                {
                    Name = p.Name,
                    Group = p.Group,
                    MaxContext = p.MaxContext,
                    KvDtype = p.KvDtype,
                    MtpSpec = p.MtpSpec,
                    DraftTokens = p.DraftTokens,
                    Vision = p.Vision,
                    IsBench = p.IsBench,
                    Note = p.Note,
                }).ToList(),
            };
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, ProfilesFileName), SerializeDoc(doc));
        }
        catch
        {
            // not fatal – the in-memory list still works for this session
        }
    }

    static string SerializeDoc(ProfileFile doc)
    {
        // JavaScriptEncoder insists on escaping '+' even though it needs no escaping in JSON
        return JsonSerializer.Serialize(doc, WriteOptions).Replace("\\u002B", "+");
    }

    /// <summary>Replaces <paramref name="original"/> with <paramref name="profile"/> (or appends
    /// a brand-new one) so the launcher's dropdown picks the change up immediately.</summary>
    public static void AddOrReplace(ServerProfile profile, ServerProfile? original)
    {
        var list = All.ToList();
        if (original is not null)
        {
            int i = list.FindIndex(x => ReferenceEquals(x, original));
            if (i >= 0)
            {
                list[i] = profile;
                All = list.ToArray();
                return;
            }
        }
        list.Add(profile);
        All = list.ToArray();
    }

    /// <summary>Invalidates the cached dropdown rows (call after the profile list changed).</summary>
    public static void RefreshDisplayRows() => _displayRows = null;
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
    /// reads its token-id corpus from <c>&lt;serverDir&gt;\bench\fixtures\bench_corpus.ids</c>.
    /// <paramref name="wddmEvictableBudget"/> (ticked on the server page) adds
    /// <c>--wddm-evictable-budget</c>: the WDDM driver may reclaim evictable VRAM pages
    /// under pressure instead of failing.</summary>
    public static List<string> BuildArgs(ServerProfile profile, string modelPath, int port, string serverDir, bool wddmEvictableBudget)
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
            "--preserve-thinking",
            "--max-concurrency", "1",
            "--max-pending-requests", "16",
            "--pending-timeout-ms", "600000",
            "--prefill-chunk", "1024",
        });
        if (wddmEvictableBudget)
            args.Add("--wddm-evictable-budget");
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

/// <summary>A single line of output in the terminal. Selection state is observable so the
/// terminal view can highlight the clicked / Shift+click-selected range.</summary>
public class TerminalLine : System.ComponentModel.INotifyPropertyChanged
{
    public string Text { get; }
    public bool IsError { get; }
    public Avalonia.Media.ISolidColorBrush Brush { get; }

    static readonly Avalonia.Media.SolidColorBrush TextBrush = new(Avalonia.Media.Color.Parse("#C5C8C6"));
    static readonly Avalonia.Media.SolidColorBrush ErrorBrush = new(Avalonia.Media.Color.Parse("#A54242"));
    // subtle blue highlight for a selected line; transparent otherwise (still hit-testable)
    static readonly Avalonia.Media.IBrush SelectionBrush =
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(90, 80, 130, 200));
    static readonly Avalonia.Media.IBrush NoSelectionBrush = Avalonia.Media.Brushes.Transparent;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(BackgroundBrush));
        }
    }

    /// <summary>Row background: a highlight while the line is selected, otherwise transparent.</summary>
    public Avalonia.Media.IBrush BackgroundBrush => IsSelected ? SelectionBrush : NoSelectionBrush;

    public TerminalLine(string text, bool isError = false)
    {
        Text = text;
        IsError = isError;
        Brush = isError ? ErrorBrush : TextBrush;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}

/// <summary>Serializes enum members as lowercase names ("recommended", "tokens4096");
/// deserialization is case-insensitive.</summary>
public sealed class LowercaseEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        if (s is null)
            return (TEnum)Enum.GetValues<TEnum>()[0];
        return (TEnum)Enum.Parse(typeof(TEnum), s, ignoreCase: true);
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString().ToLowerInvariant());
}

/// <summary>Shape of the user-editable <c>profiles.json</c> file (next to the exe).</summary>
public sealed class ProfileFile
{
    /// <summary>Human-readable help text shown at the top of the file (not parsed).</summary>
    public string? Readme { get; set; }
    public List<ProfileDto> Profiles { get; set; } = new();
}

/// <summary>One entry in <c>profiles.json</c> – mirrors <see cref="ServerProfile"/>.</summary>
public sealed class ProfileDto
{
    /// <summary>Shown name in the dropdown.</summary>
    public string Name { get; set; } = "";

    /// <summary>Dropdown section: "recommended" | "usable" | "bench" | "untested".</summary>
    public ProfileGroup Group { get; set; } = ProfileGroup.Untested;

    /// <summary>Max context tokens (also passed as --max-context / --kv-capacity).</summary>
    public int MaxContext { get; set; }

    /// <summary>KV cache dtype: bf16 | int8 | rk2v4-e8 | rk4v4-e8 | rk4v4 | rk8v4.</summary>
    public string KvDtype { get; set; } = "rk2v4-e8";

    /// <summary>true → MTP speculation enabled (with <see cref="DraftTokens"/>).</summary>
    public bool MtpSpec { get; set; }

    /// <summary>Draft tokens per speculation step (used when <see cref="MtpSpec"/> is true).</summary>
    public int DraftTokens { get; set; }

    /// <summary>Vision budget: "none" | "tokens4096" | "tokens8192".</summary>
    public VisionMode Vision { get; set; } = VisionMode.None;

    /// <summary>true → run <c>ninfer_bench.exe</c> instead of <c>ninfer-serve.exe</c>.</summary>
    public bool IsBench { get; set; }

    /// <summary>Free-form comment for yourself (not used by the launcher).</summary>
    public string? Note { get; set; }
}