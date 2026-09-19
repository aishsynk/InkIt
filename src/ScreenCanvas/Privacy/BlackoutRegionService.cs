using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenCanvas.Capture;
using ScreenCanvas.Interop;
using ScreenCanvas.Presentation;
using PixelRectangle = System.Drawing.Rectangle;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace ScreenCanvas.Privacy;

/// <summary>Places an opaque, pixel-aligned privacy mask over a selected screen region.</summary>
public sealed class BlackoutRegionService : IDisposable
{
    private BlackoutRegionWindow? _window;

    public bool IsVisible => _window?.IsVisible == true;

    public bool ShowInteractive(Window? owner = null)
    {
        Hide();
        var selector = new RegionSelectionWindow { Owner = owner };
        if (selector.ShowDialog() != true || selector.SelectedRegion is null)
            return false;

        Show(selector.SelectedRegion.PixelBounds);
        return true;
    }

    public void Show(PixelRectangle pixelBounds)
    {
        if (pixelBounds.Width < 2 || pixelBounds.Height < 2)
            throw new ArgumentOutOfRangeException(nameof(pixelBounds), "Blackout bounds must have positive dimensions.");

        Hide();
        _window = new BlackoutRegionWindow(pixelBounds);
        _window.Closed += WindowClosed;
        _window.Show();
        _window.Activate();
    }

    public void Hide()
    {
        var window = _window;
        _window = null;
        if (window is null) return;
        window.Closed -= WindowClosed;
        window.Close();
    }

    public void Dispose() => Hide();

    private void WindowClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_window, sender)) _window = null;
    }
}

internal sealed class BlackoutRegionWindow : EmergencySafeWindow
{
    private const int EscapeHotkeyId = 0x5344;
    private readonly PixelRectangle _pixelBounds;
    private HwndSource? _source;
    private bool _escapeRegistered;

    internal BlackoutRegionWindow(PixelRectangle pixelBounds)
    {
        _pixelBounds = pixelBounds;
        Background = MediaBrushes.Black;
        AllowsTransparency = false;
        Focusable = true;
        Content = new Border
        {
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(36, 36, 39)),
            BorderThickness = new Thickness(1),
            Background = MediaBrushes.Black,
            Child = new TextBlock
            {
                Text = "PRIVATE",
                Foreground = new SolidColorBrush(MediaColor.FromRgb(112, 112, 118)),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 5, 8, 5),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            }
        };
        Closed += (_, _) => ReleaseEscapeHotkey();
        Loaded += (_, _) => Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowMessage);
        _escapeRegistered = NativeMethods.RegisterHotKey(handle, EscapeHotkeyId, NativeMethods.ModNoRepeat, 0x1B);

        NativeMethods.SetWindowPos(handle, nint.Zero, _pixelBounds.X, _pixelBounds.Y,
            _pixelBounds.Width, _pixelBounds.Height, NativeMethods.SwpNoZOrder);
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != NativeMethods.WmHotkey || wParam.ToInt32() != EscapeHotkeyId) return nint.Zero;
        handled = true;
        Close();
        return nint.Zero;
    }

    private void ReleaseEscapeHotkey()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (_escapeRegistered && handle != nint.Zero) NativeMethods.UnregisterHotKey(handle, EscapeHotkeyId);
        _escapeRegistered = false;
        if (_source is not null) _source.RemoveHook(WindowMessage);
        _source = null;
    }
}
