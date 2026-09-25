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
        Title = "Screenshot";

        var close = DK.IconButton("X", 16, "Ink.Text400", "Ink.Text", "Ink.Hover", 4, 4);
        close.Click += (_, _) => Close();
        var title = DK.H(8,
            new LucideIcon("Crop", 16) { Foreground = Tw.B(Tw.Blue400) },
            DK.Text("Screenshot", 12, "Ink.Text", FontWeights.SemiBold),
            DK.Chip($"{bitmap.Width} × {bitmap.Height} px", "Ink.Control", "Ink.Text400", Tw.B(Colors.Transparent), 10, 4, new Thickness(6, 2, 6, 2)));
        var header = new Border { Padding = new Thickness(20, 12, 20, 12), BorderThickness = new Thickness(0, 0, 0, 1), Child = DK.Between(title, close) };
        header.SetResourceReference(Border.BackgroundProperty, "Ink.Sunken");
        header.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");

        var image = new Image { Source = ToBitmapSource(bitmap), Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(8) };
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(image, System.Windows.Media.BitmapScalingMode.HighQuality);
        // Copy at once so the screenshot can be pasted (Teams, PowerPoint, email) without any extra click.
        var copied = _capture.CopyToClipboard(_bitmap);
        var badgeText = DK.Text(copied ? "Copied - paste anywhere with Ctrl+V" : "Could not copy yet - click Copy", 10,
            Tw.B(copied ? Tw.Emerald400 : Tw.Amber400));
        var badgeDot = DK.Dot(6, Tw.B(copied ? Tw.Emerald400 : Tw.Amber400));
        var badge = DK.Surface(DK.H(4, badgeDot, badgeText), Tw.B(Tw.Slate950, 0.8), Tw.B(Tw.Slate700), 4, new Thickness(8, 2, 8, 2));
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        badge.VerticalAlignment = VerticalAlignment.Top;
        badge.Margin = new Thickness(8);
        var frame = DK.Surface(new Grid { Children = { image, badge } }, Tw.B(Tw.Slate900), "Ink.Divider", 12, new Thickness(0));
        frame.Height = 256;
        frame.ClipToBounds = true;
        var preview = new Border { Padding = new Thickness(24), Child = frame };
        preview.SetResourceReference(Border.BackgroundProperty, "Ink.Kbd950");

        var save = DK.Button(DK.IconLabel("Download", 14, "Save as file...", 12, spacing: 4), "Ink.Control", "Ink.Text200", "Ink.ControlHover", "Ink.Text200", 12, new Thickness(12, 6, 12, 6));
        save.ToolTip = "Save as a PNG or JPG picture (Pictures folder by default)";
        save.Click += (_, _) => Save();
        var closeButton = DK.Button(DK.Plain("Close", 12, FontWeights.Medium), "Ink.Control", "Ink.Text200", "Ink.ControlHover", "Ink.Text200", 12, new Thickness(12, 6, 12, 6));
        closeButton.ToolTip = "Close (Esc) - a copied screenshot stays on the clipboard";
        closeButton.Click += (_, _) => Close();
        var copyIcon = new LucideIcon(copied ? "Check" : "Copy", 14);
        var copyText = DK.Plain(copied ? "Copied" : "Copy", 12, FontWeights.SemiBold);
        var copy = DK.Button(DK.H(6, copyIcon, copyText), Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue500), Tw.B(Colors.White), 12, new Thickness(16, 6, 16, 6));
        copy.Effect = DK.Shadow(8, 2, 0.35);
        copy.ToolTip = "Copy the screenshot so you can paste it anywhere with Ctrl+V, then close";
        copy.Click += (_, _) =>
        {
            if (_capture.CopyToClipboard(_bitmap))
            {
                copyIcon.Kind = "Check";
                copyText.Text = "Copied";
                Toast.Show("Screenshot copied - paste it anywhere with Ctrl+V");
                Close();
            }
            else
            {
                copyText.Text = "Try again";
                Toast.Show("Another app is using the clipboard - please try Copy again");
            }
        };
        var hint = DK.Text("Paste with Ctrl+V in Teams, PowerPoint, email...", 11, "Ink.Text400");
        hint.VerticalAlignment = VerticalAlignment.Center;
        var footer = Footer(hint, DK.H(8, save, closeButton, copy), new Thickness(20, 12, 20, 12));
        AddCard(DK.V(0, header, preview, footer), 672);
    }

    private void Save()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Screenshot",
            Filter = "PNG picture (best quality)|*.png|JPG picture (smaller file)|*.jpg",
            DefaultExt = ".png",
            InitialDirectory = pictures,
            FileName = $"Screenshot {DateTime.Now:yyyy-MM-dd HH.mm.ss}"
        };
        if (dialog.ShowDialog(this) != true) return;
        var jpeg = dialog.FilterIndex == 2 || dialog.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || dialog.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
        _capture.Save(_bitmap, dialog.FileName, jpeg ? CaptureImageFormat.Jpeg : CaptureImageFormat.Png);
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
