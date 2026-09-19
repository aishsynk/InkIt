using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ScreenCanvas.Interop;

namespace ScreenCanvas.Hotkeys;

public sealed class HotkeyManager : IDisposable
{
    private const int EmergencyId = 0x5101;
    private const int ToggleDrawId = 0x5102;
    private const int UndoId = 0x5103;
    private const int ClearId = 0x5104;
    private const int EscapeId = 0x5105;
    private readonly Window _owner;
    private HwndSource? _source;
    private nint _handle;

    public event EventHandler? EmergencyStop;
    public event EventHandler? ToggleDrawing;
    public event EventHandler? Undo;
    public event EventHandler? Clear;
    public event EventHandler? EscapePressed;
    private bool _escapeRegistered;

    public HotkeyManager(Window owner) => _owner = owner;

    public void RegisterDefaults(HotkeyConfiguration? config = null)
    {
        _handle = new WindowInteropHelper(_owner).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(WindowProc);
        RegisterFromConfiguration(config ?? new HotkeyConfiguration());
    }

    public void Reconfigure(HotkeyConfiguration config)
    {
        if (_handle == nint.Zero) return;
        foreach (var id in new[] { EmergencyId, ToggleDrawId, UndoId, ClearId, EscapeId })
            NativeMethods.UnregisterHotKey(_handle, id);
        _escapeRegistered = false;
        RegisterFromConfiguration(config);
    }

    private void RegisterFromConfiguration(HotkeyConfiguration config)
    {
        foreach (var binding in config.Bindings)
        {
            if (binding.IsEmpty) continue;
            var id = GetActionId(binding.Action);
            if (id is null) continue;
            Register(id.Value, ToNativeModifiers(binding.Modifiers), binding.Key);
        }
    }

    private static int? GetActionId(string action) => action switch
    {
        HotkeyConfiguration.EmergencyAction => EmergencyId,
        "Toggle drawing" => ToggleDrawId,
        "Undo" => UndoId,
        "Clear" => ClearId,
        _ => null
    };

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        uint result = NativeMethods.ModNoRepeat;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= NativeMethods.ModAlt;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= NativeMethods.ModControl;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= NativeMethods.ModShift;
        return result;
    }

    private void Register(int id, uint modifiers, Key key)
    {
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!NativeMethods.RegisterHotKey(_handle, id, modifiers, virtualKey))
            throw new Win32Exception($"The default global hotkey for {key} is already in use.");
    }

    public void SetEscapeEnabled(bool enabled)
    {
        if (_handle == nint.Zero || enabled == _escapeRegistered) return;
        if (enabled)
        {
            _escapeRegistered = NativeMethods.RegisterHotKey(_handle, EscapeId, NativeMethods.ModNoRepeat,
                (uint)KeyInterop.VirtualKeyFromKey(Key.Escape));
        }
        else
        {
            NativeMethods.UnregisterHotKey(_handle, EscapeId);
            _escapeRegistered = false;
        }
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != NativeMethods.WmHotkey) return nint.Zero;
        handled = true;
        switch (wParam.ToInt32())
        {
            case EmergencyId: EmergencyStop?.Invoke(this, EventArgs.Empty); break;
            case ToggleDrawId: ToggleDrawing?.Invoke(this, EventArgs.Empty); break;
            case UndoId: Undo?.Invoke(this, EventArgs.Empty); break;
            case ClearId: Clear?.Invoke(this, EventArgs.Empty); break;
            case EscapeId: EscapePressed?.Invoke(this, EventArgs.Empty); break;
        }
        return nint.Zero;
    }

    public void Dispose()
    {
        if (_handle == nint.Zero) return;
        foreach (var id in new[] { EmergencyId, ToggleDrawId, UndoId, ClearId, EscapeId }) NativeMethods.UnregisterHotKey(_handle, id);
        _source?.RemoveHook(WindowProc);
    }
}
