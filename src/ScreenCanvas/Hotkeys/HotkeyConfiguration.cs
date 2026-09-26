using System.Windows.Input;

namespace ScreenCanvas.Hotkeys;

public sealed record HotkeyBinding(string Action, ModifierKeys Modifiers, Key Key, bool Protected = false)
{
    public bool IsEmpty => Key == Key.None;
    public string DisplayText => IsEmpty ? "Unassigned" : $"{Modifiers}+{KeyName(Key)}".Replace("None+", string.Empty).Replace(", ", "+").Replace("Control", "Ctrl");

    private static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num" + (key - Key.NumPad0),
        Key.Delete => "Del",
        Key.Escape => "Esc",
        Key.Return => "Enter",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        _ => key.ToString()
    };
}

public sealed class HotkeyConfiguration
{
    public const string EmergencyAction = "Emergency release";
    public List<HotkeyBinding> Bindings { get; set; } = CreateDefaults();

    public static List<HotkeyBinding> CreateDefaults() =>
    [
        new(EmergencyAction, ModifierKeys.Alt | ModifierKeys.Shift, Key.X, true),
        new("Toggle drawing", ModifierKeys.Control | ModifierKeys.Shift, Key.D2),
        new("Undo", ModifierKeys.Control | ModifierKeys.Shift, Key.Z),
        new("Clear", ModifierKeys.Control | ModifierKeys.Shift, Key.Delete),
        new("Toggle snap-to-grid", ModifierKeys.Control | ModifierKeys.Shift, Key.G),
        new("Capture region", ModifierKeys.Control | ModifierKeys.Shift, Key.D4),
        new("Toggle zoom", ModifierKeys.Control | ModifierKeys.Shift, Key.D5)
    ];

    /// <summary>
    /// Global tool shortcuts meant for pen-tablet express keys (and handy on the keyboard): Ctrl+Alt+Shift + a letter.
    /// Stored as ordinary "cmd:" bindings, so they can be changed or removed in Settings.
    /// </summary>
    public static IReadOnlyList<HotkeyBinding> TabletDefaults { get; } =
    [
        new("cmd:tool/cursor", ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.C),
        new("cmd:tool/pen", ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.P),
        new("cmd:tool/highlighter", ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.H),
        new("cmd:tool/eraser", ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.E),
        new("cmd:tool/arrow", ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.A),
        new("cmd:tool/circle", ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.O),
    ];

    /// <summary>Adds the tablet tool shortcuts that are not there yet (skipping any key combination already in use).</summary>
    public void AddTabletDefaults()
    {
        foreach (var binding in TabletDefaults)
        {
            if (Bindings.Any(b => b.Action == binding.Action)) continue;
            if (Bindings.Any(b => !b.IsEmpty && b.Key == binding.Key && b.Modifiers == binding.Modifiers)) continue;
            Bindings.Add(binding);
        }
    }

    /// <summary>Adds default bindings for actions introduced after the settings file was written.</summary>
    public void EnsureDefaults()
    {
        foreach (var binding in CreateDefaults())
            if (Bindings.All(b => !string.Equals(b.Action, binding.Action, StringComparison.OrdinalIgnoreCase)))
                Bindings.Add(binding);
    }

    public HotkeyConflict? Validate(HotkeyBinding candidate)
    {
        if (candidate.Protected && (candidate.Action != EmergencyAction || candidate.IsEmpty))
            return new(candidate.Action, EmergencyAction, "The emergency shortcut cannot be removed.");
        var match = Bindings.FirstOrDefault(x => !string.Equals(x.Action, candidate.Action, StringComparison.OrdinalIgnoreCase)
            && !x.IsEmpty && x.Key == candidate.Key && x.Modifiers == candidate.Modifiers);
        return match is null ? null : new(candidate.Action, match.Action, $"{candidate.DisplayText} is already assigned to {match.Action}.");
    }

    public bool TrySet(HotkeyBinding binding, out HotkeyConflict? conflict)
    {
        var existing = Bindings.FindIndex(x => string.Equals(x.Action, binding.Action, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0 && Bindings[existing].Protected)
        {
            var protectedBinding = Bindings[existing];
            if (binding.Key != protectedBinding.Key || binding.Modifiers != protectedBinding.Modifiers)
            {
                conflict = new(binding.Action, EmergencyAction, "The emergency-release shortcut is protected.");
                return false;
            }
            binding = binding with { Protected = true };
        }
        conflict = Validate(binding);
        if (conflict is not null) return false;
        if (existing < 0) Bindings.Add(binding); else Bindings[existing] = binding;
        return true;
    }

    public bool Clear(string action)
    {
        var index = Bindings.FindIndex(x => string.Equals(x.Action, action, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || Bindings[index].Protected) return false;
        Bindings[index] = Bindings[index] with { Key = Key.None, Modifiers = ModifierKeys.None };
        return true;
    }

    public void Reset() => Bindings = CreateDefaults();
}

public sealed record HotkeyConflict(string Action, string ConflictingAction, string Message);
