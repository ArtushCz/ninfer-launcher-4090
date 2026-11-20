using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NinferLauncher_v_2.Models;

/// <summary>One chat line. Only two chatters exist: <c>user</c> and <c>AI</c> (Slack-style).</summary>
public sealed class ChatMessage
{
    public string Sender { get; }
    public string Text { get; }
    public DateTime Time { get; }
    public bool IsUser { get; }
    public bool IsError { get; }
    public bool IsPending { get; }

    public string SenderName => IsUser ? "user" : "AI";
    public string TimeText => Time.ToString("HH:mm");
    public string AvatarGlyph => IsUser ? "U" : "AI";
    public string TextDisplay => IsPending ? "… " : Text;

    public static readonly SolidColorBrush UserSenderBrush = new(Color.Parse("#C9CDD1"));
    public static readonly SolidColorBrush AiSenderBrush = new(Color.Parse("#8FBF7F"));
    public static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#A54242"));
    public static readonly SolidColorBrush NormalBrush = new(Color.Parse("#C5C8C6"));
    public static readonly SolidColorBrush UserAvatarBrush = new(Color.Parse("#8A9099"));
    public static readonly SolidColorBrush AiAvatarBrush = new(Color.Parse("#6A9955"));

    /// <summary>User messages sit on a darker gray, AI messages on a slightly lighter one.</summary>
    public static readonly SolidColorBrush UserBubbleBrush = new(Color.Parse("#1A1C1E"));
    public static readonly SolidColorBrush AiBubbleBrush = new(Color.Parse("#26282C"));
    public static readonly SolidColorBrush UserBubbleBorderBrush = new(Color.Parse("#2A2D31"));
    public static readonly SolidColorBrush AiBubbleBorderBrush = new(Color.Parse("#34383D"));

    public ISolidColorBrush SenderBrush => IsUser ? UserSenderBrush : AiSenderBrush;
    public ISolidColorBrush AvatarBrush => IsUser ? UserAvatarBrush : AiAvatarBrush;
    public ISolidColorBrush TextBrush => IsError ? ErrorBrush : NormalBrush;
    public ISolidColorBrush BubbleBrush => IsUser ? UserBubbleBrush : AiBubbleBrush;
    public ISolidColorBrush BubbleBorderBrush => IsUser ? UserBubbleBorderBrush : AiBubbleBorderBrush;

    public ChatMessage(string sender, string text, bool isError = false, bool isPending = false)
    {
        IsUser = sender == "user";
        Sender = IsUser ? "user" : "AI";
        Text = text;
        IsError = isError;
        IsPending = isPending;
        Time = DateTime.Now;
    }
}

/// <summary>One entry in the left chat list. Starts as "New chat" and
/// renames itself after the first message is sent.</summary>
public sealed class ChatChannel : ObservableObject
{
    string _name = "New chat";
    bool _hasName;
    string _preview = "";

    public bool HasName => _hasName;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    /// <summary>Shown name: "New chat" until the first message is sent.</summary>
    public string DisplayName => _hasName ? _name : "New chat";

    /// <summary>Short preview of the latest message (shown under the name in the list).</summary>
    public string Preview
    {
        get => _preview;
        set { _preview = value; OnPropertyChanged(nameof(Preview)); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(DisplayName)); }
    }

    /// <summary>Names the chat after its first message.</summary>
    public void RenameFromFirstMessage(string firstMessage)
    {
        if (_hasName)
            return;
        _hasName = true;

        var clean = firstMessage.Trim().Replace('\n', ' ').Replace('\r', ' ');
        if (clean.Length > 36)
            clean = clean[..36].TrimEnd() + "…";
        Name = clean.Length == 0 ? "New chat" : clean;
    }
}