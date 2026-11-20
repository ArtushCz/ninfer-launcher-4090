using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinferLauncher_v_2.Models;

namespace NinferLauncher_v_2.ViewModels;

/// <summary>Slack-style chat with the Ninfer server: a list of active chats on the left
/// (each chat names itself after the first message sent), the conversation window on the
/// right and a chat box on the bottom. Two chatters: <c>user</c> and <c>AI</c>.</summary>
public partial class ChatViewModel : ViewModelBase
{
    readonly SettingsViewModel _settings;
    static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public ObservableCollection<ChatChannel> Chats { get; } = new();

    /// <summary>Copies <see cref="Chats"/> through the <see cref="SearchQuery"/>
    /// filter – what the "Aktuální" list in the sidebar actually shows.</summary>
    public ObservableCollection<ChatChannel> FilteredChats { get; } = new();

    [ObservableProperty] ChatChannel? _selectedChat;
    [ObservableProperty] string _inputText = "";
    [ObservableProperty] bool _isWaiting;
    [ObservableProperty] bool _showEmptyHint;
    [ObservableProperty] string _searchQuery = "";
    [ObservableProperty] bool _searchOpen;

    /// <summary>Raised (on the UI thread) when the view should scroll to the latest message.</summary>
    public event Action? BottomRequested;

    public bool CanSendMessage => !IsWaiting && !string.IsNullOrWhiteSpace(InputText);

    public ChatViewModel(SettingsViewModel settings)
    {
        _settings = settings;
        NewChatCommand.Execute(null);
    }

    partial void OnSelectedChatChanged(ChatChannel? value) => UpdateEmptyHint();
    partial void OnInputTextChanged(string value) => OnPropertyChanged(nameof(CanSendMessage));
    partial void OnIsWaitingChanged(bool value) => OnPropertyChanged(nameof(CanSendMessage));
    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSearchOpenChanged(bool value)
    {
        if (!value && SearchQuery.Length > 0)
            SearchQuery = "";
    }

    [RelayCommand]
    private void ToggleSearch() => SearchOpen = !SearchOpen;

    [RelayCommand]
    private void NewChat()
    {
        var chat = new ChatChannel();
        Chats.Insert(0, chat);
        SearchOpen = false;
        SearchQuery = "";
        ApplyFilter();
        SelectedChat = chat;
        UpdateEmptyHint();
    }

    /// <summary>Rebuilds <see cref="FilteredChats"/> from <see cref="Chats"/>
    /// using the current search query (empty query = everything).</summary>
    void ApplyFilter()
    {
        FilteredChats.Clear();
        var q = SearchQuery?.Trim() ?? "";
        foreach (var chat in Chats)
        {
            if (q.Length == 0 ||
                (chat.DisplayName + " " + chat.Preview)
                    .Contains(q, StringComparison.OrdinalIgnoreCase))
                FilteredChats.Add(chat);
        }
    }

    /// <summary>Enter key in the chat box: sends when possible, otherwise the user
    /// can keep typing a new line (Shift+Enter).</summary>
    public bool SendFromEnter()
    {
        if (!CanSendMessage)
            return false;
        SendMessageCommand.Execute(null);
        return true;
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsWaiting)
            return;

        if (SelectedChat is null)
            NewChat();
        var chat = SelectedChat!;

        InputText = "";
        chat.Messages.Add(new ChatMessage("user", text));
        if (!chat.HasName)
            chat.RenameFromFirstMessage(text);
        chat.Preview = PreviewOf(text);
        UpdateEmptyHint();
        RequestBottom();

        var pending = new ChatMessage("AI", "…", isPending: true);
        chat.Messages.Add(pending);
        RequestBottom();

        IsWaiting = true;
        try
        {
            var reply = await RequestReplyAsync(chat);
            chat.Messages.Remove(pending);
            chat.Messages.Add(new ChatMessage("AI", reply));
        }
        catch (Exception ex)
        {
            chat.Messages.Remove(pending);
            chat.Messages.Add(new ChatMessage("AI", $"[ERROR] {ex.Message}", isError: true));
        }
        finally
        {
            IsWaiting = false;
            RequestBottom();
        }
    }

    /// <summary>Asks the running Ninfer server (OpenAI-compatible API) for a reply.</summary>
    async Task<string> RequestReplyAsync(ChatChannel chat)
    {
        var port = int.TryParse(_settings.Port, out var p) ? p : 11434;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/v1/chat/completions");
        if (!string.IsNullOrWhiteSpace(_settings.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);

        var body = new
        {
            model = _settings.ModelFile,
            stream = false,
            messages = BuildRoles(chat),
        };
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var response = await _http.SendAsync(request, cts.Token).ConfigureAwait(true);
        var payload = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(true);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"server returned {(int)response.StatusCode} {response.StatusCode}");

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var message = choices[0];
            if (message.TryGetProperty("message", out var m) &&
                m.TryGetProperty("content", out var content) &&
                !string.IsNullOrWhiteSpace(content.GetString()))
            {
                return content.GetString()!;
            }
        }
        throw new Exception("empty answer from server");
    }

    /// <summary>Chronological user/assistant roles for the API (skips pending/error lines).</summary>
    static System.Collections.Generic.List<object> BuildRoles(ChatChannel chat)
    {
        var roles = new System.Collections.Generic.List<object>();
        foreach (var m in chat.Messages)
        {
            if (m.IsPending || m.IsError)
                continue;
            roles.Add(new { role = m.IsUser ? "user" : "assistant", content = m.Text });
        }
        return roles;
    }

    void UpdateEmptyHint()
    {
        ShowEmptyHint = SelectedChat is null || SelectedChat.Messages.Count == 0;
    }

    void RequestBottom() => BottomRequested?.Invoke();

    static string PreviewOf(string text)
    {
        var clean = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (clean.Length > 60)
            clean = clean[..59].TrimEnd() + "…";
        return clean;
    }
}