using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace NinferLauncher_v_2.Controls;

/// <summary>
/// Tlacitko s ikonou (SVG path jako retezec, nebo PNG obraz) a volitelnym textem.
/// Vse lze barvit libovolne: barva ikony (zakladni/hover/pressed), barva textu,
/// pozadi (Background) a samostatne hover/pressed highlight vrstvy.
/// </summary>
public class IconButton : TemplatedControl
{
    Border? _rootBorder;
    Path? _iconPath;
    Avalonia.Controls.Image? _iconImage;
    Grid? _iconHost;
    TextBlock? _label;
    bool _isOver;
    bool _isPressed;

    static IconButton()
    {
        FocusableProperty.OverrideDefaultValue<IconButton>(true);

        IconProperty.Changed.AddClassHandler<IconButton>((s, e) =>
        {
            var d = e.NewValue as string;
            s.IconGeometry = string.IsNullOrWhiteSpace(d) ? null : Geometry.Parse(d!);
        });

        // zmena IsEnabled musi zaktualizovat stitneni + pozadi
        IsEnabledProperty.Changed.AddClassHandler<IconButton>((s, e) => s.ApplyVisualState());


        // kdykoli se meni vizualni vlastnost, zaktualizuj template
        foreach (var dp in new AvaloniaProperty[]
                 {
                     IconProperty, IconGeometryProperty, IconBrushProperty, HoverIconBrushProperty, PressedIconBrushProperty,
                     ImageProperty, LabelProperty, LabelBrushProperty, LabelFontSizeProperty,
                     IconSizeProperty, IconStrokeThicknessProperty, BackgroundProperty,
                     HighlightBrushProperty, PressedHighlightBrushProperty,
                     CornerRadiusProperty, PaddingProperty,
                     BorderBrushProperty, BorderThicknessProperty,
                     LabelOnlyProperty,
                 })
            dp.Changed.AddClassHandler<IconButton>((s, e) => s.SyncVisuals());

        
    }

    public IconButton()
    {
        Template = new FuncControlTemplate<IconButton>((parent, _) => parent.BuildTemplateVisuals());
    }

    // ====================== VLASTNOSTI ======================

    /// <summary>Ikona jako SVG path (napr. "M12,5.5 V18.5 M5.5,12 H18.5").</summary>
    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<IconButton, string?>(nameof(Icon));
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Vyresena geometrie ikony (vypocitana z <see cref="Icon"/>).</summary>
    public static readonly StyledProperty<Geometry?> IconGeometryProperty =
        AvaloniaProperty.Register<IconButton, Geometry?>(nameof(IconGeometry));
    public Geometry? IconGeometry
    {
        get => GetValue(IconGeometryProperty);
        set => SetValue(IconGeometryProperty, value);
    }

    /// <summary>Velikost ikony (sirka = vyska) v px.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<IconButton, double>(nameof(IconSize), 20);
    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>Zakladni barva ikony (stroke pathu).</summary>
    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(IconBrush),
            Brush.Parse("#C6CAD1"));
    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }
    /// <summary>Kdyz je true, host ikony se schova a text se vycentruje bez mezer pro ikonu.</summary>
    public static readonly StyledProperty<bool> LabelOnlyProperty =
        AvaloniaProperty.Register<IconButton, bool>(nameof(LabelOnly));
    public bool LabelOnly
    {
        get => GetValue(LabelOnlyProperty);
        set => SetValue(LabelOnlyProperty, value);
    }

    /// <summary>Barva ikony pri najeti mysi (null = stejne jako zakladni).</summary>
    public static readonly StyledProperty<IBrush?> HoverIconBrushProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(HoverIconBrush));
    public IBrush? HoverIconBrush
    {
        get => GetValue(HoverIconBrushProperty);
        set => SetValue(HoverIconBrushProperty, value);
    }

    /// <summary>Barva ikony pri stisku (null = pouzije se HoverIconBrush).</summary>
    public static readonly StyledProperty<IBrush?> PressedIconBrushProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(PressedIconBrush));
    public IBrush? PressedIconBrush
    {
        get => GetValue(PressedIconBrushProperty);
        set => SetValue(PressedIconBrushProperty, value);
    }

    /// <summary>Tloustka obrysu ikony.</summary>
    public static readonly StyledProperty<double> IconStrokeThicknessProperty =
        AvaloniaProperty.Register<IconButton, double>(nameof(IconStrokeThickness), 1.6);
    public double IconStrokeThickness
    {
        get => GetValue(IconStrokeThicknessProperty);
        set => SetValue(IconStrokeThicknessProperty, value);
    }

    /// <summary>Ikona jako obraz (PNG z IconSet apod.) – vykresli se pod path ikonou.</summary>
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<IconButton, IImage?>(nameof(Image));
    public IImage? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    /// <summary>Barva highlightu pri najeti mysi (null = bez highlightu).</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(HighlightBrush));
    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    /// <summary>Barva highlightu pri stisku (null = stejne jako <see cref="HighlightBrush"/>).</summary>
    public static readonly StyledProperty<IBrush?> PressedHighlightBrushProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(PressedHighlightBrush));
    public IBrush? PressedHighlightBrush
    {
        get => GetValue(PressedHighlightBrushProperty);
        set => SetValue(PressedHighlightBrushProperty, value);
    }
/// <summary>Text vedle ikony (null = jen ikona).</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<IconButton, string?>(nameof(Label));
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Barva textu <see cref="Label"/>.</summary>
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(LabelBrush),
            Brush.Parse("#E3E6EA"));
    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    /// <summary>Velikost pisma <see cref="Label"/>.</summary>
    public static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<IconButton, double>(nameof(LabelFontSize), 14);
    public double LabelFontSize
    {
        get => GetValue(LabelFontSizeProperty);
        set => SetValue(LabelFontSizeProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<IconButton, ICommand?>(nameof(Command));
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<IconButton, object?>(nameof(CommandParameter));
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    // Interni barvy – aktualni stav (zakladni / hover / pressed); template na ne navazuji.
    public static readonly StyledProperty<IBrush?> IconColorBrushProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(IconColorBrush),
            Brush.Parse("#C6CAD1"));
    public IBrush? IconColorBrush
    {
        get => GetValue(IconColorBrushProperty);
        set => SetValue(IconColorBrushProperty, value);
    }

    public static readonly StyledProperty<IBrush?> LabelColorBrushProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(LabelColorBrush),
            Brush.Parse("#E3E6EA"));
    public IBrush? LabelColorBrush
    {
        get => GetValue(LabelColorBrushProperty);
        set => SetValue(LabelColorBrushProperty, value);
    }
public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(Background), Brush.Parse("#282A2E"));
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<IconButton, CornerRadius>(nameof(CornerRadius), new CornerRadius(8));
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<IconButton, Thickness>(nameof(Padding), new Thickness(6));
    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(BorderBrush));
    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<IconButton, Thickness>(nameof(BorderThickness), new Thickness(0));
    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

// ====================== TEMPLATE ======================

    /// <summary>Vytvori vizualni strom tlacitka (vyvolava se pri aplikaci template).</summary>
    Control BuildTemplateVisuals()
    {
        _rootBorder = new Border();

        // ikona: path (obrysova) + obraz, prekryvaji se ve spolecnem hostu
        _iconPath = new Path { IsHitTestVisible = false, Stretch = Stretch.Uniform };
        _iconImage = new Avalonia.Controls.Image { IsHitTestVisible = false, Stretch = Stretch.Uniform };
        _iconHost = new Grid();
        _iconHost.Children.Add(_iconPath);
        _iconHost.Children.Add(_iconImage);

        _label = new TextBlock
        {
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(_iconHost);
        row.Children.Add(_label);

        _rootBorder.Child = row;

        SyncVisuals();
        return _rootBorder;
    }
// ====================== INTERAKCE ======================

    /// <summary>Vyvolano po kliknuti (myš nebo klavesnice).</summary>
    public event EventHandler<RoutedEventArgs>? Click;

    protected virtual void OnClick()
    {
        Click?.Invoke(this, new RoutedEventArgs());
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPressed = true;
            _isOver = true;
            ApplyVisualState();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isPressed) return;
        _isPressed = false;
        // bezpecnost: pozici mysi overujme od systemu (pokud pointer uvolnime
        // mimo tlacitko, musi stav over spadnout)
        bool over = IsPointerOver;
        _isOver = over;
        ApplyVisualState();
        if (over) PerformClick();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isOver = true;
        ApplyVisualState();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isOver = false;
        _isPressed = false;
        ApplyVisualState();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsEnabled && e.Key == Key.Space)
        {
            _isPressed = true;
            ApplyVisualState();
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (!IsEnabled) return;
        if (e.Key == Key.Space)
        {
            bool was = _isPressed;
            _isPressed = false;
            ApplyVisualState();
            if (was) PerformClick();
        }
        else if (e.Key == Key.Enter)
        {
            PerformClick();
        }
    }

    void PerformClick()
    {
        OnClick();
        if (Command is { } cmd && cmd.CanExecute(CommandParameter))
            cmd.Execute(CommandParameter);
    }

    /// <summary>
/// Zobrazeni highlightu: puvodni HighlightBrush ztizeny alfa pozadi (Background).
/// Tak se defaultni pozadi pri hover/pressed neztrati — zustane videt pod highlightem.
/// </summary>
    IBrush? BlendOver(IBrush? highlight, IBrush? background)
    {
        if (highlight == null)
            return null;

        // premultiplied zarnice pozadi (zohledni i Opacity brush)
        double br = 0, bg = 0, bb = 0, ba = 0;
        if (background is SolidColorBrush b)
        {
            byte a0 = b.Color.A;
            ba = a0 * b.Opacity / 255.0;
            br = a0 * b.Color.R / 255.0;
            bg = a0 * b.Color.G / 255.0;
            bb = a0 * b.Color.B / 255.0;
        }

        if (highlight is not SolidColorBrush hc)
            return highlight;

        Color c = hc.Color;
        double a2 = c.A / 255.0 * hc.Opacity;
        if (a2 == 0)
            return null;

        // kompozice "over": pruzrideny svrchni nad pozadim
        double outA = a2 + ba * (1 - a2);
        if (outA == 0)
            return null;

        double r = (a2 * c.R + (1 - a2) * br) / outA;
        double g = (a2 * c.G + (1 - a2) * bg) / outA;
        double bl = (a2 * c.B + (1 - a2) * bb) / outA;
        byte a = (byte)Math.Round(Math.Min(255, outA * 255));
        byte r2 = (byte)Math.Round(Math.Min(255, r));
        byte g2 = (byte)Math.Round(Math.Min(255, g));
        byte b2 = (byte)Math.Round(Math.Min(255, bl));
        return new SolidColorBrush(Color.FromArgb(a, r2, g2, b2));
    }

// ====================== VIZUALNI STAV ======================

    /// <summary>
/// Pozadi tlacitka podle aktualniho stavu: zakladni <see cref="Background"/>,
/// nebo blend hover/pressed barvy nad ním. Vždy stejny zdroj pravdy —
/// defaultni pozadi tak neni mozne "smazat" (po odejití myši se vraci).
/// </summary>
    IBrush? CurrentBackground()
    {
        if (IsEnabled)
        {
            if (_isPressed)
            {
                IBrush? h = BlendOver(PressedHighlightBrush ?? HighlightBrush, Background);
                if (h != null) return h;
            }
            else if (_isOver)
            {
                IBrush? h = BlendOver(HighlightBrush, Background);
                if (h != null) return h;
            }
        }
        return Background;
    }

    void ApplyVisualState()
    {
        // vypnute tlacitko se ztlmiti (jako Button:disabled)
        Opacity = IsEnabled ? 1.0 : 0.45;

        IBrush? iconBrush = IconBrush;
        if (IsEnabled)
        {
            if (_isPressed) iconBrush = PressedIconBrush ?? HoverIconBrush ?? IconBrush;
            else if (_isOver) iconBrush = HoverIconBrush ?? IconBrush;
        }
        IconColorBrush = iconBrush;

        // text zustava stejny (barevne), jen pro jistotu synchronizovano
        LabelColorBrush = LabelBrush;

        SyncVisuals();
    }

    /// <summary>Prenese aktualni hodnoty vlastnosti na dily template (bez runtime bindingu).</summary>
    void SyncVisuals()
    {
        if (_rootBorder == null)
            return;

        _rootBorder.Background = CurrentBackground();
        _rootBorder.CornerRadius = CornerRadius;
        _rootBorder.Padding = Padding;
        _rootBorder.BorderBrush = BorderBrush;
        _rootBorder.BorderThickness = BorderThickness;

        if (_iconPath != null)
        {
            _iconPath.Data = IconGeometry;
            _iconPath.Stroke = IconColorBrush;
            _iconPath.StrokeThickness = IconStrokeThickness;
        }
        if (_iconImage != null)
        {
            _iconImage.Source = Image;
        }
        if (_iconHost != null)
        {
            if (LabelOnly)
            {
                // v Avalonii 12 neexistuje Visibility enum â€” schovame ho pres IsVisible
                _iconHost.IsVisible = false;
            }
            else
            {
                _iconHost.Width = IconSize;
                _iconHost.Height = IconSize;
                _iconHost.IsVisible = true;
            }
        }
        if (_label != null)
        {
            // pri LabelOnly se odpusti levy odsazeni, jinak by zustala mezera po ikone
            _label.Margin = LabelOnly ? new Thickness(0) : new Thickness(10, 0, 0, 0);
            _label.Text = Label;
            _label.Foreground = LabelColorBrush;
            _label.FontSize = LabelFontSize;
        }
    }
}
