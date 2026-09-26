using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenCanvas.Capture;
using ScreenCanvas.Core;
using ScreenCanvas.Hotkeys;
using ScreenCanvas.Overlay;
using ScreenCanvas.Settings;
using ScreenCanvas.UI.Toolbar;
using Path = System.Windows.Shapes.Path;
using Point = System.Windows.Point;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

// InkIt checks that run without a screen or user input. Exit code 0 = all passed.
var failures = 0;
var passed = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) { passed++; Console.WriteLine($"  PASS  {name}"); }
    else { failures++; Console.WriteLine($"  FAIL  {name} {detail}"); }
}

var thread = new Thread(() =>
{
    Console.WriteLine("Smart shapes");
    SmartShapeTests.Run(Check);
    Console.WriteLine("Files");
    FileTests.Run(Check);
    Console.WriteLine("Settings and toolbar");
    SettingsTests.Run(Check);
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

Console.WriteLine("PDF (Windows reader)");
try
{
    var pdf = Path2.Temp("inkit-tests.pdf");
    var sf = await Windows.Storage.StorageFile.GetFileFromPathAsync(pdf);
    var doc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(sf);
    Check("PDF opens with 2 pages", doc.PageCount == 2, $"(pages {doc.PageCount})");
    File.Delete(pdf);
}
catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or PlatformNotSupportedException or TypeLoadException)
{
    Console.WriteLine("  SKIP  Windows PDF reader not available here");
}

Console.WriteLine($"{passed} passed, {failures} failed");
return failures == 0 ? 0 : 1;

static class Path2
{
    public static string Temp(string name) => System.IO.Path.Combine(System.IO.Path.GetTempPath(), name);
}

static class SmartShapeTests
{
    private static readonly Random Rnd = new(7);
    private static Point J(double x, double y, double n) => new(x + (Rnd.NextDouble() - .5) * n, y + (Rnd.NextDouble() - .5) * n);

    private static List<Point> Loop(Func<double, Point> f, double n, double overshoot = 0.03)
    {
        var l = new List<Point>();
        for (var i = 0; i <= 90; i++) { var p = f(i / 90.0 * (1 + overshoot)); l.Add(J(p.X, p.Y, n)); }
        return l;
    }

    private static List<Point> Poly(Point[] v, double n, bool close, int per = 25)
    {
        var l = new List<Point>();
        var k = close ? v.Length : v.Length - 1;
        for (var s = 0; s < k; s++)
        {
            var a = v[s];
            var b = v[(s + 1) % v.Length];
            for (var i = 0; i < per; i++)
            {
                var t = i / (double)per;
                var bow = Math.Sin(t * Math.PI) * (Rnd.NextDouble() - .5) * n * 1.5;
                var d = b - a;
                var normal = new Vector(-d.Y, d.X);
                normal.Normalize();
                l.Add(J(a.X + d.X * t + normal.X * bow, a.Y + d.Y * t + normal.Y * bow, n));
            }
        }
        var last = close ? v[0] : v[^1];
        l.Add(J(last.X, last.Y, n));
        return l;
    }

    private static Point P(double x, double y) => new(x, y);

    public static void Run(Action<string, bool, string> check)
    {
        var cases = new (string Name, string Expect, Func<List<Point>> Make)[]
        {
            ("circle", "Ellipse", () => Loop(t => P(400 + 120 * Math.Cos(t * 6.283), 300 + 118 * Math.Sin(t * 6.283)), 6)),
            ("wide ellipse", "Ellipse", () => Loop(t => P(400 + 200 * Math.Cos(t * 6.283), 300 + 90 * Math.Sin(t * 6.283)), 7)),
            ("box", "Rectangle", () => Poly([P(100, 100), P(400, 105), P(398, 300), P(102, 296)], 6, true)),
            ("square", "Rectangle", () => Poly([P(100, 100), P(250, 98), P(252, 250), P(99, 248)], 6, true)),
            ("rotated box", "Rectangle", () => Poly([P(200, 100), P(400, 200), P(330, 340), P(130, 240)], 6, true)),
            ("triangle", "Triangle", () => Poly([P(300, 100), P(450, 350), P(150, 350)], 6, true)),
            ("diamond", "Diamond", () => Poly([P(300, 100), P(450, 250), P(300, 400), P(150, 250)], 6, true)),
            ("line", "Line", () => Poly([P(100, 300), P(500, 305)], 4, false, 40)),
            ("hooked arrow", "Arrow", () => Poly([P(100, 300), P(450, 300), P(420, 280), P(450, 300), P(420, 322)], 3, false, 20)),
            ("one-sided arrow", "Arrow", () => Poly([P(100, 100), P(400, 250), P(365, 250)], 3, false, 20)),
            ("letter o stays", "none", () => Loop(t => P(400 + 14 * Math.Cos(t * 6.283), 300 + 16 * Math.Sin(t * 6.283)), 2)),
            ("zigzag stays", "none", () => Poly([P(100, 300), P(160, 250), P(220, 320), P(290, 240), P(360, 330)], 5, false)),
            ("wavy underline stays", "none", () => Loop(t => P(300 + t * 300, 300 + 40 * Math.Sin(t * 25)), 3, 0)),
            ("tick stays", "none", () => Poly([P(100, 300), P(120, 320), P(160, 260)], 2, false, 10)),
        };
        foreach (var (name, expect, make) in cases)
        {
            var hits = 0;
            for (var run = 0; run < 20; run++)
                if ((ShapeRecognizer.Recognize(make(), 4)?.Kind.ToString() ?? "none") == expect) hits++;
            check($"{name}: {expect} ({hits}/20)", hits >= 18, "");
        }
        var chevrons = 0;
        for (var run = 0; run < 20; run++) if (ShapeRecognizer.RecognizeChevron(Poly([P(420, 270), P(460, 300), P(420, 330)], 3, false, 15)) is not null) chevrons++;
        check($"arrowhead V recognised ({chevrons}/20)", chevrons >= 19, "");
        check("straight line is not a V", ShapeRecognizer.RecognizeChevron(Poly([P(100, 300), P(500, 300)], 3, false, 30)) is null, "");
        var snapped = ShapeRecognizer.Recognize([P(100, 300), P(200, 302), P(300, 304), P(400, 306), P(500, 309), P(600, 311), P(700, 313), P(800, 315)], 4);
        check("nearly level line snaps to exactly level", snapped?.Geometry is LineGeometry l && Math.Abs(l.StartPoint.Y - l.EndPoint.Y) < 0.001, "");
    }
}

static class FileTests
{
    public static void Run(Action<string, bool, string> check)
    {
        var page = new InkPage();
        page.Strokes.Add(new Stroke(new StylusPointCollection(new[] { new StylusPoint(10, 10), new StylusPoint(200, 120) })));
        page.Strokes.Add(new StyledStroke(new StylusPointCollection(new[] { new StylusPoint(30, 50), new StylusPoint(300, 60) }),
            new DrawingAttributes { Color = Colors.Red, Width = 6, Height = 6 }, PenMode.Calligraphy));
        page.Shapes.Add(new Path { Data = new EllipseGeometry(new Rect(50, 50, 120, 80)), Stroke = Brushes.Blue, StrokeThickness = 4 });
        var note = new Border { Background = Brushes.LightYellow, Child = new TextBlock { Text = "Remember this" } };
        Canvas.SetLeft(note, 100);
        page.Shapes.Add(note);
        var file = Path2.Temp("inkit-tests.inkit");
        InkFile.Save(file, [page, new InkPage()]);
        var loaded = InkFile.Load(file);
        File.Delete(file);
        check(".inkit keeps 2 pages", loaded.Count == 2, "");
        check(".inkit keeps both strokes", loaded[0].Strokes.Count == 2, "");
        check(".inkit keeps the calligraphy pen", loaded[0].Strokes.OfType<StyledStroke>().FirstOrDefault()?.Mode == PenMode.Calligraphy, "");
        check(".inkit keeps shapes and note text", loaded[0].Shapes.Count == 2 && ((TextBlock)((Border)loaded[0].Shapes[1]).Child).Text == "Remember this", "");
        check(".inkit keeps positions", Canvas.GetLeft(loaded[0].Shapes[1]) == 100, "");

        var images = new List<BitmapSource>();
        foreach (var color in new[] { Colors.White, Colors.DarkSlateGray })
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 800, 450));
            var bitmap = new RenderTargetBitmap(800, 450, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            images.Add(bitmap);
        }
        var pdf = Path2.Temp("inkit-tests.pdf");
        PdfWriter.Write(pdf, images);
        var text = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(pdf));
        check("PDF has header, 2 pages and end marker", text.StartsWith("%PDF-1.4") && text.Split("/Type /Page ").Length - 1 == 2 && text.TrimEnd().EndsWith("%%EOF"), "");
    }
}

static class SettingsTests
{
    public static void Run(Action<string, bool, string> check)
    {
        // An old settings file: screenshot hidden, custom order, smart shapes off.
        var old = new AppSettings { DesignVersion = 2 };
        old.Canvas.AutoShapeAssist = false;
        old.Toolbar.Items = [new() { Id = "zoom", Visible = true }, new() { Id = "pen", Visible = true }, new() { Id = "cursor", Visible = true }, new() { Id = "capture", Visible = false }, new() { Id = "eraser", Visible = false }];
        old.MigrateToDesign();
        var ids = old.Toolbar.Items!.Select(i => i.Id).ToList();
        check("upgrade: pointer first, pen second", ids.IndexOf("cursor") == 0 && ids.IndexOf("pen") == 1, string.Join(",", ids.Take(4)));
        check("upgrade: Screenshot turned on", old.Toolbar.Items!.First(i => i.Id == "capture").Visible, "");
        check("upgrade: hidden buttons stay hidden", !old.Toolbar.Items!.First(i => i.Id == "eraser").Visible, "");
        check("upgrade: smart shapes on", old.Canvas.AutoShapeAssist, "");
        check("upgrade: reaches current version", old.DesignVersion == AppSettings.CurrentDesignVersion, "");

        foreach (var preset in ToolbarCatalog.Presets)
        {
            var order = ToolbarCatalog.Items.Select(i => i.Id).ToList();
            var positions = preset.ToolIds.Select(id => order.IndexOf(id)).ToList();
            check($"preset '{preset.Name}' keeps the canonical order", positions.SequenceEqual(positions.OrderBy(p => p)) && positions.All(p => p >= 0), "");
        }
        check("every toolbar item has a group", ToolbarCatalog.Items.All(i => !string.IsNullOrEmpty(i.Group)), "");

        check("shortcut text: Ctrl+Shift+2", new HotkeyBinding("x", ModifierKeys.Control | ModifierKeys.Shift, Key.D2).DisplayText == "Ctrl+Shift+2", "");
        check("shortcut text: Ctrl+Shift+Del", new HotkeyBinding("x", ModifierKeys.Control | ModifierKeys.Shift, Key.Delete).DisplayText == "Ctrl+Shift+Del", "");
    }
}
