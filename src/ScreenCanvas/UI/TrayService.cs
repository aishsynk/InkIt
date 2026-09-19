using System.Drawing;
using Forms = System.Windows.Forms;

namespace ScreenCanvas.UI;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayService(Action showToolbar, Action annotate, Action clear, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show toolbar", null, (_, _) => showToolbar());
        menu.Items.Add("Toggle annotation", null, (_, _) => annotate());
        menu.Items.Add("Clear annotations", null, (_, _) => clear());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Text = "ScreenCanvas",
            Icon = SystemIcons.Information,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => showToolbar();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
