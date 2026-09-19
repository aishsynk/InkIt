using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ScreenCanvas.Interop;

namespace ScreenCanvas.Presentation;

/// <summary>Fullscreen presenter surface that can always be dismissed with Esc or Ctrl+Shift+F12.</summary>
internal abstract class EmergencySafeWindow : Window
{
    private const int EmergencyHotkeyId = 0x5343;
    private HwndSource? _source;

    protected EmergencySafeWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            ReleaseInputCapture();
            EmergencyExit();
        };
        PreviewMouseRightButtonDown += (_, e) => { e.Handled = true; EmergencyExit(); };
        Closed += (_, _) => ReleaseHotkey();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(WindowMessage);
        NativeMethods.RegisterHotKey(new WindowInteropHelper(this).Handle, EmergencyHotkeyId,
            NativeMethods.ModControl | NativeMethods.ModShift | NativeMethods.ModNoRepeat, 0x7B);
    }

    protected virtual void EmergencyExit() => Close();

    private static void ReleaseInputCapture()
    {
        if (Mouse.Captured is not null) Mouse.Capture(null);
        if (Stylus.Captured is not null) Stylus.Capture(null);
        if (Keyboard.FocusedElement is not null) Keyboard.ClearFocus();
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == EmergencyHotkeyId)
        {
            handled = true;
            EmergencyExit();
        }
        return nint.Zero;
    }

    private void ReleaseHotkey()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != nint.Zero) NativeMethods.UnregisterHotKey(handle, EmergencyHotkeyId);
        if (_source is not null) _source.RemoveHook(WindowMessage);
        _source = null;
    }
}
