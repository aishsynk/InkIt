using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ScreenCanvas.Hotkeys;
using ScreenCanvas.Overlay;
using ScreenCanvas.Settings;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Button = System.Windows.Controls.Button;
using Colors = System.Windows.Media.Colors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ScreenCanvas.UI;

/// <summary>InkIt Settings (820×620) - design: SettingsModal.tsx. Changes are saved as they are made.</summary>
public sealed class SettingsWindow : ModalHost
{
    private enum Tab { Appearance, Toolbar, Hotkeys, Presentation, Profiles, Audit, About }

    private readonly AppSettings _settings;
    private readonly ISettingsStore _store;
    private readonly IOverlayManager _overlay;
    private readonly Action<HotkeyConfiguration>? _hotkeysChanged;
    private readonly ToolbarWindow _toolbar;
    private readonly StackPanel _nav = new();
    private readonly ContentControl _content = new();
    private readonly DispatcherTimer _cpuTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Tab _tab = Tab.Appearance;
    private string? _capturingAction;
    private string _hotkeyStatus = string.Empty;
    private TimeSpan _lastCpu;
    private DateTime _lastSample;
    private TextBlock? _cpuText;

    public SettingsWindow(AppSettings settings, ISettingsStore store, IOverlayManager overlay, Action<HotkeyConfiguration>? hotkeysChanged, ToolbarWindow toolbar)
        : base(closeOnBackdropClick: false)
    {
        _settings = settings;
        _store = store;
        _overlay = overlay;
        _hotkeysChanged = hotkeysChanged;
        _toolbar = toolbar;
        Title = "InkIt Settings";

        var subtitle = DK.Text($"Version {AppInfo.Version}  ·  Changes are saved automatically", 12, "Ink.Text400");
        var header = Header("Settings", Tw.Blue400, "InkIt Settings", subtitle);

        var navHost = new Border { Width = 224, Padding = new Thickness(12), BorderThickness = new Thickness(0, 0, 1, 0), Child = _nav };
        navHost.SetResourceReference(Border.BackgroundProperty, "Ink.Sunken");
        navHost.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");
        var scroll = new ScrollViewer
        {
            Style = (Style)FindResource("Ink.ScrollViewer"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(24), Child = _content }
        };
        var body = new DockPanel();
        DockPanel.SetDock(navHost, Dock.Left);
        body.Children.Add(navHost);
        body.Children.Add(scroll);

        var done = DK.Button(DK.Plain("Done", 12, FontWeights.Medium), Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue500), Tw.B(Colors.White), 12, new Thickness(16, 6, 16, 6));
        done.Click += (_, _) => Close();
        var footer = Footer(DK.Text("Settings automatically save on change", 12, "Ink.Text400"), done);

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(footer);
        layout.Children.Add(body);
        AddCard(layout, 896, 620);

        PreviewKeyDown += OnCaptureKey;
        _cpuTimer.Tick += (_, _) => SampleCpu();
        Closed += (_, _) => _cpuTimer.Stop();
        Render();
    }

    private void Save() => _store.SaveAsync(_settings).GetAwaiter().GetResult();

    /// <summary>QA capture only: switches to the Hotkeys tab without input.</summary>
    internal void ShowTab(string name) { _tab = Enum.Parse<Tab>(name); Render(); }

    private void Render()
    {
        _nav.Children.Clear();
        foreach (var (tab, name, icon) in new[]
                 {
                     (Tab.Appearance, "Appearance", "Palette"), (Tab.Toolbar, "Toolbar & Presets", "Layout"),
                     (Tab.Hotkeys, "Keyboard Shortcuts", "Keyboard"), (Tab.Presentation, "Spotlight & Focus", "Eye"),
                     (Tab.Profiles, "Tool Memory", "Sliders"), (Tab.Audit, "Performance", "Cpu"), (Tab.About, "About & Feedback", "Info")
                 })
        {
            var active = _tab == tab;
            var content = DK.IconLabel(icon, 16, name, 12, FontWeights.Medium, 10);
            var button = active
                ? DK.Button(content, Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue600), Tw.B(Colors.White), 12, new Thickness(12, 8, 12, 8))
                : DK.Button(content, Tw.B(Colors.Transparent), "Ink.Text400", Tw.B(Tw.Slate800, 0.6), "Ink.Text200", 12, new Thickness(12, 8, 12, 8));
            if (active) button.Effect = DK.Shadow(6, 1, 0.3);
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            if (_nav.Children.Count > 0) button.Margin = new Thickness(0, 4, 0, 0);
            button.Click += (_, _) => { _tab = tab; _capturingAction = null; Render(); };
            _nav.Children.Add(button);
        }
        _cpuTimer.Stop();
        _content.Content = _tab switch
        {
            Tab.Appearance => Appearance(),
            Tab.Toolbar => ToolbarTab(),
            Tab.Hotkeys => Hotkeys(),
            Tab.Presentation => Presentation(),
            Tab.Profiles => Profiles(),
            Tab.About => About(),
            _ => Audit()
        };
    }

    // ---------------------------------------------------------------- helpers

    private static StackPanel Section(string title, UIElement? description, params UIElement[] body)
    {
        var header = DK.Text(title, 14, "Ink.Text", FontWeights.SemiBold);
        header.Margin = new Thickness(0, 0, 0, 4);
        var panel = DK.V(0, header);
        if (description is FrameworkElement d) { d.Margin = new Thickness(0, 0, 0, 12); panel.Children.Add(d); }
        foreach (var b in body) panel.Children.Add(b);
        return panel;
    }

    private static Border Ruled(UIElement child)
    {
        var rule = DK.TopRule(child, 16);
        rule.Margin = new Thickness(0, 24, 0, 0);
        return rule;
    }

    private static TextBlock CodeNote(params (string Text, bool Code)[] parts) =>
        DK.Rich(12, "Ink.Text400", parts.Select(p => (p.Text, p.Code ? (Paint?)"Ink.Code" : null, p.Code)).ToArray());

    private static Button Choice(UIElement content, bool selected, Action click, Thickness padding)
    {
        var b = selected
            ? DK.Button(content, "Ink.Selected", "Ink.Text", "Ink.Selected", "Ink.Text", 12, padding, Tw.B(Tw.Blue500), Tw.B(Tw.Blue500), 1)
            : DK.Button(content, "Ink.Raised40", "Ink.Text300", "Ink.Hover", "Ink.Text300", 12, padding, "Ink.Divider", "Ink.Divider", 1);
        b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        b.Click += (_, _) => click();
        return b;
    }

    // ---------------------------------------------------------------- tabs

    private UIElement Appearance()
    {
        var themes = new[] { AppTheme.Dark, AppTheme.Light, AppTheme.System }.Select(theme =>
        {
            var selected = _settings.Appearance.Theme == theme;
            var check = new LucideIcon("Check", 14) { Foreground = Tw.B(Tw.Blue400), Visibility = selected ? Visibility.Visible : Visibility.Hidden };
            var content = DK.Between(DK.Plain($"{theme} Theme", 12), check);
            return (UIElement)Choice(content, selected, () =>
            {
                _settings.Appearance.Theme = theme;
                ThemeManager.SetPreference(theme);
                Save();
                Render();
            }, new Thickness(12));
        }).ToList();

        var dpiBox = DK.Surface(DK.Between(DK.Text("Sharp on every monitor and display scaling", 12, "Ink.Text300"),
                DK.Chip("Active", Tw.B(Tw.Emerald500, 0.2), "Ink.SuccessText", Tw.B(Colors.Transparent), 10, 4, new Thickness(8, 2, 8, 2))),
            "Ink.Raised40", "Ink.Divider", 12, new Thickness(12));

        return DK.V(0,
            Section("Theme", DK.Text("Choose dark, light, or match your Windows setting.", 12, "Ink.Text400").Wrap(), DK.Columns(3, 12, themes)),
            Ruled(Section("Display Scaling", null, dpiBox)));
    }

    private UIElement ToolbarTab()
    {
        var presets = Commands.PresetManager.Instance.Presets.Select(p =>
        {
            var content = DK.V(4, DK.Text(p.Name, 12, "Ink.Text", FontWeights.SemiBold), DK.Text(p.Description, 11, "Ink.Text400").Wrap());
            return (UIElement)Choice(content, _settings.Toolbar.Preset == p.Id, () => { _toolbar.SetPreset(p.Id); Render(); }, new Thickness(12));
        }).ToList();

        var orientation = DK.H(12,
            Choice(DK.Plain("Horizontal (Top / Bottom)", 12), _toolbar.IsHorizontal, () => { _toolbar.ApplyOrientation(true); Render(); }, new Thickness(16, 8, 16, 8)),
            Choice(DK.Plain("Vertical (Left / Right)", 12), !_toolbar.IsHorizontal, () => { _toolbar.ApplyOrientation(false); Render(); }, new Thickness(16, 8, 16, 8)));
        orientation.Margin = new Thickness(0, 8, 0, 0);

        var s = _overlay.Settings;
        var pill = DK.Between(DK.V(0, DK.Text("Windows Ink status pill", 12, "Ink.Text200", FontWeights.Medium),
                DK.Text("Show the digitizer / snap status pill while a drawing tool is active", 11, "Ink.Text400")),
            DK.Switch(s.ShowStatusPill, v => _overlay.UpdateOptions(o => o.ShowStatusPill = v), Tw.B(Tw.Blue600)));
        var tooltips = DK.Between(DK.V(0, DK.Text("Toolbar tooltips", 12, "Ink.Text200", FontWeights.Medium),
                DK.Text("Describe each tool and its shortcut on hover", 11, "Ink.Text400")),
            DK.Switch(_settings.Toolbar.ShowTooltips, v => { _settings.Toolbar.ShowTooltips = v; ToolTipService.SetIsEnabled(_toolbar, v); Save(); }, Tw.B(Tw.Blue600)));
        tooltips.Margin = new Thickness(0, 12, 0, 0);

        return DK.V(0,
            Section("Toolbar Preset", DK.Text("Pick the set of toolbar buttons that suits how you present.", 12, "Ink.Text400").Wrap(), DK.Columns(2, 12, presets)),
            Ruled(Section("Toolbar Direction", null, orientation)),
            Ruled(Section("Display", null, pill, tooltips)));
    }

    private UIElement Hotkeys()
    {
        var badge = DK.Surface(DK.H(4, new LucideIcon("Shield", 12) { Foreground = Tw.B(Tw.Red400), VerticalAlignment = VerticalAlignment.Center },
                DK.Text("Panic key: Alt+Shift+X", 10, "Ink.DangerText")),
            "Ink.DangerBg", "Ink.DangerBorder", 999, new Thickness(8, 2, 8, 2));
        badge.VerticalAlignment = VerticalAlignment.Top;
        var header = DK.Between(
            DK.V(2, DK.Text("Keyboard Shortcuts", 14, "Ink.Text", FontWeights.SemiBold),
                DK.Text("These keys work in any app. Click a shortcut, then press the new keys to change it.", 12, "Ink.Text400").Wrap()),
            badge);
        header.Margin = new Thickness(0, 0, 0, 16);

        var table = new Grid();
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void Cell(UIElement element, int row, int col, bool head)
        {
            var cell = new Border { Padding = new Thickness(10), Child = element, BorderThickness = new Thickness(0, 0, 0, 1) };
            cell.SetResourceReference(Border.BackgroundProperty, head ? "Ink.Kbd950" : "Ink.Surface");
            cell.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            table.Children.Add(cell);
        }
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Cell(DK.Text("Action Name", 11, "Ink.Text400", mono: true), 0, 0, true);
        Cell(DK.Text("Assigned Keys", 11, "Ink.Text400", mono: true), 0, 1, true);
        Cell(DK.Text("Status", 11, "Ink.Text400", mono: true), 0, 2, true);
        var row = 1;
        foreach (var binding in _settings.Hotkeys.Bindings)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var isCustom = binding.Action.StartsWith(HotkeyManager.CommandPrefix, StringComparison.Ordinal);
            var commandName = isCustom ? binding.Action[HotkeyManager.CommandPrefix.Length..] : string.Empty;
            var actionName = !isCustom ? binding.Action
                : commandName.StartsWith("tool/", StringComparison.Ordinal) ? $"Select {commandName[5..]} (tablet key)"
                : _toolbar.Registry.Find(commandName)?.Name ?? commandName;
            Cell(DK.Text(actionName, 12, "Ink.Text", FontWeights.Medium), row, 0, false);
            var capturing = _capturingAction == binding.Action;
            var keysText = capturing ? "Press keys…" : binding.DisplayText;
            var kbd = DK.Kbd(keysText, capturing ? Tw.B(Tw.Amber300) : "Ink.KbdText", "Ink.Kbd950", capturing ? Tw.B(Tw.Amber500) : "Ink.BorderStrong", 12, new Thickness(8, 2, 8, 2));
            FrameworkElement keysCell = kbd;
            if (!binding.Protected)
            {
                var rebind = DK.Button(kbd, Tw.B(Colors.Transparent), "Ink.Text", Tw.B(Colors.Transparent), "Ink.Text", 4);
                rebind.ToolTip = "Click, then press the new shortcut";
                rebind.HorizontalAlignment = HorizontalAlignment.Left;
                rebind.Click += (_, _) => { _capturingAction = binding.Action; _hotkeyStatus = string.Empty; Render(); Focus(); };
                keysCell = rebind;
            }
            else kbd.HorizontalAlignment = HorizontalAlignment.Left;
            Cell(keysCell, row, 1, false);
            Cell(binding.Protected
                ? DK.H(4, new LucideIcon("Shield", 12) { Foreground = Tw.B(Tw.Red400) }, DK.Text("Always on (panic key)", 12, Tw.B(Tw.Red400), FontWeights.SemiBold))
                : isCustom ? RemoveButton(binding.Action) : DK.Text("Rebindable", 12, "Ink.Text400"), row, 2, false);
            row++;
        }
        var frame = DK.Surface(table, Tw.B(Colors.Transparent), "Ink.Divider", 12, new Thickness(0));
        frame.ClipToBounds = true;

        // Any feature can get its own shortcut.
        var features = new System.Windows.Controls.ComboBox
        {
            MinWidth = 260,
            ItemsSource = _toolbar.Registry.Commands.OrderBy(c => c.Name).Select(c => new System.Windows.Controls.ComboBoxItem { Content = c.Name, Tag = c.Id }).ToList(),
            ToolTip = "Pick a feature, then press the keys you want for it"
        };
        var add = DK.Button(DK.IconLabel("Plus", 12, "Add shortcut", 12, spacing: 4), Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue500), Tw.B(Colors.White), 8, new Thickness(10, 5, 10, 5));
        add.Click += (_, _) =>
        {
            if (features.SelectedItem is not System.Windows.Controls.ComboBoxItem { Tag: string id }) { _hotkeyStatus = "Pick a feature first."; Render(); return; }
            var action = HotkeyManager.CommandPrefix + id;
            if (_settings.Hotkeys.Bindings.All(b => b.Action != action)) _settings.Hotkeys.Bindings.Add(new HotkeyBinding(action, ModifierKeys.None, Key.None));
            _capturingAction = action;
            _hotkeyStatus = "Now press the keys (for example Ctrl+Shift+W). Esc cancels.";
            Render();
            Focus();
        };
        var addRow = DK.H(8, features, add);
        var streamDeck = DK.Text("Stream Deck, macros and scripts can also run any feature with a link such as inkit://pen, inkit://zoom, inkit://screenshot, inkit://whiteboard, inkit://next-page or inkit://color/red.", 11, "Ink.Text400").Wrap();
        var more = Ruled(Section("Shortcut for any feature", null, addRow, streamDeck));

        var reset = DK.Button(DK.IconLabel("RotateCcw", 12, "Reset to defaults", 11, spacing: 4), Tw.B(Colors.Transparent), Tw.B(Tw.Blue400), Tw.B(Colors.Transparent), Tw.B(Tw.Blue300), 4);
        reset.HorizontalAlignment = HorizontalAlignment.Left;
        reset.Click += (_, _) => { _settings.Hotkeys.Reset(); CommitHotkeys(); };
        var status = DK.Text(_hotkeyStatus, 12, Tw.B(Tw.Amber400)).Wrap();
        var footer = DK.Between(reset, status);
        footer.Margin = new Thickness(0, 12, 0, 0);
        return DK.V(0, header, frame, footer, more, TabletSection());
    }

    /// <summary>Pen tablet: which express key does what, and one click to set up an XP-Pen tablet.</summary>
    private UIElement TabletSection()
    {
        var tablet = Support.XpPenSetup.DetectTablet();
        var intro = DK.Text(tablet is { } t
                ? $"Found your XP-Pen {t.Name} with {t.Keys} express keys. InkIt can put the teaching tools on them, top key first:"
                : "Using a pen tablet? Its express keys can send these shortcuts (works with any tablet app). XP-Pen tablets can be set up with one click:",
            12, "Ink.Text400").Wrap();
        var rows = DK.V(4, Support.XpPenSetup.Layout.Take(tablet?.Keys is > 0 and var k ? k : 8).Select((a, i) =>
        {
            var name = DK.Text($"Key {i + 1}:  {a.Tool}", 12, "Ink.Text", FontWeights.Medium);
            var keys = DK.Kbd(a.Shortcut, "Ink.KbdText", "Ink.Kbd950", "Ink.BorderStrong", 11, new Thickness(6, 1, 6, 1));
            return (UIElement)DK.Between(name, keys);
        }).ToArray());
        rows.Margin = new Thickness(0, 6, 0, 6);
        var status = DK.Text(string.Empty, 12, Tw.B(Tw.Emerald400)).Wrap();
        var setup = DK.Button(DK.IconLabel("Pen", 13, "Set up my XP-Pen keys", 12, spacing: 6), Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue500), Tw.B(Colors.White), 8, new Thickness(12, 6, 12, 6));
        setup.ToolTip = "Writes these keys into the XP-Pen app (a backup is kept) and restarts it for a moment";
        setup.IsEnabled = tablet is { Keys: > 0 };
        setup.Click += (_, _) => { status.Text = Support.XpPenSetup.Apply(); _settings.Hotkeys.AddTabletDefaults(); CommitHotkeys(); };
        var undo = DK.Button(DK.IconLabel("RotateCcw", 13, "Undo XP-Pen setup", 12, spacing: 6), "Ink.Control", "Ink.Text200", "Ink.ControlHover", "Ink.Text", 8, new Thickness(12, 6, 12, 6));
        undo.IsEnabled = Support.XpPenSetup.HasBackup;
        undo.Click += (_, _) => { status.Text = Support.XpPenSetup.Restore(); Render(); };
        var tip = DK.Text("Pen side button: set one button to \"Mouse right click\" in the XP-Pen app - while drawing, it opens InkIt's tool wheel (laser, spotlight, screenshot and more).", 11, "Ink.Text400").Wrap();
        return Ruled(Section("Pen tablet (XP-Pen)", intro, rows, DK.H(8, setup, undo), status, tip));
    }

    private FrameworkElement RemoveButton(string action)
    {
        var b = DK.Button(DK.IconLabel("X", 12, "Remove", 12, spacing: 4), Tw.B(Colors.Transparent), "Ink.Text400", Tw.B(Tw.Red950, 0.4), Tw.B(Tw.Red300), 4, new Thickness(6, 2, 6, 2));
        b.HorizontalAlignment = HorizontalAlignment.Left;
        b.Click += (_, _) => { _settings.Hotkeys.Bindings.RemoveAll(x => x.Action == action); _hotkeyStatus = string.Empty; CommitHotkeys(); };
        return b;
    }

    private void OnCaptureKey(object sender, KeyEventArgs e)
    {
        if (_capturingAction is null) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;
        if (key == Key.Escape)
        {
            // Cancelling a new custom shortcut leaves no empty row behind.
            _settings.Hotkeys.Bindings.RemoveAll(b => b.Action == _capturingAction && b.IsEmpty && b.Action.StartsWith(HotkeyManager.CommandPrefix, StringComparison.Ordinal));
            _capturingAction = null;
            Render();
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var original = _settings.Hotkeys.Bindings.First(b => b.Action == _capturingAction);
        var candidate = original with { Key = key, Modifiers = Keyboard.Modifiers };
        if (!_settings.Hotkeys.TrySet(candidate, out var conflict))
        {
            _hotkeyStatus = conflict!.Message;
            Render();
            return;
        }
        _capturingAction = null;
        _hotkeyStatus = $"{candidate.Action} → {candidate.DisplayText}";
        CommitHotkeys();
    }

    private void CommitHotkeys()
    {
        Save();
        _hotkeysChanged?.Invoke(_settings.Hotkeys);
        Render();
    }

    private UIElement About()
    {
        Button Action(string icon, string label, Action run, bool primary = false)
        {
            var b = primary
                ? DK.Button(DK.IconLabel(icon, 14, label, 12, spacing: 6), Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue500), Tw.B(Colors.White), 8, new Thickness(12, 7, 12, 7))
                : DK.Button(DK.IconLabel(icon, 14, label, 12, spacing: 6), "Ink.Control", "Ink.Text200", "Ink.ControlHover", "Ink.Text", 8, new Thickness(12, 7, 12, 7));
            b.Click += (_, _) => run();
            return b;
        }
        FrameworkElement SwitchRow(string title, string detail, bool value, Action<bool> changed)
        {
            var text = DK.V(0, DK.Text(title, 13, "Ink.Text", FontWeights.Medium), DK.Text(detail, 11, "Ink.Text400").Wrap());
            text.Margin = new Thickness(0, 0, 16, 0);
            var toggle = DK.Switch(value, changed, Tw.Blue500);
            toggle.VerticalAlignment = VerticalAlignment.Center;
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, Margin = new Thickness(0, 6, 0, 6) };
            row.Children.Add(text);
            Grid.SetColumn(toggle, 1);
            row.Children.Add(toggle);
            return row;
        }

        var version = DK.V(2, DK.Text($"InkIt {AppInfo.Version}", 20, "Ink.Text", FontWeights.SemiBold),
            DK.Text("Draw, highlight, zoom and screenshot on your screen.", 12, "Ink.Text400"));
        var updates = DK.H(8,
            Action("RotateCw", "Check for updates", () => _toolbar.CheckForUpdates(manual: true), primary: true),
            Action("Info", "What's new", () => Support.Links.Open(Support.Links.Releases)));
        updates.Margin = new Thickness(0, 12, 0, 0);
        var autoUpdate = SwitchRow("Check for updates automatically",
            "Once a day InkIt asks GitHub whether a newer version exists. Nothing else is sent.",
            _settings.Advanced.CheckForUpdates, v => { _settings.Advanced.CheckForUpdates = v; Save(); });
        var startup = SwitchRow("Start InkIt when I sign in to Windows",
            "InkIt waits in the tray, ready for Ctrl+Shift+5 and the other shortcuts.",
            Support.StartWithWindows.IsEnabled,
            v => { if (!Support.StartWithWindows.TrySet(v)) Toast.Show("Windows did not allow that change"); });
        var feedback = DK.H(8,
            Action("Star", "Send feedback", _toolbar.SendFeedback, primary: true),
            Action("AlertCircle", "Report a problem", _toolbar.ReportProblem),
            Action("Folder", "Crash reports", Support.CrashLog.OpenFolder));
        var tour = Action("Compass", "Show the quick tour", () => { Close(); _toolbar.StartTour(); });
        tour.HorizontalAlignment = HorizontalAlignment.Left;
        return DK.V(0,
            Section("InkIt", null, version, updates),
            Ruled(Section("Start-up", null, autoUpdate, startup)),
            Ruled(Section("Feedback", DK.Text("Tell us what works and what to improve. The forms open in your browser with your version filled in.", 12, "Ink.Text400").Wrap(), feedback)),
            Ruled(Section("Help", null, tour)));
    }

    private UIElement Presentation()
    {
        var s = _overlay.Settings;
        var radius = DK.SliderBlock("Default Lens Radius", 60, 350, 1, Math.Clamp(s.SpotlightRadius, 60, 350), v => $"{v:0}px", Tw.B(Tw.Amber400),
            v => _overlay.UpdateOptions(o => o.SpotlightRadius = v));
        ((Slider)radius.Children[1]).Foreground = Tw.B(Tw.Amber500);
        var slit = DK.SliderBlock("Slit Viewport Height", 40, 300, 1, Math.Clamp(s.CodeFocusBandHeight, 40, 300), v => $"{v:0}px", Tw.B(Tw.Blue400),
            v => _overlay.UpdateOptions(o => o.CodeFocusBandHeight = v));
        ((Slider)slit.Children[1]).Foreground = Tw.B(Tw.Blue500);
        radius.Margin = slit.Margin = new Thickness(0, 8, 0, 0);
        var slidesText = DK.V(0, DK.Text("Keep drawings with each PowerPoint slide", 13, "Ink.Text", FontWeights.Medium),
            DK.Text("During a slide show, moving to the next slide hides your drawings; going back shows them again.", 11, "Ink.Text400").Wrap());
        slidesText.Margin = new Thickness(0, 0, 16, 0);
        var slidesSwitch = DK.Switch(_toolbar.FollowSlides, v => _toolbar.FollowSlides = v, Tw.Blue500);
        slidesSwitch.VerticalAlignment = VerticalAlignment.Center;
        var slides = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        slides.Children.Add(slidesText);
        Grid.SetColumn(slidesSwitch, 1);
        slides.Children.Add(slidesSwitch);
        return DK.V(0, Section("PowerPoint", null, slides), Ruled(Section("Spotlight Size", null, radius)), Ruled(Section("Focus Band", null, slit)));
    }

    private UIElement Profiles()
    {
        FrameworkElement Card(string title, System.Windows.Media.Color accent, string key, string fallbackColor, double fallbackWidth, byte fallbackOpacity, bool fallbackPressure)
        {
            var profile = _settings.ToolProfiles.TryGetValue(key, out var p) ? p : null;
            var color = profile?.Color ?? fallbackColor;
            var hex = color.Length == 9 ? "#" + color[3..] : color;
            var name = InspectorWindow.TeachingColors.FirstOrDefault(c => string.Equals(c.Hex, hex, StringComparison.OrdinalIgnoreCase)).Name ?? "Custom";
            var opacity = profile?.Opacity ?? fallbackOpacity;
            var lines = DK.V(2,
                DK.Text($"Color: {hex.ToUpperInvariant()} ({name})", 11, "Ink.Text300", mono: true),
                DK.Text($"Thickness: {profile?.StrokeWidth ?? fallbackWidth:0.0} px", 11, "Ink.Text300", mono: true),
                DK.Text($"Opacity: {Math.Round(opacity / 255d * 100)}%" + (opacity < 255 ? $" ({opacity} alpha)" : string.Empty), 11, "Ink.Text300", mono: true),
                DK.Text($"Pressure: {((profile?.PressureEnabled ?? fallbackPressure) ? "Enabled" : "Disabled")}", 11, "Ink.Text300", mono: true));
            var titleText = DK.Text(title, 12, Tw.B(accent), FontWeights.SemiBold);
            titleText.Margin = new Thickness(0, 0, 0, 4);
            return DK.Surface(DK.V(0, titleText, lines), "Ink.Raised40", "Ink.Divider", 12, new Thickness(12));
        }
        return Section("Tool Memory",
            DK.Text("Each tool remembers its own colour and thickness, so switching tools never loses your settings.", 12, "Ink.Text400").Wrap(),
            DK.Columns(2, 12, new UIElement[]
            {
                Card("Pen Profile", Tw.Blue400, "Pen_Ballpoint", "#FF2563EB", 4, 255, true),
                Card("Highlighter Profile", Tw.Amber400, "Highlighter_Highlighter", "#FFF2B705", 18, 115, false),
                Card("Arrow Profile", Tw.Rose400, "Shape_Arrow", "#FFE5484D", 6, 255, false),
                Card("Laser Profile", Tw.Red400, "Laser", "#FFE5484D", 8, 255, false)
            }));
    }

    private UIElement Audit()
    {
        _cpuText = DK.Text("Measuring…", 12, Tw.B(Tw.Amber400), FontWeights.SemiBold);
        var optimized = _settings.Advanced.IdleCpuOptimized;
        var toggle = optimized
            ? DK.Button(DK.Plain("Power saving (recommended)", 12, FontWeights.Medium), Tw.B(Tw.Emerald600), Tw.B(Colors.White), Tw.B(Tw.Emerald500), Tw.B(Colors.White), 4, new Thickness(10, 4, 10, 4))
            : DK.Button(DK.Plain("Faster monitor detection", 12, FontWeights.Medium), "Ink.Control", "Ink.Text400", "Ink.ControlHover", "Ink.Text200", 4, new Thickness(10, 4, 10, 4));
        toggle.Click += (_, _) =>
        {
            _settings.Advanced.IdleCpuOptimized = !_settings.Advanced.IdleCpuOptimized;
            Save();
            Toast.Show("Saved - takes effect next time InkIt starts");
            Render();
        };
        var cpuCard = DK.Surface(DK.V(6,
                DK.H(8, new LucideIcon("AlertCircle", 16) { Foreground = Tw.B(Tw.Amber400) }, _cpuText),
                DK.Text("InkIt uses almost no processor time while you are not drawing, so it will not slow your presentation or video call.", 11, "Ink.Text400").Wrap(),
                DK.Between(DK.Text("Background activity:", 12, "Ink.Text400"), toggle)),
            Tw.B(Tw.Amber950, 0.3), Tw.B(Tw.Amber800, 0.6), 12, new Thickness(14));
        var nuget = DK.Surface(DK.V(4, DK.Text("Built into Windows", 12, "Ink.Text", FontWeights.Medium),
                DK.Text("InkIt needs no extra downloads or add-ins and works offline on Windows 10 and 11.", 11, "Ink.Text400").Wrap()),
            "Ink.Raised40", "Ink.Divider", 12, new Thickness(12));
        nuget.Margin = new Thickness(0, 16, 0, 0);
        using (var process = Process.GetCurrentProcess()) { _lastCpu = process.TotalProcessorTime; }
        _lastSample = DateTime.UtcNow;
        _cpuTimer.Start();
        var title = DK.Text("Performance", 14, "Ink.Text", FontWeights.SemiBold);
        title.Margin = new Thickness(0, 0, 0, 16);
        return DK.V(0, title, cpuCard, nuget);
    }

    private void SampleCpu()
    {
        if (_cpuText is null) return;
        using var process = Process.GetCurrentProcess();
        var now = DateTime.UtcNow;
        var cpu = process.TotalProcessorTime;
        var percent = (cpu - _lastCpu).TotalMilliseconds / ((now - _lastSample).TotalMilliseconds * Environment.ProcessorCount) * 100;
        _lastCpu = cpu;
        _lastSample = now;
        _cpuText.Text = $"Live InkIt CPU: {percent:0.00}% · Working set {process.WorkingSet64 / 1048576d:0} MB";
        _cpuText.Foreground = Tw.B(percent < 1 ? Tw.Emerald400 : Tw.Amber400);
    }
}
