using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;
using Size = System.Windows.Size;

namespace ScreenCanvas.Overlay;

/// <summary>
/// Zoom-to-area: a frozen snapshot of a dragged area fills this display and stays put, so a trainer can
/// point, draw and highlight on it. Drawings made while zoomed are kept apart and dropped on exit; the
/// annotations that were on screen before zooming come back unchanged.
/// </summary>
public partial class OverlayWindow
{
    private (StrokeCollection Strokes, List<UIElement> Shapes, List<object> History)? _zoomStash;
    private Border? _zoomBar;

    public bool IsZoomViewActive => _zoomStash is not null;

    public void EnterZoomView(ImageSource image)
    {
        CommitTextEditor();
        ClearSelection();
        CancelMouseStroke();
        CancelShapePreview();
        if (_zoomStash is null)
        {
            DropFadingItems();
            _zoomStash = (InkSurface.Strokes, ShapeSurface.Children.Cast<UIElement>().ToList(), _history.ToList());
            InkSurface.Strokes = new StrokeCollection();
        }
        else
        {
            InkSurface.Strokes.Clear();
        }
        ShapeSurface.Children.Clear();
        _history.Clear();
        _removed.Clear();
        _expirations.Clear();

        ZoomImage.Source = image;
        ZoomSurface.Visibility = Visibility.Visible;
        ShowZoomBar();
        RefreshPageBar();
        if (!IsVisible) Show();
    }

    public void ExitZoomView()
    {
        if (_zoomStash is not { } stash) return;
        CommitTextEditor();
        ClearSelection();
        CancelMouseStroke();
        CancelShapePreview();
        _zoomStash = null;
        InkSurface.Strokes = stash.Strokes;
        ShapeSurface.Children.Clear();
        foreach (var shape in stash.Shapes) ShapeSurface.Children.Add(shape);
        _history.Clear();
        _history.AddRange(stash.History);
        _removed.Clear();
        _expirations.Clear();
        _fadeTimer.Stop();

        ZoomImage.Source = null;
        ZoomSurface.Visibility = Visibility.Collapsed;
        if (_zoomBar is not null) _zoomBar.Visibility = Visibility.Collapsed;
        RefreshPageBar();
        if (_settings.Tool == Core.ToolKind.Cursor && !HasVisibleContent) Hide();
    }

    /// <summary>Fading ink is removed now rather than tracked across the zoom session.</summary>
    private void DropFadingItems()
    {
        foreach (var item in _expirations.Keys.ToList())
        {
            if (item is Stroke stroke && InkSurface.Strokes.Contains(stroke)) InkSurface.Strokes.Remove(stroke);
            if (item is UIElement element) ShapeSurface.Children.Remove(element);
            _history.Remove(item);
        }
        _expirations.Clear();
        _fadeTimer.Stop();
    }

    private void ShowZoomBar()
    {
        _zoomBar ??= BuildZoomBar();
        _zoomBar.Visibility = Visibility.Visible;
        _zoomBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        Canvas.SetLeft(_zoomBar, Math.Max(8, (width - _zoomBar.DesiredSize.Width) / 2));
        Canvas.SetTop(_zoomBar, Math.Max(8, height - _zoomBar.DesiredSize.Height - 56));
    }

    private Border BuildZoomBar()
    {
        Button Action(string icon, string label, Color fg, Color bg, string tip, Action click)
        {
            var b = DK.Button(DK.IconLabel(icon, 14, label, 12, spacing: 6), Tw.B(bg), Tw.B(fg), Tw.B(bg, 0.8), Tw.B(Colors.White), 8, new Thickness(10, 5, 10, 5));
            b.ToolTip = tip;
            b.Click += (_, _) => click();
            return b;
        }

        var title = DK.H(6, new LucideIcon("ZoomIn", 16) { Foreground = Tw.B(Tw.Indigo300) },
            DK.Text("Zoomed area - draw or highlight on it", 12, Tw.B(Colors.White), FontWeights.SemiBold));
        title.VerticalAlignment = VerticalAlignment.Center;
        title.Margin = new Thickness(4, 0, 12, 0);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                title,
                Action("Crop", "Zoom another area", Colors.White, Tw.Indigo600, "Pick a different part of the screen", () => _manager?.RequestZoomArea()),
                new Border { Width = 6 },
                Action("X", "Exit zoom (Esc)", Tw.Slate200, Tw.Slate700, "Back to the normal screen", () => _manager?.ExitZoomArea())
            }
        };
        var bar = new Border
        {
            Child = row,
            Background = Tw.B(Tw.Slate900, 0.95),
            BorderBrush = Tw.B(Tw.Indigo500, 0.5),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 6, 8, 6),
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.5, Color = Colors.Black },
            Visibility = Visibility.Collapsed
        };
        ChromeSurface.Children.Add(bar);
        return bar;
    }
}
