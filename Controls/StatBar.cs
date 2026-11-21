using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NinferLauncher_v_2.Controls;

/// <summary>Tenka use bar pro hardwarove statistik: tmave stopa, barevny plnik.
/// Value je 0..100; Vysku se da nastavit v XAML (Height="6"), sirka se samy
/// natahuje (Stretch).</summary>
public class StatBar : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<StatBar, double>(nameof(Value));

    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<StatBar, IBrush>(nameof(Fill),
            new SolidColorBrush(Color.Parse("#6A9955")));

    /// <summary>Obsazenost v procentech (0..100).</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Barva plniku.</summary>
    public IBrush Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    static StatBar()
    {
        ValueProperty.Changed.AddClassHandler<StatBar>((s, _) => s.InvalidateVisual());
        FillProperty.Changed.AddClassHandler<StatBar>((s, _) => s.InvalidateVisual());
    }

    public StatBar() => ClipToBounds = true;

    public override void Render(DrawingContext ctx)
    {
        var b = new Rect(Bounds.Size);
        if (b.Width < 1 || b.Height < 1)
            return;
        var r = Math.Min(3, b.Height / 2);
        ctx.DrawGeometry(new SolidColorBrush(Color.Parse("#282A2E")), null,
            new RectangleGeometry(b, r, r));
        double w = Math.Clamp(Value, 0, 100) / 100.0 * b.Width;
        if (w < 1)
            return;
        var fill = new Rect(b.X, b.Y, w, b.Height);
        ctx.DrawGeometry(Fill, null, new RectangleGeometry(fill, r, r));
    }
}