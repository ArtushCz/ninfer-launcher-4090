using System;
using System.IO;
using System.Text.Json;
using NinferLauncher_v_2.Models;

namespace NinferLauncher_v_2.Services;

/// <summary>Saves / loads settings to a local <c>settings.json</c> file next to the exe.
/// If the directory containing the exe is not writable (e.g. a read-only folder), it
/// falls back to <c>%AppData%\NinferLauncher\settings.json</c>.</summary>
public static class SettingsStore
{
    static readonly string ExeDir = AppContext.BaseDirectory;
    static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NinferLauncher");

    /// <summary>Whether the directory next to the exe is writable (probed once on first use).</summary>
    static bool? _localWritable;

    static bool LocalWritable
    {
        get
        {
            if (_localWritable is null)
            {
                try
                {
                    var probe = Path.Combine(ExeDir, ".settings_probe_" + Guid.NewGuid().ToString("N"));
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    _localWritable = true;
                }
                catch
                {
                    _localWritable = false;
                }
            }
            return _localWritable.Value;
        }
    }

    static string Dir => LocalWritable ? ExeDir : AppDataDir;
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static ServerSettings Load()
    {
        // Prefer the local file next to the exe; then the %AppData% file (migration
        // for users upgrading from a version that saved settings there).
        var candidates = new[]
        {
            Path.Combine(ExeDir, "settings.json"),
            Path.Combine(AppDataDir, "settings.json"),
        };

        foreach (var candidate in candidates)
        {
            try
            {
                if (!File.Exists(candidate))
                    continue;

                var loaded = Deserialize(File.ReadAllText(candidate));
                if (loaded == null)
                    continue;

                if (Path.GetFullPath(candidate) != Path.GetFullPath(FilePath))
                    Save(loaded); // migrate the file to the current location
                return loaded;
            }
            catch
            {
                // corrupted file – try the next candidate
            }
        }

        return new ServerSettings();
    }

    static ServerSettings? Deserialize(string json) =>
        JsonSerializer.Deserialize<ServerSettings>(json);

    public static void Save(ServerSettings s)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // not fatal
        }
    }
}