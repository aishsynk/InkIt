import sys

with open('src/ScreenCanvas/UI/InspectorWindow.xaml.cs', 'r') as f:
    lines = f.readlines()

new_method = '''    private void BuildTextInspector()
    {
        bool isHoriz = _owner.IsHorizontal;

        // Font style row: Bold, Italic, Underline
        var styleRow = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = isHoriz ? new Thickness(0, 0, 6, 0) : new Thickness(0, 0, 0, 4)
        };
        styleRow.Children.Add(CreateTextStylePill("B", _overlay.Settings.TextBold, true, () =>
        {
            _overlay.Settings.TextBold = !_overlay.Settings.TextBold;
            RebuildContent();
            Reposition();
        }));
        styleRow.Children.Add(CreateTextStylePill("I", _overlay.Settings.TextItalic, false, () =>
        {
            _overlay.Settings.TextItalic = !_overlay.Settings.TextItalic;
            RebuildContent();
            Reposition();
        }));
        styleRow.Children.Add(CreateTextStylePill("U", _overlay.Settings.TextUnderline, false, () =>
        {
            _overlay.Settings.TextUnderline = !_overlay.Settings.TextUnderline;
            RebuildContent();
            Reposition();
        }));
        ContentHost.Children.Add(styleRow);

        ContentHost.Children.Add(CreateDivider());

'''

lines.insert(399, new_method)

with open('src/ScreenCanvas/UI/InspectorWindow.xaml.cs', 'w') as f:
    f.writelines(lines)
