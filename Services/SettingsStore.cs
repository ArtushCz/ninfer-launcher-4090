using System;
using System.IO;
using System.Text.Json;
using NinferLauncher_v_2.Models;

namespace NinferLauncher_v_2.Services;

/// <summary>Saves / loads settings to %AppData%\NinferLauncher\settings.json.</summary>
public static class SettingsStore
{
    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NinferLauncher");
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static ServerSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = File.ReadAllText(FilePath);
                var loaded = Deserialize(s);
                if (loaded != null)
                    return loaded;
            }
        }
        catch
        {
            // corrupted file – defaults are used
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