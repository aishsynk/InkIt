using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenCanvas.Capture;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Colors = System.Windows.Media.Colors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ScreenCanvas.UI;

/// <summary>Capture Preview - design: CapturePreviewModal.tsx, showing the real captured bitmap.</summary>
public sealed class CapturePreviewWindow : ModalHost
{
    private readonly Bitmap _bitmap;
    private readonly ICaptureService _capture;

    public CapturePreviewWindow(Bitmap bitmap, ICaptureService capture) : base(closeOnBackdropClick: false, backdropKey: "Ink.Backdrop")
    {
        _bitmap = bitmap;
        _capture = capture;
        Title = "Capture Preview";

        var close = DK.IconButton("X", 16, "Ink.Text400", "Ink.Text", "Ink.Hover", 4, 4);
        close.Click += (_, _) => Close();
        var title = DK.H(8,
            new LucideIcon("Crop", 16) { Foreground = Tw.B(Tw.Blue400) },
            DK.Text("Capture Preview", 12, "Ink.Text", FontWeights.SemiBold),
            DK.Chip($"{bitmap.Width} × {bitmap.Height} px", "Ink.Control", "Ink.Text400", Tw.B(Colors.Transparent), 10, 4, new Thickness(6, 2, 6, 2)));
        var header = new Border { Padding = new Thickness(20, 12, 20, 12), BorderThickness = new Thickness(0, 0, 0, 1), Child = DK.Between(title, close) };
        header.SetResourceReference(Border.BackgroundProperty, "Ink.Sunken");
        header.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");

        var image = new Image { Source = ToBitmapSource(bitmap), Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(8) };
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(image, System.Windows.Media.BitmapScalingMode.HighQuality);
        var badge = DK.Surface(DK.H(4, DK.Dot(6, Tw.B(Tw.Emerald400)), DK.Text("GDI BitBlt Complete", 10, Tw.B(Tw.Emerald400), mono: true)),
            Tw.B(Tw.Slate950, 0.8), Tw.B(Tw.Slate700), 4, new Thickness(8, 2, 8, 2));
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        badge.VerticalAlignment = VerticalAlignment.Top;
        badge.Margin = new Thickness(8);
        var frame = DK.Surface(new Grid { Children = { image, badge } }, Tw.B(Tw.Slate900), "Ink.Divider", 12, new Thickness(0));
        frame.Height = 256;
        frame.ClipToBounds = true;
        var preview = new Border { Padding = new Thickness(24), Child = frame };
        preview.SetResourceReference(Border.BackgroundProperty, "Ink.Kbd950");

        var discard = DK.Button(DK.IconLabel("Trash2", 14, "Discard (Esc)", 12), Tw.B(Colors.Transparent), "Ink.Text400", Tw.B(Tw.Red950, 0.4), Tw.B(Tw.Red300), 8, new Thickness(12, 6, 12, 6));
        discard.Click += (_, _) => Close();
        var saveJpeg = DK.Button(DK.IconLabel("Download", 14, "Save JPEG", 12, spacing: 4), "Ink.Control", "Ink.Text200", "Ink.ControlHover", "Ink.Text200", 12, new Thickness(12, 6, 12, 6));
        saveJpeg.Click += (_, _) => Save("JPEG", ".jpg", CaptureImageFormat.Jpeg);
        var savePng = DK.Button(DK.IconLabel("Download", 14, "Save PNG", 12, spacing: 4), "Ink.Control", "Ink.Text200", "Ink.ControlHover", "Ink.Text200", 12, new Thickness(12, 6, 12, 6));
        savePng.Click += (_, _) => Save("PNG", ".png", CaptureImageFormat.Png);
        var copyIcon = new LucideIcon("Copy", 14);
        var copyText = DK.Plain("Copy to Clipboard", 12, FontWeights.Medium);
        var copy = DK.Button(DK.H(6, copyIcon, copyText), Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue500), Tw.B(Colors.White), 12, new Thickness(16, 6, 16, 6));
        copy.Effect = DK.Shadow(8, 2, 0.35);
        copy.Click += (_, _) =>
        {
            _capture.CopyToClipboard(_bitmap);
            copyIcon.Kind = "Check";
            copyText.Text = "Copied to Clipboard!";
            Toast.Show("Screenshot copied to clipboard!");
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            timer.Tick += (_, _) => { timer.Stop(); Close(); };
            timer.Start();
        };
        var footer = Footer(discard, DK.H(8, saveJpeg, savePng, copy), new Thickness(20, 12, 20, 12));

        AddCard(DK.V(0, header, preview, footer), 672);
    }

    private void Save(string name, string extension, CaptureImageFormat format)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Screenshot",
            Filter = $"{name} image|*{extension}",
            DefaultExt = extension,
            FileName = $"InkIt_Capture_{DateTime.Now:yyyyMMdd_HHmmss}{extension}"
        };
        if (dialog.ShowDialog(this) != true) return;
        _capture.Save(_bitmap, dialog.FileName, format);
        Toast.Show($"Saved {System.IO.Path.GetFileName(dialog.FileName)}");
        Close();
    }

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(handle, nint.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);
}
