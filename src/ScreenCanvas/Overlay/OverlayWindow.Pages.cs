using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenCanvas.Core;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace ScreenCanvas.Overlay;

/// <summary>One page of drawings: ink strokes, shapes/text/stamps and its undo history.</summary>
public sealed class InkPage
{
    public StrokeCollection Strokes { get; set; } = new();
    public List<UIElement> Shapes { get; set; } = [];
    public List<object> History { get; set; } = [];
    public bool IsEmpty => Strokes.Count == 0 && Shapes.Count == 0;
}

/// <summary>
/// Pages: the whiteboard (or the screen) can hold several pages of drawings, like a notebook, and the drawings of
/// each PowerPoint slide are kept apart and come back when the slide does. Pages also export to images/PDF and
/// save to an .inkit file.
/// </summary>
public partial class OverlayWindow
{
    private static readonly Guid PenModeProperty = new("7C8B6C2E-5D0B-4E7A-9C51-2A6C9B8D1F01");

    private readonly List<InkPage> _pages = [new InkPage()];
    private int _pageIndex;
    private readonly Dictionary<string, InkPage> _slidePages = [];
    private InkPage? _beforeSlideshow;
    private string? _currentSlide;

    public int PageCount => _pages.Count;
    public int PageIndex => _pageIndex;
    public bool IsFollowingSlides => _beforeSlideshow is not null;

    // ------------------------------------------------------------------ Numbered pages

    public void GoToPage(int index)
    {
        if (IsFollowingSlides || index < 0 || index >= _pages.Count || index == _pageIndex) return;
        StashLive(_pages[_pageIndex]);
        _pageIndex = index;
        LoadLive(_pages[_pageIndex]);
    }

    /// <summary>Adds an empty page after the current one and shows it.</summary>
    public void AddPage()
    {
        if (IsFollowingSlides) return;
        StashLive(_pages[_pageIndex]);
        _pages.Insert(_pageIndex + 1, new InkPage());
        _pageIndex++;
        LoadLive(_pages[_pageIndex]);
    }

    public void DeleteCurrentPage()
    {
        if (IsFollowingSlides) return;
        if (_pages.Count == 1) { ClearInk(); return; }
        _pages.RemoveAt(_pageIndex);
        _pageIndex = Math.Min(_pageIndex, _pages.Count - 1);
        LoadLive(_pages[_pageIndex]);
    }

    // ------------------------------------------------------------------ Slides

    /// <summary>Shows the drawings that belong to a slide (a new blank page the first time).</summary>
    public void ShowSlide(string slideKey)
    {
        if (_currentSlide == slideKey) return;
        if (_beforeSlideshow is null)
        {
            _beforeSlideshow = new InkPage();
            StashLive(_beforeSlideshow);
        }
        else if (_currentSlide is not null)
        {
            StashLive(_slidePages[_currentSlide]);
        }
        if (!_slidePages.TryGetValue(slideKey, out var page)) _slidePages[slideKey] = page = new InkPage();
        _currentSlide = slideKey;
        LoadLive(page);
    }

    /// <summary>The slideshow ended: slide drawings are kept for next time, the screen's own drawings come back.</summary>
    public void EndSlides()
    {
        if (_beforeSlideshow is null) return;
        if (_currentSlide is not null) StashLive(_slidePages[_currentSlide]);
        LoadLive(_beforeSlideshow);
        _beforeSlideshow = null;
        _currentSlide = null;
    }

    public void ForgetSlides() => _slidePages.Clear();

    // ------------------------------------------------------------------ Swapping content

    private void StashLive(InkPage page)
    {
        CommitTextEditor();
        ClearSelection();
        CancelMouseStroke();
        CancelShapePreview();
        DropFadingItems();
        page.Strokes = InkSurface.Strokes;
        page.Shapes = ShapeSurface.Children.Cast<UIElement>().ToList();
        page.History = _history.ToList();
    }

    private void LoadLive(InkPage page)
    {
        InkSurface.Strokes = page.Strokes;
        ShapeSurface.Children.Clear();
        foreach (var shape in page.Shapes) ShapeSurface.Children.Add(shape);
        _history.Clear();
        _history.AddRange(page.History);
        _removed.Clear();
        if (!IsVisible && !page.IsEmpty) Show();
        if (_settings.Tool == ToolKind.Cursor && !HasVisibleContent) Hide();
        RefreshPageBar();
    }

    /// <summary>All numbered pages with the live one up to date (for export and saving).</summary>
    public IReadOnlyList<InkPage> SnapshotPages()
    {
        if (!IsFollowingSlides) StashLive(_pages[_pageIndex]);
        return _pages.ToList();
    }

    /// <summary>Replaces the numbered pages (opening a saved file).</summary>
    public void ReplacePages(IReadOnlyList<InkPage> pages)
    {
        if (IsFollowingSlides) EndSlides();
        _pages.Clear();
        _pages.AddRange(pages.Count > 0 ? pages : [new InkPage()]);
        _pageIndex = 0;
        LoadLive(_pages[0]);
    }

    // ------------------------------------------------------------------ Page bar

    private Border? _pageBar;
    private TextBlock? _pageLabel;

    /// <summary>"‹ Page 2 of 3 ›  + New page" at the bottom while a board is showing or there is more than one page.</summary>
    public void RefreshPageBar()
    {
        var show = !IsZoomViewActive && (IsFollowingSlides || _board != BoardKind.Transparent || _pages.Count > 1);
        if (!show)
        {
            if (_pageBar is not null) _pageBar.Visibility = Visibility.Collapsed;
            return;
        }
        _pageBar ??= BuildPageBar();
        _pageLabel!.Text = IsFollowingSlides ? $"Slide {_currentSlide?.Split('#').LastOrDefault()} drawings" : $"Page {_pageIndex + 1} of {_pages.Count}";
        foreach (var child in ((StackPanel)_pageBar.Child).Children.OfType<Button>())
            child.Visibility = IsFollowingSlides ? Visibility.Collapsed : Visibility.Visible;
        _pageBar.Visibility = Visibility.Visible;
        _pageBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        Canvas.SetLeft(_pageBar, Math.Max(8, (width - _pageBar.DesiredSize.Width) / 2));
        Canvas.SetTop(_pageBar, Math.Max(8, height - _pageBar.DesiredSize.Height - 56));
    }

    private Border BuildPageBar()
    {
        Button Action(string icon, string? label, string tip, Action run)
        {
            UIElement content = label is null ? new UI.Controls.LucideIcon(icon, 16) : UI.Theme.DK.IconLabel(icon, 14, label, 12, spacing: 6);
            var b = UI.Theme.DK.Button(content, UI.Theme.Tw.B(Colors.Transparent), UI.Theme.Tw.B(UI.Theme.Tw.Slate200), UI.Theme.Tw.B(UI.Theme.Tw.Slate700), UI.Theme.Tw.B(Colors.White), 8, new Thickness(8, 5, 8, 5));
            b.ToolTip = tip;
            b.Click += (_, _) => run();
            return b;
        }
        _pageLabel = new TextBlock { Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
        var row = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Children =
            {
                Action("ChevronLeft", null, "Previous page (Page Up)", () => _manager?.PreviousPage()),
                _pageLabel,
                Action("ChevronRight", null, "Next page (Page Down)", () => _manager?.NextPage()),
                Action("Plus", "New page", "Add a blank page", () => _manager?.AddPage()),
            }
        };
        var bar = new Border
        {
            Child = row,
            Background = UI.Theme.Tw.B(UI.Theme.Tw.Slate900, 0.92),
            BorderBrush = UI.Theme.Tw.B(UI.Theme.Tw.Slate600, 0.6),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(6, 4, 6, 4),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Opacity = 0.45, Color = Colors.Black }
        };
        ChromeSurface.Children.Add(bar);
        return bar;
    }

    // ------------------------------------------------------------------ Rendering and files

    /// <summary>Renders one page on the given background (the board colour, or white for the plain screen).</summary>
    public BitmapSource RenderPage(InkPage page, Brush background)
    {
        var width = Math.Max(1, ActualWidth);
        var height = Math.Max(1, ActualHeight);
        var ink = new InkPresenter { Strokes = page.Strokes };
        var shapes = new Canvas();
        var root = new Grid { Width = width, Height = height, Background = background };
        root.Children.Add(ink);
        root.Children.Add(shapes);
        // Shapes belong to the live surface; render copies so the page on screen is not disturbed.
        foreach (var shape in page.Shapes)
            if (XamlClone(shape) is UIElement copy) shapes.Children.Add(copy);
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * dpi.DpiScaleX), (int)Math.Ceiling(height * dpi.DpiScaleY),
            96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(root);
        bitmap.Freeze();
        ink.Strokes = new StrokeCollection();
        return bitmap;
    }

    public Brush PageBackground(bool forExport)
    {
        if (BoardSurface.Background is SolidColorBrush { Color.A: > 0 } or ImageBrush or DrawingBrush or VisualBrush)
            return BoardSurface.Background;
        return forExport ? Brushes.White : Brushes.Transparent;
    }

    public static UIElement? XamlClone(UIElement element)
    {
        try { return XamlReader.Parse(XamlWriter.Save(element)) as UIElement; }
        catch (Exception ex) when (ex is XamlParseException or InvalidOperationException or IOException) { return null; }
    }

    /// <summary>Ink saved as ISF bytes, with the pen style kept so special pens look the same when reopened.</summary>
    public static byte[] SaveStrokes(StrokeCollection strokes)
    {
        var copy = new StrokeCollection();
        foreach (var stroke in strokes)
        {
            var clone = stroke.Clone();
            if (stroke is StyledStroke styled) clone.AddPropertyData(PenModeProperty, (int)styled.Mode);
            copy.Add(clone);
        }
        using var stream = new MemoryStream();
        copy.Save(stream);
        return stream.ToArray();
    }

    public static StrokeCollection LoadStrokes(byte[] data)
    {
        using var stream = new MemoryStream(data);
        var loaded = new StrokeCollection(stream);
        var result = new StrokeCollection();
        foreach (var stroke in loaded)
        {
            if (stroke.ContainsPropertyData(PenModeProperty) && stroke.GetPropertyData(PenModeProperty) is int mode)
            {
                stroke.RemovePropertyData(PenModeProperty);
                result.Add(new StyledStroke(stroke.StylusPoints, stroke.DrawingAttributes, (PenMode)mode));
            }
            else result.Add(stroke);
        }
        return result;
    }
}
