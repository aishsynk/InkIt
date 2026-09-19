using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenCanvas.Commands;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Cursors = System.Windows.Input.Cursors;

namespace ScreenCanvas.UI;

public partial class RadialMenuWindow : Window
{
    private readonly CommandRegistry _registry;

    private readonly (string Id, string Name, string IconKey, double Angle)[] _slots =
    [
        ("cursor", "Cursor (Esc)", "Fluent.Cursor.Regular", -90),
        ("pen", "Pen (P)", "Fluent.Pen.Regular", -45),
        ("highlighter", "Highlighter (H)", "Fluent.Highlight.Regular", 0),
        ("eraser", "Eraser (E)", "Fluent.EraserTool.Regular", 45),
        ("arrow", "Arrow (A)", "Fluent.ArrowRight.Regular", 90),
        ("rectangle", "Rectangle (R)", "Fluent.Rectangle.Regular", 135),
        ("present.spotlight", "Spotlight (M)", "Fluent.Circle.Regular", 180),
        ("zoom", "Zoom (Z)", "Fluent.ZoomIn.Regular", 225)
    ];

    public RadialMenuWindow(CommandRegistry registry)
    {
        InitializeComponent();
        _registry = registry;

        PositionAtCursor();
        BuildSectors();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
        Deactivated += (_, _) => Close();
    }

    private void PositionAtCursor()
    {
        var pos = System.Windows.Forms.Cursor.Position;
        Left = pos.X - (Width / 2);
        Top = pos.Y - (Height / 2);
    }

    private void BuildSectors()
    {
        const double center = 120; // Half of 240
        const double radius = 72;  // Distance from center to button center
        const double buttonSize = 40;

        foreach (var slot in _slots)
        {
            var rad = slot.Angle * Math.PI / 180.0;
            var x = center + (radius * Math.Cos(rad)) - (buttonSize / 2);
            var y = center + (radius * Math.Sin(rad)) - (buttonSize / 2);

            var cmd = _registry.Find(slot.Id);
            var btn = new Button
            {
                Width = buttonSize,
                Height = buttonSize,
                ToolTip = slot.Name,
                Cursor = Cursors.Hand,
                Background = (Brush)FindResource("Level2Brush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Style = (Style)FindResource("RadialSectorButton")
            };

            var iconGeom = TryFindResource(slot.IconKey) as Geometry ?? (Geometry)FindResource("Fluent.Cursor.Regular");
            var iconPath = new Path
            {
                Data = iconGeom,
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform,
                Fill = (Brush)FindResource("PrimaryTextBrush")
            };
            btn.Content = iconPath;

            btn.Click += (_, _) =>
            {
                Close();
                cmd?.Execute();
            };

            Canvas.SetLeft(btn, x);
            Canvas.SetTop(btn, y);
            SectorsCanvas.Children.Add(btn);
        }
    }

    private void CenterButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
