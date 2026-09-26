using System.Drawing;
using Forms = System.Windows.Forms;

namespace ScreenCanvas.UI;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayService(Action showToolbar, Action annotate, Action clear, Action exit, IReadOnlyList<(string Label, Action Run)>? extras = null)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show toolbar", null, (_, _) => showToolbar());
        menu.Items.Add("Start / stop drawing", null, (_, _) => annotate());
        menu.Items.Add("Clear all drawings", null, (_, _) => clear());
        menu.Items.Add(new Forms.ToolStripSeparator());
        if (extras is { Count: > 0 })
        {
            foreach (var (label, run) in extras) menu.Items.Add(label, null, (_, _) => run());
            menu.Items.Add(new Forms.ToolStripSeparator());
        }
        menu.Items.Add("Exit InkIt", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Text = $"InkIt {AppInfo.Version} - double-click to show the toolbar",
            Icon = LoadAppIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => showToolbar();
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/InkIt.ico"));
            if (info is not null)
            {
                using var stream = info.Stream;
                return new Icon(stream, Forms.SystemInformation.SmallIconSize);
            }
        }
        catch (System.IO.IOException) { }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
