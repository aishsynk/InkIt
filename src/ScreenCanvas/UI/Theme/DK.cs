using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ScreenCanvas.UI.Controls;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;
using FontFamily = System.Windows.Media.FontFamily;

namespace ScreenCanvas.UI.Theme;

/// <summary>A colour reference: either a theme resource key ("Ink.Text300") or a fixed brush.</summary>
public readonly struct Paint
{
    public string? Key { get; }
    public Brush? Brush { get; }
    private Paint(string? key, Brush? brush) { Key = key; Brush = brush; }
    public static implicit operator Paint(string key) => new(key, null);
    public static implicit operator Paint(SolidColorBrush brush) => new(null, brush);
    public static implicit operator Paint(Color color) => new(null, Tw.B(color));
    public static Paint Of(Brush brush) => new(null, brush);
    public bool IsEmpty => Key is null && Brush is null;

    public void ApplyTo(DependencyObject target, DependencyProperty property)
    {
        if (Key is not null)
        {
            if (target is FrameworkElement fe) fe.SetResourceReference(property, Key);
            else if (target is FrameworkContentElement fce) fce.SetResourceReference(property, Key);
        }
        else if (Brush is not null) target.SetValue(property, Brush);
    }
}

/// <summary>Design kit: factories that reproduce the Tailwind look of the InkIt design in WPF.</summary>
public static class DK
{
    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    // ---------- Text ----------
    public static TextBlock Text(string text, double size, Paint foreground, FontWeight? weight = null, bool mono = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontFamily = mono ? Mono : Font,
            FontWeight = weight ?? FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };
        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
        foreground.ApplyTo(tb, TextBlock.ForegroundProperty);
        return tb;
    }

    /// <summary>Text that inherits its colour (e.g. from a button's hover state).</summary>
    public static TextBlock Plain(string text, double size, FontWeight? weight = null, bool mono = false, bool center = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontFamily = mono ? Mono : Font,
            FontWeight = weight ?? FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Stretch
        };
        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
        return tb;
    }

    public static TextBlock Wrap(this TextBlock tb, double lineHeight = 0)
    {
        tb.TextWrapping = TextWrapping.Wrap;
        tb.TextTrimming = TextTrimming.None;
        if (lineHeight > 0) tb.LineHeight = lineHeight;
        return tb;
    }

    /// <summary>Uppercase tracking-wider section heading (text-xs font-semibold uppercase).</summary>
    public static TextBlock Caps(string text, double size, Paint foreground, FontWeight? weight = null)
    {
        var tb = Text(text.ToUpperInvariant(), size, foreground, weight ?? FontWeights.SemiBold);
        tb.Margin = new Thickness(0);
        return tb;
    }

    /// <summary>Paragraph with inline runs; pass (text, paint, mono) segments.</summary>
    public static TextBlock Rich(double size, Paint baseForeground, params (string Text, Paint? Paint, bool Mono)[] segments)
    {
        var tb = Text(string.Empty, size, baseForeground).Wrap();
        tb.Inlines.Clear();
        foreach (var (text, paint, mono) in segments)
        {
            var run = new System.Windows.Documents.Run(text) { FontFamily = mono ? Mono : Font };
            paint?.ApplyTo(run, System.Windows.Documents.TextElement.ForegroundProperty);
            tb.Inlines.Add(run);
        }
        return tb;
    }

    // ---------- Layout ----------
    public static StackPanel V(double spacing, params UIElement?[] children) => Stack(Orientation.Vertical, spacing, children);
    public static StackPanel H(double spacing, params UIElement?[] children) => Stack(Orientation.Horizontal, spacing, children);

    public static StackPanel Stack(Orientation orientation, double spacing, IEnumerable<UIElement?> children)
    {
        var panel = new StackPanel { Orientation = orientation };
        var first = true;
        foreach (var child in children)
        {
            if (child is null) continue;
            if (!first && child is FrameworkElement fe)
            {
                var m = fe.Margin;
                fe.Margin = orientation == Orientation.Vertical
                    ? new Thickness(m.Left, m.Top + spacing, m.Right, m.Bottom)
                    : new Thickness(m.Left + spacing, m.Top, m.Right, m.Bottom);
            }
            panel.Children.Add(child);
            first = false;
        }
        return panel;
    }

    /// <summary>CSS-like grid with a fixed number of equal columns and a gap.</summary>
    public static Grid Columns(int columns, double gap, IEnumerable<UIElement> children)
    {
        var grid = new Grid();
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var index = 0;
        foreach (var child in children)
        {
            var row = index / columns;
            var col = index % columns;
            while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (child is FrameworkElement fe)
                fe.Margin = new Thickness(col == 0 ? 0 : gap / 2, row == 0 ? 0 : gap, col == columns - 1 ? 0 : gap / 2, 0);
            Grid.SetRow(child, row);
            Grid.SetColumn(child, col);
            grid.Children.Add(child);
            index++;
        }
        return grid;
    }

    /// <summary>Left and right content on one row ("flex justify-between").</summary>
    public static DockPanel Between(UIElement left, UIElement right)
    {
        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(right, Dock.Right);
        dock.Children.Add(right);
        dock.Children.Add(left);
        if (left is FrameworkElement l) l.HorizontalAlignment = HorizontalAlignment.Left;
        return dock;
    }

    public static Border Divider(bool horizontalLine = true, Paint? paint = null)
    {
        var line = new Border { Height = horizontalLine ? 1 : double.NaN, Width = horizontalLine ? double.NaN : 1, SnapsToDevicePixels = true };
        (paint ?? "Ink.Divider").ApplyTo(line, Border.BackgroundProperty);
        return line;
    }

    /// <summary>A block separated from what precedes it by a top border (pt-2 border-t).</summary>
    public static Border TopRule(UIElement child, double paddingTop = 8, Paint? paint = null)
    {
        var border = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, paddingTop, 0, 0), Child = child };
        (paint ?? "Ink.Divider").ApplyTo(border, Border.BorderBrushProperty);
        return border;
    }

    // ---------- Surfaces ----------
    public static Border Surface(UIElement child, Paint background, Paint border, double radius, Thickness padding, double borderThickness = 1)
    {
        var b = new Border
        {
            Child = child,
            CornerRadius = new CornerRadius(radius),
            Padding = padding,
            BorderThickness = new Thickness(borderThickness),
            SnapsToDevicePixels = true
        };
        background.ApplyTo(b, Border.BackgroundProperty);
        border.ApplyTo(b, Border.BorderBrushProperty);
        return b;
    }

    public static DropShadowEffect Shadow(double blur = 32, double depth = 10, double opacity = 0.5, Color? color = null) =>
        new() { BlurRadius = blur, ShadowDepth = depth, Direction = 270, Opacity = opacity, Color = color ?? Colors.Black, RenderingBias = RenderingBias.Performance };

    // ---------- Buttons ----------
    public static Button Button(object content, Paint background, Paint foreground, Paint hoverBackground, Paint hoverForeground,
        double radius = 8, Thickness? padding = null, Paint? border = null, Paint? hoverBorder = null, double borderThickness = 0)
    {
        var btn = new Button
        {
            Style = (Style)System.Windows.Application.Current.FindResource("Ink.Button"),
            Content = content,
            Padding = padding ?? new Thickness(0),
            BorderThickness = new Thickness(borderThickness)
        };
        Ui.SetCornerRadius(btn, new CornerRadius(radius));
        Recolor(btn, background, foreground, hoverBackground, hoverForeground, border, hoverBorder);
        return btn;
    }

    public static void Recolor(Button btn, Paint background, Paint foreground, Paint hoverBackground, Paint hoverForeground,
        Paint? border = null, Paint? hoverBorder = null)
    {
        background.ApplyTo(btn, System.Windows.Controls.Control.BackgroundProperty);
        foreground.ApplyTo(btn, System.Windows.Controls.Control.ForegroundProperty);
        hoverBackground.ApplyTo(btn, Ui.HoverBackgroundProperty);
        hoverForeground.ApplyTo(btn, Ui.HoverForegroundProperty);
        var b = border ?? Paint.Of(System.Windows.Media.Brushes.Transparent);
        b.ApplyTo(btn, System.Windows.Controls.Control.BorderBrushProperty);
        (hoverBorder ?? b).ApplyTo(btn, Ui.HoverBorderBrushProperty);
    }

    /// <summary>Plain icon button with hover background (p-1 / p-1.5 close buttons, etc.).</summary>
    public static Button IconButton(string icon, double iconSize, Paint foreground, Paint hoverForeground, Paint? hoverBackground = null,
        double padding = 4, double radius = 8, string? tooltip = null)
    {
        var btn = Button(new LucideIcon(icon, iconSize), Paint.Of(System.Windows.Media.Brushes.Transparent), foreground,
            hoverBackground ?? "Ink.Hover", hoverForeground, radius, new Thickness(padding));
        if (tooltip is not null) btn.ToolTip = tooltip;
        return btn;
    }

    /// <summary>Icon + label content for buttons ("flex items-center space-x-1.5").</summary>
    public static StackPanel IconLabel(string icon, double iconSize, string label, double fontSize, FontWeight? weight = null, double spacing = 6)
    {
        var text = new TextBlock { Text = label, FontSize = fontSize, FontFamily = Font, FontWeight = weight ?? FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
        TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
        return H(spacing, new LucideIcon(icon, iconSize) { VerticalAlignment = VerticalAlignment.Center }, text);
    }

    // ---------- Inputs ----------
    public static Slider Slider(double min, double max, double step, double value, Paint accent, Action<double> changed)
    {
        var slider = new Slider
        {
            Style = (Style)System.Windows.Application.Current.FindResource("Ink.Slider"),
            Minimum = min,
            Maximum = max,
            SmallChange = step,
            LargeChange = step,
            TickFrequency = step,
            IsSnapToTickEnabled = true,
            Value = value
        };
        accent.ApplyTo(slider, System.Windows.Controls.Control.ForegroundProperty);
        slider.ValueChanged += (_, e) => changed(e.NewValue);
        return slider;
    }

    public static CheckBox Check(bool isChecked, Action<bool> changed, Paint? accent = null)
    {
        var box = new CheckBox { Style = (Style)System.Windows.Application.Current.FindResource("Ink.CheckBox"), IsChecked = isChecked, VerticalAlignment = VerticalAlignment.Center };
        accent?.ApplyTo(box, System.Windows.Controls.Control.ForegroundProperty);
        box.Checked += (_, _) => changed(true);
        box.Unchecked += (_, _) => changed(false);
        return box;
    }

    public static CheckBox Switch(bool isChecked, Action<bool> changed, Paint? accent = null, double width = 32, double height = 16, Paint? offBackground = null)
    {
        var box = new CheckBox
        {
            Style = (Style)System.Windows.Application.Current.FindResource("Ink.Switch"),
            IsChecked = isChecked,
            Width = width,
            Height = height,
            VerticalAlignment = VerticalAlignment.Center
        };
        accent?.ApplyTo(box, System.Windows.Controls.Control.ForegroundProperty);
        offBackground?.ApplyTo(box, System.Windows.Controls.Control.BackgroundProperty);
        box.Checked += (_, _) => changed(true);
        box.Unchecked += (_, _) => changed(false);
        return box;
    }

    public static TextBox Input(string text, string placeholder, Paint focusBorder, double radius = 6, double fontSize = 12, bool mono = false)
    {
        var box = new TextBox
        {
            Style = (Style)System.Windows.Application.Current.FindResource("Ink.TextBox"),
            Text = text,
            Tag = placeholder,
            FontSize = fontSize
        };
        if (mono) box.FontFamily = Mono;
        Ui.SetCornerRadius(box, new CornerRadius(radius));
        focusBorder.ApplyTo(box, Ui.HoverBorderBrushProperty);
        return box;
    }

    // ---------- Chips ----------
    /// <summary>&lt;kbd&gt; style shortcut badge.</summary>
    public static Border Kbd(string text, Paint foreground, Paint? background = null, Paint? border = null, double fontSize = 10, Thickness? padding = null)
    {
        var tb = Text(text, fontSize, foreground, mono: true);
        var b = Surface(tb, background ?? "Ink.Kbd", border ?? "Ink.BorderStrong", 4, padding ?? new Thickness(6, 1, 6, 1));
        b.VerticalAlignment = VerticalAlignment.Center;
        return b;
    }

    public static Border Chip(string text, Paint background, Paint foreground, Paint border, double fontSize = 9, double radius = 4,
        Thickness? padding = null, bool mono = true, FontWeight? weight = null)
    {
        var tb = Text(text, fontSize, foreground, weight, mono);
        var b = Surface(tb, background, border, radius, padding ?? new Thickness(4, 0, 4, 0));
        b.VerticalAlignment = VerticalAlignment.Center;
        return b;
    }

    public static Ellipse Dot(double size, Paint fill)
    {
        var e = new Ellipse { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
        fill.ApplyTo(e, Shape.FillProperty);
        return e;
    }

    /// <summary>"Label ........ value" header above sliders (flex justify-between text-xs text-slate-400 mb-1).</summary>
    public static DockPanel ValueRow(string label, string value, Paint valueForeground, out TextBlock valueText, double size = 12)
    {
        valueText = Text(value, size, valueForeground, FontWeights.Normal, mono: true);
        var row = Between(Text(label, size, "Ink.Text400"), valueText);
        row.Margin = new Thickness(0, 0, 0, 4);
        return row;
    }

    /// <summary>Label above a slider whose value text updates as it moves.</summary>
    public static StackPanel SliderBlock(string label, double min, double max, double step, double value, Func<double, string> format,
        Paint accent, Action<double> changed, FontWeight? valueWeight = null)
    {
        var header = ValueRow(label, format(value), accent, out var valueText);
        if (valueWeight is not null) valueText.FontWeight = valueWeight.Value;
        var slider = Slider(min, max, step, value, accent, v => { valueText.Text = format(v); changed(v); });
        return V(0, header, slider);
    }
}
