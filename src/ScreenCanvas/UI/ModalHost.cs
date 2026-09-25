using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ScreenCanvas.UI.Theme;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using TextElement = System.Windows.Documents.TextElement;

namespace ScreenCanvas.UI;

/// <summary>
/// Full-monitor modal: dimmed backdrop (black/60) with a centred card, as used by the design's
/// Capability Centre, Command Palette, Radial Menu, Settings and Capture Preview.
/// </summary>
public class ModalHost : Window
{
    protected readonly Grid Root = new();
    private readonly Border _backdrop = new();

    protected ModalHost(bool closeOnBackdropClick, string backdropKey = "Ink.Backdrop")
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        _backdrop.SetResourceReference(Border.BackgroundProperty, backdropKey);
        Root.Children.Add(_backdrop);
        Content = Root;
        TextElement.SetFontFamily(Root, DK.Font);

        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).Bounds;
        SourceInitialized += (_, _) =>
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = screen.Left / dpi.DpiScaleX;
            Top = screen.Top / dpi.DpiScaleY;
            Width = screen.Width / dpi.DpiScaleX;
            Height = screen.Height / dpi.DpiScaleY;
        };
        Left = screen.Left;
        Top = screen.Top;
        Width = screen.Width;
        Height = screen.Height;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                e.Handled = true;
                Close();
            }
        };
        if (closeOnBackdropClick) _backdrop.MouseLeftButtonDown += (_, _) => Close();
        Loaded += (_, _) => Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
    }

    /// <summary>Adds the design card on top of the backdrop.</summary>
    protected Border AddCard(UIElement content, double width, double height = double.NaN, VerticalAlignment vertical = VerticalAlignment.Center, double top = 0)
    {
        var card = DK.Surface(content, "Ink.SurfaceSolid", "Ink.Border", 16, new Thickness(0));
        card.Width = width;
        card.Height = height;
        card.MaxWidth = width;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = vertical;
        card.Margin = new Thickness(16, top, 16, 16);
        card.ClipToBounds = true;
        card.Effect = DK.Shadow(60, 24, 0.55);
        card.MouseLeftButtonDown += (_, e) => e.Handled = true;
        Root.Children.Add(card);
        return card;
    }

    /// <summary>Header strip (px-6 py-4 border-b bg-slate-950/60) with icon tile, title, subtitle and close button.</summary>
    protected FrameworkElement Header(string icon, System.Windows.Media.Color iconColor, string title, UIElement subtitle)
    {
        var tile = DK.Surface(new Controls.LucideIcon(icon, 20) { Foreground = Tw.B(iconColor) }, Tw.B(iconColor, 0.2), Tw.B(Colors.Transparent), 12, new Thickness(8), 0);
        var text = DK.V(2, DK.Text(title, 16, "Ink.Text", FontWeights.SemiBold), subtitle);
        var close = DK.IconButton("X", 20, "Ink.Text400", "Ink.Text", "Ink.Hover", 6, 8);
        close.Click += (_, _) => Close();
        var bar = new Border
        {
            Padding = new Thickness(24, 16, 24, 16),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = DK.Between(DK.H(12, tile, text), close)
        };
        bar.SetResourceReference(Border.BackgroundProperty, "Ink.Sunken");
        bar.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");
        return bar;
    }

    /// <summary>Footer strip (px-6 py-3 border-t bg-slate-950/60).</summary>
    protected static Border Footer(UIElement left, UIElement right, Thickness? padding = null)
    {
        var bar = new Border
        {
            Padding = padding ?? new Thickness(24, 12, 24, 12),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = DK.Between(left, right)
        };
        bar.SetResourceReference(Border.BackgroundProperty, "Ink.Sunken");
        bar.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");
        return bar;
    }
}
