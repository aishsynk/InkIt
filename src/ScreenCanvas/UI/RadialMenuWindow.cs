using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenCanvas.Core;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ScreenCanvas.UI;

/// <summary>Quick Radial Pie Menu - design: RadialMenuModal.tsx (eight tools on a 96px ring).</summary>
public sealed class RadialMenuWindow : ModalHost
{
    private static readonly (string Id, string Label, string Icon, Color Hover)[] Items =
    [
        ("cursor", "Cursor", "MousePointer", Tw.Slate700),
        ("pen", "Pen", "Pen", Tw.Blue600),
        ("highlighter", "Highlight", "Highlighter", Tw.Amber500),
        ("eraser", "Eraser", "Eraser", Tw.Rose600),
        ("shape", "Shapes", "Shapes", Tw.Indigo600),
        ("laser", "Laser", "Flame", Tw.Red600),
        ("spotlight", "Spotlight", "SunMedium", Tw.Amber600),
        ("zoom", "Zoom", "ZoomIn", Tw.Purple600),
    ];

    public RadialMenuWindow(ToolbarWindow toolbar) : base(closeOnBackdropClick: true, backdropKey: "Ink.BackdropLight")
    {
        Title = "Radial Menu";
        var wheel = new Canvas { Width = 256, Height = 256 };
        const double center = 128;

        var close = new Button
        {
            Style = (Style)FindResource("Ink.Button"),
            Width = 56,
            Height = 56,
            BorderThickness = new Thickness(2),
            Content = DK.V(0, new LucideIcon("X", 20) { HorizontalAlignment = HorizontalAlignment.Center },
                DK.Text("ESC", 8, Tw.B(Tw.Slate500), mono: true))
        };
        ((TextBlock)((StackPanel)close.Content).Children[1]).HorizontalAlignment = HorizontalAlignment.Center;
        Ui.SetCornerRadius(close, new CornerRadius(28));
        DK.Recolor(close, Tw.B(Tw.Slate900), Tw.B(Tw.Slate300), Tw.B(Tw.Slate900), Tw.B(Colors.White), Tw.B(Tw.Slate700), Tw.B(Tw.Slate700));
        close.Effect = DK.Shadow(40, 14, 0.6);
        close.Click += (_, _) => Close();
        Canvas.SetLeft(close, center - 28);
        Canvas.SetTop(close, center - 28);
        System.Windows.Controls.Panel.SetZIndex(close, 10);
        wheel.Children.Add(close);

        for (var i = 0; i < Items.Length; i++)
        {
            var item = Items[i];
            var angle = (i * (360.0 / Items.Length) - 90) * Math.PI / 180;
            var x = Math.Round(96 * Math.Cos(angle));
            var y = Math.Round(96 * Math.Sin(angle));
            var label = DK.Plain(item.Label, 9, FontWeights.Medium, center: true);
            var content = DK.V(2, new LucideIcon(item.Icon, 16) { HorizontalAlignment = HorizontalAlignment.Center }, label);
            var button = DK.Button(content, Tw.B(Tw.Slate900, 0.95), Tw.B(Tw.Slate200), Tw.B(item.Hover), Tw.B(Colors.White), 16, new Thickness(0), Tw.B(Tw.Slate700, 0.8), Tw.B(Tw.Slate700, 0.8), 1);
            button.Width = button.Height = 48;
            button.Effect = DK.Shadow(24, 8, 0.5);
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.RenderTransform = new ScaleTransform(1, 1);
            button.MouseEnter += (_, _) => button.RenderTransform = new ScaleTransform(1.1, 1.1);
            button.MouseLeave += (_, _) => button.RenderTransform = new ScaleTransform(1, 1);
            button.Click += (_, _) =>
            {
                Close();
                Dispatcher.BeginInvoke(() =>
                {
                    switch (item.Id)
                    {
                        case "cursor": toolbar.EndCurrentTool(); break;
                        case "pen": toolbar.SelectTool(ToolKind.Pen); break;
                        case "highlighter": toolbar.SelectTool(ToolKind.Highlighter); break;
                        case "eraser": toolbar.SelectTool(ToolKind.Eraser); break;
                        case "shape": toolbar.SelectTool(ToolKind.Shape); break;
                        case "laser": toolbar.SelectTool(ToolKind.Laser); break;
                        case "spotlight": toolbar.SelectTool(ToolKind.Spotlight); break;
                        case "zoom": toolbar.StartLiveZoom(); break;
                    }
                    Toast.Show($"Switched tool to {item.Label.ToLowerInvariant()}");
                });
            };
            Canvas.SetLeft(button, center + x - 24);
            Canvas.SetTop(button, center + y - 24);
            wheel.Children.Add(button);
        }

        // Centre the wheel on the cursor (clamped to the monitor).
        wheel.HorizontalAlignment = HorizontalAlignment.Left;
        wheel.VerticalAlignment = VerticalAlignment.Top;
        Root.Children.Add(wheel);
        Loaded += (_, _) =>
        {
            var cursor = PointFromScreen(new Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y));
            wheel.Margin = new Thickness(
                Math.Clamp(cursor.X - center, 8, Math.Max(8, ActualWidth - 264)),
                Math.Clamp(cursor.Y - center, 8, Math.Max(8, ActualHeight - 264)), 0, 0);
        };
    }
}
