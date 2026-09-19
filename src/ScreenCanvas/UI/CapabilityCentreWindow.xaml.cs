using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenCanvas.Commands;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScreenCanvas.UI;

public partial class CapabilityCentreWindow : Window
{
    private readonly CommandRegistry _registry;
    private readonly List<CommandPaletteItemViewModel> _allItems = [];
    private readonly Action? _onOpenSettings;
    private readonly Action? _onOpenPresets;

    public CapabilityCentreWindow(CommandRegistry registry, Action? onOpenSettings = null, Action? onOpenPresets = null)
    {
        InitializeComponent();
        _registry = registry;
        _onOpenSettings = onOpenSettings;
        _onOpenPresets = onOpenPresets;

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

        PopulateCategories();
        Loaded += (_, _) => SearchBox.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    private void PopulateCategories()
    {
        CategoryList.Items.Add("All Tools");
        foreach (CapabilityCategory cat in Enum.GetValues(typeof(CapabilityCategory)))
        {
            CategoryList.Items.Add(cat.ToString());
        }
        CategoryList.SelectedIndex = 0;
    }

    private void ApplyFilters()
    {
        var query = SearchBox.Text.Trim();
        var selCategory = CategoryList.SelectedItem?.ToString();

        var queryResults = _allItems.AsEnumerable();

        if (!string.IsNullOrEmpty(selCategory) && selCategory != "All Tools")
        {
            queryResults = queryResults.Where(x => string.Equals(x.Category, selCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query))
        {
            queryResults = queryResults.Where(x => x.Command.Matches(query));
        }

        var list = queryResults.ToList();
        ToolsList.ItemsSource = list;
        FooterText.Text = $"{list.Count} capabilities available • Click to activate";
    }

    private void CategoryList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void SearchBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void ToolItem_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommandPaletteItemViewModel vm })
        {
            Close();
            vm.Command.Execute();
        }
    }

    private void ToolsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ToolsList.SelectedItem is CommandPaletteItemViewModel vm)
        {
            Close();
            vm.Command.Execute();
        }
    }

    private void PresetsButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
        _onOpenPresets?.Invoke();
    }

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
        _onOpenSettings?.Invoke();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
