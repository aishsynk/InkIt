using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenCanvas.Capture;

public sealed partial class CapturePreviewWindow : Window
{
    private readonly Bitmap _bitmap;
    private readonly ICaptureService _capture;

    public CapturePreviewWindow(Bitmap bitmap, ICaptureService capture)
    {
        InitializeComponent();
        _bitmap = bitmap;
        _capture = capture;

        PreviewImage.Source = ToBitmapSource(bitmap);
        DimensionsText.Text = $"{bitmap.Width} x {bitmap.Height}";
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        _capture.CopyToClipboard(_bitmap);
        Close();
    }

    private void SavePng_Click(object sender, RoutedEventArgs e)
    {
        if (ShowSaveDialog("PNG", ".png") is { } path)
            _capture.Save(_bitmap, path, CaptureImageFormat.Png);
    }

    private void SaveJpeg_Click(object sender, RoutedEventArgs e)
    {
        if (ShowSaveDialog("JPEG", ".jpg") is { } path)
            _capture.Save(_bitmap, path, CaptureImageFormat.Jpeg);
    }

    private static string? ShowSaveDialog(string filterName, string extension)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Screenshot",
            Filter = $"{filterName} image|*{extension}",
            DefaultExt = extension,
            FileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}{extension}"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                handle, nint.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);
}
