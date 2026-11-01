using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NinferLauncher_v_2.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public SettingsViewModel Settings { get; }
    public ServerViewModel Server { get; }
    public ChatViewModel Chat { get; }
    public HistoryViewModel History { get; }

    [ObservableProperty] object? _currentPage;
    [ObservableProperty] int _section;

    public MainViewModel(Window window)
    {
        Settings = new SettingsViewModel();
        Settings.LoadFromStore();
        Server = new ServerViewModel(Settings);
        Server.AttachWindow(window);
        Chat = new ChatViewModel();
        History = new HistoryViewModel();
        Section = 0;
        CurrentPage = Server; // open the server launcher on startup
    }

    public bool IsServerSelected { get => Section == 0; set { if (value) Section = 0; } }
    public bool IsChatSelected { get => Section == 1; set { if (value) Section = 1; } }
    public bool IsHistorySelected { get => Section == 2; set { if (value) Section = 2; } }
    public bool IsSettingsSelected { get => Section == 3; set { if (value) Section = 3; } }

    partial void OnSectionChanged(int value)
    {
        CurrentPage = value switch
        {
            1 => Chat,
            2 => History,
            3 => Settings,
            _ => Server,
        };
        OnPropertyChanged(nameof(IsServerSelected));
        OnPropertyChanged(nameof(IsChatSelected));
        OnPropertyChanged(nameof(IsHistorySelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
    }

    /// <summary>On window close – saves settings and stops the server.</summary>
    public void OnWindowClosing()
    {
        Settings.Save();
        Server.OnWindowClosing();
    }
}