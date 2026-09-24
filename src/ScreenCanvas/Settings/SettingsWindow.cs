using ScreenCanvas.Hotkeys;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfSlider = System.Windows.Controls.Slider;
using WpfBrushes = System.Windows.Media.Brushes;

namespace ScreenCanvas.Settings;

public sealed class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ISettingsStore _store;
    private readonly Action<HotkeyConfiguration>? _onSaved;
    private readonly WpfTextBox _search = new() { Margin = new Thickness(12), ToolTip = "Search settings" };
    private readonly WpfListBox _sections = new() { Width = 175, BorderThickness = new Thickness(0) };
    private readonly StackPanel _editor = new() { Margin = new Thickness(18) };
    private readonly TextBlock _status = new() { Foreground = WpfBrushes.OrangeRed, Margin = new Thickness(8, 0, 8, 0) };
    private readonly List<SectionItem> _allSections;

    public SettingsWindow(AppSettings settings, ISettingsStore store, Action<HotkeyConfiguration>? onSaved = null)
    {
        _settings = settings; _store = store; _onSaved = onSaved;
        Title = "ScreenCanvas Settings"; Width = 820; Height = 620; MinWidth = 680; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _allSections = BuildSections();

        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumnSpan(_search, 2); root.Children.Add(_search);
        var content = new Grid(); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) }); content.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(content, 1); root.Children.Add(content);
        content.Children.Add(_sections); var scroll = new ScrollViewer { Content = _editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(scroll, 1); content.Children.Add(scroll);
        var footer = new DockPanel { Margin = new Thickness(10) }; Grid.SetRow(footer, 2); root.Children.Add(footer); footer.Children.Add(_status);
        var save = new WpfButton { Content = "Save", Width = 88, Padding = new Thickness(8, 5, 8, 5) }; DockPanel.SetDock(save, Dock.Right); footer.Children.Add(save);
        Content = root;

        _search.TextChanged += (_, _) => Filter();
        _sections.SelectionChanged += (_, _) => ShowSelected();
        save.Click += async (_, _) => { ThemeManager.SetPreference(_settings.Appearance.Theme);await _store.SaveAsync(_settings); _onSaved?.Invoke(_settings.Hotkeys); DialogResult = true; Close(); };
        Filter(); _sections.SelectedIndex = 0;
    }

    private List<SectionItem> BuildSections()
    {
        var properties = typeof(AppSettings).GetProperties().Where(p => p.Name is not nameof(AppSettings.SchemaVersion));
        return properties.Select(p =>
        {
            var value = p.GetValue(_settings)!;
            var terms = p.Name + " " + string.Join(' ', value.GetType().GetProperties().Select(x => x.Name));
            return new SectionItem(SplitName(p.Name), value, terms);
        }).ToList();
    }

    private void Filter()
    {
        var query = _search.Text.Trim(); _sections.Items.Clear();
        foreach (var section in _allSections.Where(x => string.IsNullOrEmpty(query) || x.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase)))
            _sections.Items.Add(section);
        if (_sections.Items.Count > 0) _sections.SelectedIndex = 0;
    }

    private void ShowSelected()
    {
        _editor.Children.Clear(); _status.Text = string.Empty;
        if (_sections.SelectedItem is not SectionItem section) return;
        _editor.Children.Add(new TextBlock { Text = section.Name, FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        if (section.Value is HotkeyConfiguration hotkeys) { AddHotkeys(hotkeys); return; }
        foreach (var property in section.Value.GetType().GetProperties().Where(p => p.CanRead && p.CanWrite)) AddProperty(section.Value, property);
        if (section.Name == "Advanced") _editor.Children.Add(new TextBlock { Text = $"ScreenCanvas settings schema {_settings.SchemaVersion}", Foreground = WpfBrushes.Gray, Margin = new Thickness(0, 20, 0, 0) });
    }

    private static readonly Dictionary<(string Type, string Property), (double Min, double Max, double Tick)> NumericRanges = new()
    {
        [("SpotlightSettings", "Radius")] = (50, 300, 1),
        [("SpotlightSettings", "OverlayOpacity")] = (0.1, 0.9, 0.05),
        [("AppearanceSettings", "UiScale")] = (0.5, 2, 0.05),
        [("ToolbarSettings", "Opacity")] = (0.1, 1, 0.05),
    };

    private void AddProperty(object target, PropertyInfo property)
    {
        var row = new Grid { Margin = new Thickness(0, 4, 0, 8) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        row.Children.Add(new TextBlock { Text = SplitName(property.Name), VerticalAlignment = VerticalAlignment.Center });
        FrameworkElement editor;
        var value = property.GetValue(target);
        if (property.PropertyType == typeof(bool))
        {
            var box = new WpfCheckBox { IsChecked = (bool?)value, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            box.Checked += (_, _) => property.SetValue(target, true); box.Unchecked += (_, _) => property.SetValue(target, false); editor = box;
        }
        else if (property.PropertyType.IsEnum)
        {
            var combo = new WpfComboBox { ItemsSource = Enum.GetValues(property.PropertyType), SelectedItem = value };
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is not null) property.SetValue(target, combo.SelectedItem); }; editor = combo;
        }
        else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(int))
        {
            var typeName = target.GetType().Name;
            if (NumericRanges.TryGetValue((typeName, property.Name), out var range))
            {
                var numericValue = Convert.ToDouble(value);
                var slider = new Slider
                {
                    Minimum = range.Min,
                    Maximum = range.Max,
                    Value = numericValue,
                    TickFrequency = range.Tick,
                    IsSnapToTickEnabled = true,
                    TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
                    Width = 200,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left
                };
                var label = new TextBlock { Text = FormatNumericValue(numericValue, property.PropertyType), Width = 44, TextAlignment = System.Windows.TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                slider.ValueChanged += (_, e) =>
                {
                    var coerced = Math.Round(e.NewValue / range.Tick) * range.Tick;
                    if (property.PropertyType == typeof(int))
                        property.SetValue(target, (int)coerced);
                    else
                        property.SetValue(target, coerced);
                    label.Text = FormatNumericValue(coerced, property.PropertyType);
                };
                var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                panel.Children.Add(slider); panel.Children.Add(label);
                editor = panel;
            }
            else
            {
                var box = new WpfTextBox { Text = value?.ToString() ?? string.Empty };
                box.LostFocus += (_, _) => TryAssign(target, property, box.Text); editor = box;
            }
        }
        else
        {
            var box = new WpfTextBox { Text = value?.ToString() ?? string.Empty };
            box.LostFocus += (_, _) => TryAssign(target, property, box.Text); editor = box;
        }
        Grid.SetColumn(editor, 1); row.Children.Add(editor); _editor.Children.Add(row);
    }

    private static string FormatNumericValue(double value, Type targetType)
    {
        return targetType == typeof(int) ? ((int)value).ToString() : value.ToString("0.##");
    }

    private void AddHotkeys(HotkeyConfiguration hotkeys)
    {
        foreach (var binding in hotkeys.Bindings.ToArray())
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 8) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.Children.Add(new TextBlock { Text = binding.Action, VerticalAlignment = VerticalAlignment.Center });
            var capture = new WpfTextBox { Text = binding.DisplayText, IsReadOnly = true, Tag = binding, ToolTip = "Focus and press a shortcut" };
            capture.PreviewKeyDown += (_, e) => CaptureShortcut(hotkeys, capture, e); Grid.SetColumn(capture, 1); row.Children.Add(capture);
            var clear = new WpfButton { Content = "Clear", IsEnabled = !binding.Protected }; clear.Click += (_, _) => { hotkeys.Clear(binding.Action); ShowSelected(); }; Grid.SetColumn(clear, 2); row.Children.Add(clear); _editor.Children.Add(row);
        }
        var reset = new WpfButton { Content = "Reset defaults", Width = 120, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
        reset.Click += (_, _) => { hotkeys.Reset(); ShowSelected(); }; _editor.Children.Add(reset);
    }

    private void CaptureShortcut(HotkeyConfiguration hotkeys, WpfTextBox box, WpfKeyEventArgs e)
    {
        e.Handled = true; var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var original = (HotkeyBinding)box.Tag; var candidate = original with { Key = key, Modifiers = Keyboard.Modifiers };
        if (!hotkeys.TrySet(candidate, out var conflict)) { _status.Text = conflict!.Message; return; }
        _status.Text = string.Empty; box.Tag = candidate; box.Text = candidate.DisplayText;
    }

    private void TryAssign(object target, PropertyInfo property, string value)
    {
        try
        {
            object? converted = property.PropertyType == typeof(string) ? value : Convert.ChangeType(value, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
            property.SetValue(target, converted); _status.Text = string.Empty;
        }
        catch { _status.Text = $"Invalid value for {SplitName(property.Name)}."; }
    }

    private static string SplitName(string value) => System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");
    private sealed record SectionItem(string Name, object Value, string SearchText) { public override string ToString() => Name; }
}
