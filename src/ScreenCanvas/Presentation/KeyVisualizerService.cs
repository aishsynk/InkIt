using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenCanvas.Interop;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace ScreenCanvas.Presentation;

public sealed class KeyVisualizerService : IDisposable
{
    private KeyVisualizerWindow? _window;
    private nint _hookId = nint.Zero;
    private LowLevelKeyboardProc? _proc;

    public bool IsEnabled => _hookId != nint.Zero;

    public void Toggle()
    {
        if (IsEnabled) Stop();
        else Start();
    }

    public void Start()
    {
        if (IsEnabled) return;
        _window = new KeyVisualizerWindow();
        _window.Show();

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var moduleHandle = GetModuleHandle(curModule?.ModuleName);
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, moduleHandle, 0);
    }

    public void Stop()
    {
        if (_hookId != nint.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = nint.Zero;
        }
        _proc = null;
        if (_window is not null)
        {
            _window.Close();
            _window = null;
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (wParam == (nint)WmKeydown || wParam == (nint)WmSyskeydown))
        {
            var vkCode = Marshal.ReadInt32(lParam);
            var isCtrl = (GetKeyState(VkControl) & 0x8000) != 0;
            var isShift = (GetKeyState(VkShift) & 0x8000) != 0;
            var isAlt = (GetKeyState(VkMenu) & 0x8000) != 0;
            var isWin = (GetKeyState(VkLwin) & 0x8000) != 0 || (GetKeyState(VkRwin) & 0x8000) != 0;

            // Only display modifier combinations or function/special keys to avoid noisy typing
            var keyStr = FormatKeyString(vkCode, isCtrl, isShift, isAlt, isWin);
            if (!string.IsNullOrEmpty(keyStr))
            {
                _window?.Dispatcher.BeginInvoke(() => _window.DisplayKey(keyStr));
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static string FormatKeyString(int vkCode, bool ctrl, bool shift, bool alt, bool win)
    {
        // Don't show standalone modifier keypresses
        if (vkCode is VkControl or VkLcontrol or VkRcontrol or VkShift or VkLshift or VkRshift or VkMenu or VkLmenu or VkRmenu or VkLwin or VkRwin)
            return string.Empty;

        var sb = new StringBuilder();
        if (ctrl) sb.Append("Ctrl + ");
        if (alt) sb.Append("Alt + ");
        if (shift) sb.Append("Shift + ");
        if (win) sb.Append("Win + ");

        var name = KeyCodeToString(vkCode);
        if (string.IsNullOrEmpty(name)) return string.Empty;

        // If no modifiers, only show significant presenter keys (F-keys, Esc, Enter, Tab, etc.)
        if (!ctrl && !alt && !win && !name.StartsWith("F", StringComparison.Ordinal) &&
            name is not ("Esc" or "Enter" or "Tab" or "Delete" or "Backspace" or "PageUp" or "PageDown" or "Home" or "End"))
        {
            return string.Empty;
        }

        sb.Append(name);
        return sb.ToString();
    }

    private static string KeyCodeToString(int vkCode) => vkCode switch
    {
        0x1B => "Esc",
        0x0D => "Enter",
        0x09 => "Tab",
        0x20 => "Space",
        0x2E => "Del",
        0x08 => "Backspace",
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x24 => "Home",
        0x23 => "End",
        >= 0x70 and <= 0x7B => $"F{vkCode - 0x70 + 1}",
        >= 0x30 and <= 0x39 => ((char)('0' + (vkCode - 0x30))).ToString(),
        >= 0x41 and <= 0x5A => ((char)('A' + (vkCode - 0x41))).ToString(),
        _ => string.Empty
    };

    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmSyskeydown = 0x0104;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLwin = 0x5B;
    private const int VkRwin = 0x5C;
    private const int VkLshift = 0xA0;
    private const int VkRshift = 0xA1;
    private const int VkLcontrol = 0xA2;
    private const int VkRcontrol = 0xA3;
    private const int VkLmenu = 0xA4;
    private const int VkRmenu = 0xA5;

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    public void Dispose() => Stop();
}

internal sealed class KeyVisualizerWindow : Window
{
    private readonly StackPanel _keys = new() { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly List<string> _recent = [];
    private readonly StackPanel _root;
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(4) };

    internal KeyVisualizerWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Focusable = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;

        // Design: header chip "Key Visualizer (WH_KEYBOARD_LL)" above the last three key chips.
        var header = UI.Theme.DK.Surface(
            UI.Theme.DK.H(4,
                new UI.Controls.LucideIcon("Keyboard", 12) { Foreground = UI.Theme.Tw.B(UI.Theme.Tw.Blue400) },
                UI.Theme.DK.Caps("Key Visualizer (WH_KEYBOARD_LL)", 10, UI.Theme.Tw.B(UI.Theme.Tw.Slate400), FontWeights.Normal)),
            UI.Theme.Tw.B(UI.Theme.Tw.Slate900, 0.8), UI.Theme.Tw.B(UI.Theme.Tw.Slate700, 0.6), 999, new Thickness(8, 2, 8, 2));
        ((TextBlock)((StackPanel)header.Child).Children[1]).FontFamily = UI.Theme.DK.Mono;
        header.HorizontalAlignment = HorizontalAlignment.Right;
        header.Effect = UI.Theme.DK.Shadow(12, 3, 0.4);
        _keys.Margin = new Thickness(0, 6, 0, 0);
        _root = new StackPanel { Margin = new Thickness(16), Children = { header, _keys }, Opacity = 0 };
        Content = _root;
        _idleTimer.Tick += (_, _) =>
        {
            _idleTimer.Stop();
            _recent.Clear();
            _root.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(250)));
        };
        SizeChanged += (_, _) => PositionWindow();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle,
            style | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate | NativeMethods.WsExTransparent);
    }

    private void PositionWindow()
    {
        Left = SystemParameters.WorkArea.Right - ActualWidth - 8;
        Top = SystemParameters.WorkArea.Bottom - ActualHeight - 32;
    }

    internal void DisplayKey(string text)
    {
        _recent.Add(text);
        if (_recent.Count > 3) _recent.RemoveRange(0, _recent.Count - 3);
        _keys.Children.Clear();
        foreach (var key in _recent)
        {
            var chip = UI.Theme.DK.Surface(UI.Theme.DK.Text(key, 12, UI.Theme.Tw.B(Colors.White), FontWeights.SemiBold, mono: true),
                UI.Theme.Tw.B(UI.Theme.Tw.Slate900, 0.9), UI.Theme.Tw.B(UI.Theme.Tw.Slate700), 8, new Thickness(10, 4, 10, 4));
            chip.Margin = new Thickness(6, 0, 0, 0);
            chip.Effect = UI.Theme.DK.Shadow(16, 4, 0.45);
            _keys.Children.Add(chip);
        }
        _root.BeginAnimation(OpacityProperty, null);
        _root.Opacity = 1;
        _idleTimer.Stop();
        _idleTimer.Start();
    }
}
