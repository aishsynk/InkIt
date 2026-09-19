using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScreenCanvas.Commands;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScreenCanvas.UI;

public partial class CommandPaletteWindow : Window
{
    private readonly CommandRegistry _registry;
    private readonly List<CommandPaletteItemViewModel> _allItems = [];

    public CommandPaletteWindow(CommandRegistry registry)
    {
        InitializeComponent();
        _registry = registry;

        foreach (var cmd in _registry.Commands)
        {
            var geom = TryFindResource(cmd.IconKey) as Geometry ?? (Geometry)FindResource("Fluent.Cursor.Regular");
            _allItems.Add(new CommandPaletteItemViewModel
            {
                Command = cmd,
                Name = cmd.Name,
                Description = cmd.Description,
                Category = cmd.Category.ToString(),
                Shortcut = cmd.Shortcut,
                HasShortcut = !string.IsNullOrEmpty(cmd.Shortcut),
                IconGeometry = geom
            });
        }

        FilterItems(string.Empty);
        Loaded += (_, _) =>
        {
            SearchBox.Focus();
        };
    }

    private void FilterItems(string query)
    {
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allItems
            : _allItems.Where(item => item.Command.Matches(query)).ToList();

        ResultsList.ItemsSource = filtered;
        if (filtered.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            StatusText.Text = $"{filtered.Count} commands available • Enter to execute • Esc to close";
        }
        else
        {
            StatusText.Text = "No matching commands found. Try 'pen', 'spotlight', 'timer', etc.";
        }
    }

    private void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is CommandPaletteItemViewModel vm)
        {
            Close();
            vm.Command.Execute();
        }
    }

    private void SearchBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        FilterItems(SearchBox.Text);
    }

    private void SearchBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (ResultsList.SelectedIndex < ResultsList.Items.Count - 1)
                ResultsList.SelectedIndex++;
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (ResultsList.SelectedIndex > 0)
                ResultsList.SelectedIndex--;
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
    }

    private void ResultsList_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void ResultsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelected();
    }
}

public sealed class CommandPaletteItemViewModel
{
    public required CommandItem Command { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public string? Shortcut { get; init; }
    public bool HasShortcut { get; init; }
    public required Geometry IconGeometry { get; init; }
}
