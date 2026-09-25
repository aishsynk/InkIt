using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenCanvas.Commands;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Colors = System.Windows.Media.Colors;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;
using TextElement = System.Windows.Documents.TextElement;

namespace ScreenCanvas.UI;

/// <summary>Command Palette (Ctrl+K / Ctrl+Shift+P) - design: CommandPaletteModal.tsx.</summary>
public sealed class CommandPaletteWindow : ModalHost
{
    private readonly CommandRegistry _registry;
    private readonly TextBox _query;
    private readonly StackPanel _results = new();
    private readonly ScrollViewer _scroll;
    private List<CommandItem> _filtered = [];
    private int _selected;

    public CommandPaletteWindow(CommandRegistry registry) : base(closeOnBackdropClick: true)
    {
        _registry = registry;
        Title = "Command Palette";

        _query = new TextBox
        {
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 14,
            FontFamily = DK.Font,
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null
        };
        _query.SetResourceReference(TextBox.ForegroundProperty, "Ink.Text");
        _query.SetResourceReference(TextBox.CaretBrushProperty, "Ink.Text");
        var hint = DK.Text("Type a command, tool, or shortcut (e.g., 'pen', 'zoom', 'break')...", 14, "Ink.Text500");
        hint.IsHitTestVisible = false;
        hint.Margin = new Thickness(2, 0, 0, 0);
        _query.TextChanged += (_, _) => { hint.Visibility = _query.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; _selected = 0; Render(); };
        _query.PreviewKeyDown += OnQueryKeyDown;

        var input = new DockPanel();
        var searchIcon = new LucideIcon("Search", 20) { Foreground = Tw.B(Tw.Blue400), Margin = new Thickness(0, 0, 12, 0) };
        var esc = DK.Kbd("Esc", "Ink.Text400", "Ink.Control", "Ink.BorderStrong", 10, new Thickness(8, 2, 8, 2));
        DockPanel.SetDock(searchIcon, Dock.Left);
        DockPanel.SetDock(esc, Dock.Right);
        input.Children.Add(searchIcon);
        input.Children.Add(esc);
        input.Children.Add(new Grid { Children = { _query, hint } });
        var searchBar = new Border { Padding = new Thickness(16, 12, 16, 12), BorderThickness = new Thickness(0, 0, 0, 1), Child = input };
        searchBar.SetResourceReference(Border.BackgroundProperty, "Ink.Sunken");
        searchBar.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");

        _scroll = new ScrollViewer
        {
            Style = (Style)FindResource("Ink.ScrollViewer"),
            MaxHeight = 320,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(8), Child = _results }
        };

        var footer = Footer(DK.Text("Use ↑ ↓ to choose · Enter to run · Esc to close", 11, "Ink.Text500"),
            DK.Text("Type what you want to do, e.g. zoom, screenshot, whiteboard", 11, "Ink.Text500"), new Thickness(16, 8, 16, 8));
        footer.SetResourceReference(Border.BackgroundProperty, "Ink.Footer");

        AddCard(DK.V(0, searchBar, _scroll, footer), 576, double.NaN, VerticalAlignment.Top, 96);
        Loaded += (_, _) => { _query.Focus(); Keyboard.Focus(_query); };
        Render();
    }

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                _selected = (_selected + 1) % Math.Max(1, _filtered.Count);
                Render();
                e.Handled = true;
                break;
            case Key.Up:
                _selected = (_selected - 1 + _filtered.Count) % Math.Max(1, _filtered.Count);
                Render();
                e.Handled = true;
                break;
            case Key.Enter:
                if (_selected < _filtered.Count) Run(_filtered[_selected]);
                e.Handled = true;
                break;
        }
    }

    private void Run(CommandItem command)
    {
        Close();
        Dispatcher.BeginInvoke(command.Execute);
    }

    private void Render()
    {
        var q = _query.Text.Trim();
        _filtered = _registry.Commands.Where(c => q.Length == 0 ||
            c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            c.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            c.Category.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
            c.SearchTags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
            c.Shortcut?.Contains(q, StringComparison.OrdinalIgnoreCase) == true).ToList();
        _results.Children.Clear();
        if (_filtered.Count == 0)
        {
            var empty = DK.Text("No matching InkIt commands found.", 12, "Ink.Text500");
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.Margin = new Thickness(0, 32, 0, 32);
            _results.Children.Add(empty);
            return;
        }
        _selected = Math.Clamp(_selected, 0, _filtered.Count - 1);
        for (var i = 0; i < _filtered.Count; i++) _results.Children.Add(Row(_filtered[i], i, i == _selected));
        if (_results.Children[_selected] is FrameworkElement selected) selected.BringIntoView();
    }

    private FrameworkElement Row(CommandItem command, int index, bool selected)
    {
        var white = Tw.B(Colors.White);
        var tile = DK.Surface(new LucideIcon("Compass", 16), selected ? Tw.B(Colors.White, 0.2) : "Ink.Control", Tw.B(Colors.Transparent), 8, new Thickness(6), 0);
        if (selected) tile.SetValue(TextElement.ForegroundProperty, white); else tile.SetResourceReference(TextElement.ForegroundProperty, "Ink.Text400");
        var name = DK.Text(command.Name, 12, selected ? white : "Ink.Text300", FontWeights.SemiBold);
        var chip = selected
            ? DK.Chip(command.Category.ToString(), Tw.B(Colors.White, 0.1), white, Tw.B(Colors.White, 0.2), 10, 4, new Thickness(6, 0, 6, 0))
            : DK.Chip(command.Category.ToString(), "Ink.Control", "Ink.Text400", "Ink.BorderStrong", 10, 4, new Thickness(6, 0, 6, 0));
        chip.Margin = new Thickness(8, 0, 0, 0);
        var description = DK.Text(command.Description, 11, selected ? Tw.B(Tw.Blue100) : "Ink.Text500");
        description.MaxWidth = 384;
        description.HorizontalAlignment = HorizontalAlignment.Left;
        var left = DK.H(12, tile, DK.V(0, DK.H(0, name, chip), description));
        FrameworkElement right = command.Shortcut is null
            ? new Border()
            : selected
                ? DK.Kbd(command.Shortcut, white, Tw.B(Colors.White, 0.2), Tw.B(Colors.White, 0.3), 10, new Thickness(8, 2, 8, 2))
                : DK.Kbd(command.Shortcut, "Ink.Text400", "Ink.Control", "Ink.BorderStrong", 10, new Thickness(8, 2, 8, 2));
        var row = DK.Surface(DK.Between(left, right), selected ? Tw.B(Tw.Blue600) : Tw.B(Colors.Transparent), Tw.B(Colors.Transparent), 12, new Thickness(12, 10, 12, 10), 0);
        row.Cursor = Cursors.Hand;
        row.MouseEnter += (_, _) =>
        {
            if (_selected == index) return;
            _selected = index;
            Render();
        };
        row.MouseLeftButtonUp += (_, _) => Run(command);
        return row;
    }
}
