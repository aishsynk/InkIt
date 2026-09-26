using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ScreenCanvas.Support;

/// <summary>
/// Puts InkIt's teaching tools on an XP-Pen tablet's express keys by writing the XP-Pen driver's own settings
/// (%APPDATA%\XPPen\config.xml). A backup is made first and can be restored. The driver is closed while the file is
/// written and started again afterwards, because it keeps the settings in memory and saves them on exit.
/// </summary>
public static class XpPenSetup
{
    /// <summary>One express key: what it does in InkIt and the shortcut the tablet sends.</summary>
    public sealed record KeyAssignment(string Tool, string Shortcut, string Codes);

    // Windows virtual-key : scan-code pairs, the format the XP-Pen driver stores.
    private const string Ctrl = "17:29", Alt = "18:56", Shift = "16:42";

    /// <summary>Top-to-bottom layout for teaching; tablets with fewer keys get the first ones.</summary>
    public static IReadOnlyList<KeyAssignment> Layout { get; } =
    [
        new("Pointer", "Ctrl+Alt+Shift+C", $"{Ctrl}+{Alt}+{Shift}+67:46"),
        new("Pen", "Ctrl+Alt+Shift+P", $"{Ctrl}+{Alt}+{Shift}+80:25"),
        new("Highlighter", "Ctrl+Alt+Shift+H", $"{Ctrl}+{Alt}+{Shift}+72:35"),
        new("Eraser", "Ctrl+Alt+Shift+E", $"{Ctrl}+{Alt}+{Shift}+69:18"),
        new("Arrow", "Ctrl+Alt+Shift+A", $"{Ctrl}+{Alt}+{Shift}+65:30"),
        new("Circle", "Ctrl+Alt+Shift+O", $"{Ctrl}+{Alt}+{Shift}+79:24"),
        new("Undo", "Ctrl+Shift+Z", $"{Ctrl}+{Shift}+90:44"),
        new("Zoom into an area", "Ctrl+Shift+5", $"{Ctrl}+{Shift}+53:6"),
    ];

    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XPPen");
    private static string ConfigPath => Path.Combine(Folder, "config.xml");
    private static string BackupPath => Path.Combine(Folder, "config.before-inkit.xml");

    public static bool IsDriverInstalled => File.Exists(ConfigPath);
    public static bool HasBackup => File.Exists(BackupPath);

    /// <summary>The connected tablet, from the driver's log: (display name, settings section, number of keys).</summary>
    public static (string Name, string Section, int Keys)? DetectTablet()
    {
        try
        {
            var log = Path.Combine(Folder, "log.txt");
            if (!File.Exists(log) || !File.Exists(ConfigPath)) return null;
            string? name = null, section = null;
            foreach (var line in File.ReadLines(log))
            {
                var match = Regex.Match(line, "EnmuTabeltDevice: \"([^\"]+)\" -- \"([^\"]+)\"");
                if (match.Success) { name = match.Groups[1].Value; section = match.Groups[2].Value; }
            }
            if (section is null) return null;
            var device = XDocument.Load(ConfigPath).Root?.Element(section);
            var keys = int.TryParse(device?.Element("DeviceInfo")?.Element("DeviceKeyNum")?.Value, out var n) ? n : 0;
            return device is null ? null : (name!, section, keys);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException) { return null; }
    }

    /// <summary>Writes the layout to the tablet's express keys. Returns a message for the user.</summary>
    public static string Apply()
    {
        if (DetectTablet() is not { } tablet) return "No XP-Pen tablet was found. Connect it, open the XP-Pen app once, then try again.";
        if (tablet.Keys == 0) return $"{tablet.Name} has no express keys. Use the pen's side button for the tool wheel instead.";
        return WithDriverClosed(() =>
        {
            if (!HasBackup) File.Copy(ConfigPath, BackupPath);
            var count = WriteLayout(ConfigPath, tablet.Section, tablet.Keys);
            return $"Done: the {count} keys on your {tablet.Name} now select InkIt tools (top key: Pointer). The XP-Pen app was restarted.";
        });
    }

    /// <summary>Writes the express-key layout into a driver settings file; returns how many keys were set.</summary>
    public static int WriteLayout(string configPath, string section, int keyCount)
    {
        // A surgical text edit: only this tablet's <K1>..<Kn> lines change, every other byte stays as the driver wrote it.
        var bytes = File.ReadAllBytes(configPath);
        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var text = new System.Text.UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));

        var sectionStart = text.IndexOf($"<{section}>", StringComparison.Ordinal);
        var sectionEnd = sectionStart < 0 ? -1 : text.IndexOf($"</{section}>", sectionStart, StringComparison.Ordinal);
        var common = sectionStart < 0 ? -1 : text.IndexOf("<CommonAPP>", sectionStart, StringComparison.Ordinal);
        var keysStart = common < 0 ? -1 : text.IndexOf("<K>", common, StringComparison.Ordinal);
        var keysEnd = keysStart < 0 ? -1 : text.IndexOf("</K>", keysStart, StringComparison.Ordinal);
        if (sectionEnd < 0 || keysStart < 0 || keysEnd < 0 || keysEnd > sectionEnd)
            throw new InvalidDataException("The XP-Pen settings for this tablet have no express keys.");

        var block = text[keysStart..keysEnd];
        var count = 0;
        for (var i = 1; i <= Math.Min(keyCount, Layout.Count); i++)
        {
            var element = new Regex($"<K{i}\\b([^>]*?)(/>|>.*?</K{i}>)", RegexOptions.Singleline);
            var match = element.Match(block);
            if (!match.Success) continue;
            string Attribute(string name, string fallback)
            {
                var m = Regex.Match(match.Groups[1].Value, $"\\b{name}=\"([^\"]*)\"");
                return m.Success ? m.Groups[1].Value : fallback;
            }
            var assignment = Layout[i - 1];
            var value = System.Security.SecurityElement.Escape($"1|{assignment.Shortcut}|{assignment.Shortcut}|{assignment.Codes}");
            var replacement = $"<K{i} Show=\"1\" Actid=\"{Attribute("Actid", i.ToString())}\" Motid=\"{Attribute("Motid", i.ToString())}\" id=\"1\">{value}</K{i}>";
            block = block[..match.Index] + replacement + block[(match.Index + match.Length)..];
            count++;
        }
        text = text[..keysStart] + block + text[keysEnd..];
        var output = new System.Text.UTF8Encoding(false).GetBytes(text);
        File.WriteAllBytes(configPath, bom ? [0xEF, 0xBB, 0xBF, .. output] : output);
        return count;
    }

    /// <summary>Puts back the XP-Pen settings from before InkIt changed them.</summary>
    public static string Restore()
    {
        if (!HasBackup) return "There is nothing to restore.";
        return WithDriverClosed(() =>
        {
            File.Copy(BackupPath, ConfigPath, overwrite: true);
            File.Delete(BackupPath);
            return "Your earlier XP-Pen key settings are back. The XP-Pen app was restarted.";
        });
    }

    private static string WithDriverClosed(Func<string> change)
    {
        var driver = Process.GetProcessesByName("XPPenTablet").FirstOrDefault();
        var exe = driver?.MainModule?.FileName ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XPPen", "XPPenTablet.exe");
        try
        {
            if (driver is not null)
            {
                // Ask nicely first; the driver saves its settings when it exits, so it must be gone before we write.
                driver.CloseMainWindow();
                if (!driver.WaitForExit(4000)) { driver.Kill(); driver.WaitForExit(4000); }
            }
            return change();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "The XP-Pen settings could not be changed: " + ex.Message;
        }
        finally
        {
            if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
    }
}
