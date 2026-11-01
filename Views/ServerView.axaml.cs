using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using NinferLauncher_v_2.ViewModels;

namespace NinferLauncher_v_2.Views;

public partial class ServerView : UserControl
{
    ServerViewModel? _vm;

    public ServerView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ServerViewModel vm)
            {
                _vm = vm;
                vm.Terminal.CollectionChanged += OnTerminalChanged;
            }
        };
    }

    void OnTerminalChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset))
        {
            return;
        }

        if (_vm is { Terminal.Count: > 0 })
        {
            var last = _vm.Terminal[^1];
            Dispatcher.UIThread.Post(() => TermList.ScrollIntoView(last));
        }
    }
}