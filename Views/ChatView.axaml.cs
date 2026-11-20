using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NinferLauncher_v_2.ViewModels;

namespace NinferLauncher_v_2.Views;

public partial class ChatView : UserControl
{
    ChatViewModel? _viewModel;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            var vm = DataContext as ChatViewModel;
            if (ReferenceEquals(_viewModel, vm))
                return;
            if (_viewModel != null)
                _viewModel.BottomRequested -= OnBottomRequested;
            _viewModel = vm;
            if (_viewModel != null)
                _viewModel.BottomRequested += OnBottomRequested;
        };
    }

    /// <summary>Scrolls the message window to the newest message.</summary>
    void OnBottomRequested()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (MessagesScroller is not { } sv)
                return;
            var max = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
            sv.Offset = new Vector(sv.Offset.X, max);
        });
    }

    /// <summary>Enter sends the message; Shift+Enter keeps a new line.</summary>
    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;
        if (_viewModel?.SendFromEnter() == true)
            e.Handled = true;
    }
}