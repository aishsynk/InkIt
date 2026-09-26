using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Colors = System.Windows.Media.Colors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ScreenCanvas.UI.Toolbar;

/// <summary>An action in a <see cref="NoticeCard"/>; the card closes after it runs.</summary>
public sealed record NoticeAction(string Label, Action Run, bool Primary = false);

/// <summary>
/// A small non-activating card with a title, a message and buttons: "update available", the first-run tour, ...
/// Shown near an anchor (below the toolbar button it talks about) or in the bottom-right corner.
/// </summary>
public sealed class NoticeCard : FloatingCard
{
    private FrameworkElement? _anchor;

    public NoticeCard(ToolbarWindow toolbar) : base(toolbar, 300, Tw.B(Tw.Blue500, 0.45), new Thickness(16)) { }

    public string? Step { get; private set; }

    public void Present(string icon, string title, string message, IReadOnlyList<NoticeAction> actions,
        FrameworkElement? anchor = null, string? step = null, Action? onDismiss = null)
    {
        _anchor = anchor;
        Step = step;
        var close = DK.IconButton("X", 14, "Ink.Text400", "Ink.Text", "Ink.Hover", 4, 4);
        close.ToolTip = "Close";
        close.Click += (_, _) => { Hide(); onDismiss?.Invoke(); };
        var heading = DK.H(8, new LucideIcon(icon, 18) { Foreground = Tw.B(Tw.Blue400) }, DK.Text(title, 14, "Ink.Text", FontWeights.SemiBold));
        var top = DK.Between(heading, close);
        var body = DK.Text(message, 12, "Ink.Text300").Wrap();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        if (step is not null) buttons.Children.Add(new TextBlock { Text = step, FontSize = 11, Foreground = Tw.B(Tw.Slate400), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        foreach (var action in actions)
        {
            var b = action.Primary
                ? DK.Button(DK.Plain(action.Label, 12, FontWeights.SemiBold), Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue500), Tw.B(Colors.White), 8, new Thickness(12, 6, 12, 6))
                : DK.Button(DK.Plain(action.Label, 12, FontWeights.Medium), "Ink.Control", "Ink.Text200", "Ink.ControlHover", "Ink.Text", 8, new Thickness(12, 6, 12, 6));
            b.Margin = new Thickness(6, 0, 0, 0);
            var run = action.Run;
            b.Click += (_, _) => { Hide(); run(); };
            buttons.Children.Add(b);
        }

        Card.Child = DK.V(10, top, body, buttons);
        Show();
        Reposition();
    }

    public override void Reposition()
    {
        if (!IsVisible) return;
        UpdateLayout();
        if (_anchor is { IsVisible: true } && PresentationSource.FromVisual(_anchor) is not null)
        {
            var r = ScreenRectDip(_anchor);
            PlaceCard(r.Left + r.Width / 2 - Card.ActualWidth / 2, r.Bottom + 14);
            return;
        }
        var area = SystemParameters.WorkArea;
        PlaceCard(area.Right - Card.ActualWidth - 16, area.Bottom - Card.ActualHeight - 16);
    }
}
