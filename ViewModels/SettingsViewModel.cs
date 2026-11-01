using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using NinferLauncher_v_2.Models;
using NinferLauncher_v_2.Services;

namespace NinferLauncher_v_2.ViewModels;

/// <summary>Shared settings – used by both the Settings page and the Launcher page.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] string _serverDir;
    [ObservableProperty] string _modelFile;
    [ObservableProperty] string _port;
    [ObservableProperty] string _token;
    [ObservableProperty] int _profileIndex;
    [ObservableProperty] string _statusMessage = "";

    public List<ServerProfile> Profiles { get; } = new(ServerProfiles.All);

    public string FullModelPath => Path.Combine(ServerDir, ModelFile);
    public string UrlText => $"http://127.0.0.1:{Port}";
    public bool IsPortValid => int.TryParse(Port, out _);

    public ServerProfile SelectedProfile =>
        Profiles[Math.Clamp(ProfileIndex, 0, Profiles.Count - 1)];

    public void LoadFromStore()
    {
        var s = SettingsStore.Load();
        ServerDir = s.ServerDir;
        ModelFile = s.ModelFile;
        Port = s.Port;
        Token = s.Token;
        ProfileIndex = s.ProfileIndex;
        OnPropertyChanged(nameof(FullModelPath));
        OnPropertyChanged(nameof(UrlText));
    }

    /// <returns>false if the settings are invalid.</returns>
    public bool Save()
    {
        if (string.IsNullOrWhiteSpace(ServerDir))
        {
            StatusMessage = "Enter a server directory.";
            return false;
        }
        if (!int.TryParse(Port, out var port) || port is < 1 or > 65535)
        {
            StatusMessage = "Enter a valid port (1–65535).";
            return false;
        }
        SettingsStore.Save(new ServerSettings
        {
            ServerDir = ServerDir,
            ModelFile = ModelFile,
            Port = Port,
            Token = Token,
            ProfileIndex = ProfileIndex,
        });
        StatusMessage = "Settings saved ✓";
        return true;
    }

    partial void OnServerDirChanged(string value) => OnPropertyChanged(nameof(FullModelPath));
    partial void OnModelFileChanged(string value) => OnPropertyChanged(nameof(FullModelPath));
    partial void OnPortChanged(string value) => OnPropertyChanged(nameof(UrlText));
    partial void OnProfileIndexChanged(int value) => OnPropertyChanged(nameof(SelectedProfile));

    [RelayCommand]
    void SaveSettings() => Save();
}