using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenCanvas.Commands;
using ScreenCanvas.Settings;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Button = System.Windows.Controls.Button;
using Colors = System.Windows.Media.Colors;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;
using TextElement = System.Windows.Documents.TextElement;

namespace ScreenCanvas.UI;

/// <summary>Capability Centre (F10) - design: CapabilityCentreModal.tsx.</summary>
public sealed class CapabilityCentreWindow : ModalHost
{
    private readonly CommandRegistry _registry;
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly TextBox _search;
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal };
    private readonly Grid _grid = new();
    private readonly ScrollViewer _scroll;
    private CapabilityCategory? _category;

    public CapabilityCentreWindow(CommandRegistry registry, AppSettings settings, Action save) : base(closeOnBackdropClick: false)
    {
        _registry = registry;
        _settings = settings;
        _save = save;
        Title = "Capability Centre";

        var domains = registry.Commands.Select(c => c.Category).Distinct().Count();
        var header = Header("Compass", Tw.Purple400, "Capability Centre (F10)",
            DK.Text($"CommandRegistry · {registry.Commands.Count} commands across {domains} functional domains", 12, "Ink.Text400"));

        _search = DK.Input(string.Empty, "Search commands, shortcuts, tags...", Tw.B(Tw.Purple500), 12);
        _search.Padding = new Thickness(30, 6, 12, 6);
        _search.TextChanged += (_, _) => Render();
        var searchBox = new Grid { Width = 320, Children = { _search, new LucideIcon("Search", 16) { Foreground = Tw.B(Tw.Slate400), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10, 0, 0, 0) } } };

        var filterBar = new Border
        {
            Padding = new Thickness(24, 12, 24, 12),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = DK.Between(searchBox, new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _tabs, Margin = new Thickness(12, 0, 0, 0) })
        };
        filterBar.SetResourceReference(Border.BackgroundProperty, "Ink.Surface");
        filterBar.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");

        _grid.ColumnDefinitions.Add(new ColumnDefinition());
        _grid.ColumnDefinitions.Add(new ColumnDefinition());
        _scroll = new ScrollViewer
        {
            Style = (Style)FindResource("Ink.ScrollViewer"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(24), Child = _grid }
        };

        var escHint = DK.H(4, DK.Text("Press", 11, "Ink.Text400", mono: true),
            DK.Kbd("Esc", "Ink.Text300", "Ink.Control", "Ink.BorderStrong", 11, new Thickness(6, 2, 6, 2)),
            DK.Text("to close", 11, "Ink.Text400", mono: true));
        var footer = Footer(DK.Text("Click any command to execute or switch mode", 12, "Ink.Text400"), escHint);

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(filterBar, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(filterBar);
        layout.Children.Add(footer);
        layout.Children.Add(_scroll);
        AddCard(layout, 896, 620);

        Loaded += (_, _) => { _search.Focus(); Keyboard.Focus(_search); };
        Render();
    }

    private IEnumerable<CommandItem> Filtered()
    {
        var q = _search.Text.Trim();
        return _registry.Commands.Where(c =>
            (_category is null || c.Category == _category) &&
            (q.Length == 0 || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
             c.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
             c.SearchTags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
             c.Shortcut?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
    }

    private void Render()
    {
        _tabs.Children.Clear();
        Tab($"All ({_registry.Commands.Count})", null);
        foreach (var category in Enum.GetValues<CapabilityCategory>()) Tab(category.ToString(), category);

        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        var items = Filtered().ToList();
        for (var i = 0; i < items.Count; i++)
        {
            if (i % 2 == 0) _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var card = Card(items[i]);
            card.Margin = new Thickness(i % 2 == 0 ? 0 : 6, i < 2 ? 0 : 12, i % 2 == 0 ? 6 : 0, 0);
            Grid.SetRow(card, i / 2);
            Grid.SetColumn(card, i % 2);
            _grid.Children.Add(card);
        }
        if (items.Count == 0)
        {
            _grid.RowDefinitions.Add(new RowDefinition());
            var empty = DK.Text("No capabilities match your search.", 12, "Ink.Text500");
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.Margin = new Thickness(0, 32, 0, 0);
            Grid.SetColumnSpan(empty, 2);
            _grid.Children.Add(empty);
        }
        _scroll.ScrollToTop();
    }

    private void Tab(string label, CapabilityCategory? category)
    {
        var selected = _category == category;
        var text = DK.Plain(label, 12, FontWeights.Medium);
        var tab = selected
            ? DK.Button(text, Tw.B(Tw.Purple600), Tw.B(Colors.White), Tw.B(Tw.Purple600), Tw.B(Colors.White), 8, new Thickness(category is null ? 12 : 10, 4, category is null ? 12 : 10, 4))
            : DK.Button(text, Tw.B(Colors.Transparent), "Ink.Text400", "Ink.Hover", "Ink.Text200", 8, new Thickness(category is null ? 12 : 10, 4, category is null ? 12 : 10, 4));
        if (selected) tab.Effect = DK.Shadow(6, 1, 0.3);
        if (_tabs.Children.Count > 0) tab.Margin = new Thickness(4, 0, 0, 0);
        tab.Click += (_, _) => { _category = category; Render(); };
        _tabs.Children.Add(tab);
    }

    private FrameworkElement Card(CommandItem command)
    {
        var isFavourite = _settings.FavoriteCommands.Contains(command.Id);
        var iconTile = DK.Surface(new LucideIcon(command.IconKey, 16), "Ink.Control", Tw.B(Colors.Transparent), 8, new Thickness(8), 0);
        iconTile.SetResourceReference(TextElement.ForegroundProperty, "Ink.Text300");
        iconTile.VerticalAlignment = VerticalAlignment.Top;
        var name = DK.Text(command.Name, 12, "Ink.Text", FontWeights.SemiBold);
        var categoryChip = DK.Chip(command.Category.ToString(), "Ink.Control", "Ink.Text400", "Ink.BorderStrong", 10, 4, new Thickness(6, 0, 6, 0));
        categoryChip.Margin = new Thickness(8, 0, 0, 0);
        var description = DK.Text(command.Description, 11, "Ink.Text400").Wrap(17);
        description.MaxHeight = 36;
        description.Margin = new Thickness(0, 4, 0, 0);
        var body = DK.V(0, DK.H(0, name, categoryChip), description);
        var left = new DockPanel();
        DockPanel.SetDock(iconTile, Dock.Left);
        body.Margin = new Thickness(12, 0, 0, 0);
        left.Children.Add(iconTile);
        left.Children.Add(body);

        var star = new LucideIcon("Star", 14) { Foreground = isFavourite ? Tw.B(Tw.Amber400) : Tw.B(Tw.Slate600), Fill = isFavourite ? Tw.B(Tw.Amber400) : null };
        var starButton = DK.Button(star, Tw.B(Colors.Transparent), Tw.B(Tw.Slate600), Tw.B(Colors.Transparent), Tw.B(Tw.Amber400), 4, new Thickness(4));
        starButton.ToolTip = isFavourite ? "Remove from favourites" : "Add to favourites";
        starButton.Click += (_, e) =>
        {
            e.Handled = true;
            if (!_settings.FavoriteCommands.Remove(command.Id)) _settings.FavoriteCommands.Add(command.Id);
            _save();
            Render();
        };
        var right = DK.V(8, starButton);
        starButton.HorizontalAlignment = HorizontalAlignment.Right;
        if (command.Shortcut is not null)
        {
            var kbd = DK.Kbd(command.Shortcut, Tw.B(Tw.Purple300), "Ink.Kbd", "Ink.BorderStrong", 10, new Thickness(8, 2, 8, 2));
            kbd.HorizontalAlignment = HorizontalAlignment.Right;
            right.Children.Add(kbd);
        }
        right.Margin = new Thickness(8, 0, 0, 0);

        var row = new DockPanel();
        DockPanel.SetDock(right, Dock.Right);
        row.Children.Add(right);
        row.Children.Add(left);

        var card = DK.Surface(row, "Ink.Raised40", "Ink.Divider", 12, new Thickness(14));
        card.Cursor = Cursors.Hand;
        card.MouseEnter += (_, _) =>
        {
            card.SetResourceReference(Border.BackgroundProperty, "Ink.RaisedHover");
            card.BorderBrush = Tw.B(Tw.Purple500, 0.5);
            iconTile.Background = Tw.B(Tw.Purple600, 0.2);
            iconTile.SetValue(TextElement.ForegroundProperty, Tw.B(Tw.Purple300));
            name.Foreground = Tw.B(Tw.Purple200);
        };
        card.MouseLeave += (_, _) =>
        {
            card.SetResourceReference(Border.BackgroundProperty, "Ink.Raised40");
            card.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");
            iconTile.SetResourceReference(Border.BackgroundProperty, "Ink.Control");
            iconTile.SetResourceReference(TextElement.ForegroundProperty, "Ink.Text300");
            name.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Text");
        };
        card.MouseLeftButtonUp += (_, _) =>
        {
            Close();
            Dispatcher.BeginInvoke(command.Execute);
        };
        return card;
    }
}
