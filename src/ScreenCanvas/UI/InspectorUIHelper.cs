using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenCanvas.Core;
using ScreenCanvas.Overlay;
using Button = System.Windows.Controls.Button;
using Panel = System.Windows.Controls.Panel;
using MediaBrush = System.Windows.Media.Brush;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using FontFamily = System.Windows.Media.FontFamily;
using MediaBrushes = System.Windows.Media.Brushes;

namespace ScreenCanvas.UI;

public static class InspectorUIHelper
{
    public static Border CreateComponentBar(ToolbarWindow owner, IOverlayManager overlay, (string icon, string label, Action action, bool isSelected)[] items, bool isHoriz)
    {
        var bar = new Border
        {
            Background = (MediaBrush)owner.FindResource("Level2Brush"),
            BorderBrush = (MediaBrush)owner.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(2),
            Margin = isHoriz ? new Thickness(0, 0, 8, 0) : new Thickness(0, 0, 0, 4)
        };
        var stack = new StackPanel { Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical };
        foreach (var item in items)
        {
            stack.Children.Add(CreateNibPill(owner, item.icon, item.label, item.isSelected, item.action));
        }
        bar.Child = stack;
        return bar;
    }

    public static Button CreateNibPill(ToolbarWindow owner, string iconKey, string label, bool isSelected, Action onClick)
    {
        bool isHoriz = owner.IsHorizontal;
        var btn = new Button
        {
            Width = isHoriz ? double.NaN : 22,
            Height = 22,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = isHoriz ? new Thickness(1, 0, 1, 0) : new Thickness(0, 0.5, 0, 0.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = label
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = isHoriz ? new Thickness(5, 0, 6, 0) : new Thickness(0),
            Background = isSelected ? (MediaBrush)owner.FindResource("PanelBrush") : MediaBrushes.Transparent
        };
        if (isSelected)
        {
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = (MediaBrush)owner.FindResource("BorderBrush");
        }

        var geom = owner.TryFindResource(iconKey) as Geometry ?? (Geometry)owner.FindResource("Fluent.Pen.Regular");
        var path = new Path
        {
            Data = geom,
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            Fill = isSelected ? (MediaBrush)owner.FindResource("AccentBrush") : (MediaBrush)owner.FindResource("PrimaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isHoriz)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(path);
            var txt = new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isSelected ? (MediaBrush)owner.FindResource("AccentBrush") : (MediaBrush)owner.FindResource("PrimaryTextBrush"),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(txt);
            border.Child = stack;
        }
        else
        {
            border.Child = path;
        }

        btn.Content = border;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    public static Panel CreateStrokeSelector(ToolbarWindow owner, IOverlayManager overlay, (double width, double dot, string tip)[] options, bool isHoriz)
    {
        var stack = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            Margin = isHoriz ? new Thickness(0, 0, 4, 0) : new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        foreach (var opt in options)
        {
            var isSel = Math.Abs(overlay.Settings.Thickness - opt.width) < 2.0;
            stack.Children.Add(CreateStrokeDot(owner, opt.dot, opt.width, isSel, opt.tip, () => { overlay.SetThickness(opt.width); }));
        }
        return stack;
    }

    public static Button CreateStrokeDot(ToolbarWindow owner, double diameter, double widthVal, bool isSelected, string tip, Action onClick)
    {
        bool isHoriz = owner.IsHorizontal;
        var btn = new Button
        {
            Width = 20,
            Height = 20,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = isHoriz ? new Thickness(1, 0, 1, 0) : new Thickness(0, 0.5, 0, 0.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = tip
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = isSelected ? (MediaBrush)owner.FindResource("SelectedBrush") : MediaBrushes.Transparent
        };
        if (isSelected)
        {
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = (MediaBrush)owner.FindResource("AccentBrush");
        }

        var dot = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = isSelected ? (MediaBrush)owner.FindResource("AccentBrush") : (MediaBrush)owner.FindResource("PrimaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = dot;
        btn.Content = border;
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
