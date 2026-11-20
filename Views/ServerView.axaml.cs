using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NinferLauncher_v_2.Models;
using NinferLauncher_v_2.ViewModels;

namespace NinferLauncher_v_2.Views;

public partial class ServerView : UserControl
{
    ServerViewModel? _vm;

    // TEMP diagnostics: same file as the VM's clipboard log.
    static string ClipLogPath => System.IO.Path.Combine(AppContext.BaseDirectory, "clipboard-debug.log");
    static void VLog(string msg)
    {
        try { System.IO.File.AppendAllText(ClipLogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " [VIEW] " + msg + Environment.NewLine); }
        catch { }
    }

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

    /// <summary>Terminal selection: press a line to anchor, drag across lines to extend the
    /// selection (live highlight via the view model), release to copy the range to the
    /// clipboard. A plain click copies a single line; Shift+click extends from the anchor.
    /// Pointer handling is centralized on the ScrollViewer with pointer capture so a release
    /// is always caught even if the pointer leaves the list mid-drag.</summary>
    void OnTermPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        VLog($"OnTermPointerPressed: vm=({_vm is not null}) sender={sender?.GetType().Name}");
        if (_vm is null)
            return;
        var pt = e.GetCurrentPoint(this);
        VLog($"  leftButton={pt.Properties.IsLeftButtonPressed} pos={e.GetPosition(this)}");
        if (!pt.Properties.IsLeftButtonPressed)
            return;

        if (FindLineAt(e, out var line) && line is not null)
        {
            VLog($"  FindLineAt HIT line={line.Text}");
            // Capture on the ScrollViewer (the element that hosts the PointerReleased handler) so
            // the release is routed back here; capturing on 'this' (the UserControl) would route
            // the release to a view that has no release handler and the copy would never fire.
            if (sender is Avalonia.Controls.Control captureTarget)
                e.Pointer.Capture(captureTarget);
            _vm.BeginLineSelection(line, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            e.Handled = true;
        }
        else
        {
            VLog("  FindLineAt MISS (no line under point)");
        }
    }

    void OnTermPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer.Captured, sender))
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (_vm is null)
            return;

        if (FindLineAt(e, out var line) && line is not null)
            _vm.UpdateLineSelection(line);
    }

    void OnTermPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer.Captured, sender))
            return;
        e.Pointer.Capture(null);
        _vm?.CommitLineSelection();
    }

    /// <summary>Finds the terminal line under the given pointer by hit-testing the materialized
    /// children of the items panel. The point is converted into the <b>panel's</b> coordinate
    /// space and each child's bounds is transformed up to that same space, so the test is correct
    /// regardless of the ScrollViewer's current scroll offset (hit-testing against the view space
    /// fails once the list is scrolled). Virtualized (off-screen) lines are not selectable.</summary>
    bool FindLineAt(PointerEventArgs e, out TerminalLine? line)
    {
        line = null;
        if (TermList is null)
        {
            VLog("  FindLineAt: TermList is null");
            return false;
        }

        Panel? panel = null;
        foreach (var d in TermList.GetVisualDescendants())
        {
            if (d is Panel p)
            {
                panel = p;
                break;
            }
        }
        if (panel is null)
        {
            VLog("  FindLineAt: no items Panel found");
            return false;
        }

        // Work in the items panel's coordinate space: get the pointer position in that space,
        // then for each materialized line compute its top-left in the same space via
        // TranslatePoint (which, unlike TransformToVisual, accounts for the child's offset
        // inside the panel) and test the pointer against [topLeft, topLeft+size]. This is
        // correct regardless of the ScrollViewer's scroll offset.
        var panelPos = e.GetPosition(panel);
        foreach (var child in panel.Children)
        {
            var topLeft = child.TranslatePoint(default, panel);
            if (topLeft is null)
                continue;

            if (new Rect(topLeft.Value, child.Bounds.Size).Contains(panelPos))
            {
                line = child.DataContext as TerminalLine;
                if (line is not null)
                    VLog($"  FindLineAt HIT line={line.Text}");
                return line is not null;
            }
        }
        VLog($"  FindLineAt: panel={panel.GetType().Name} children={panel.Children.Count} panelPos={panelPos} -> MISS");
        return false;
    }
}