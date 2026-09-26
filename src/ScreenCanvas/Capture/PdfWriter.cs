using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace ScreenCanvas.Capture;

/// <summary>
/// Writes pictures to a PDF, one per page, sized to the picture (72 points per inch at 96 DPI).
/// Pages are JPEG-compressed; no external libraries are needed.
/// </summary>
public static class PdfWriter
{
    public static void Write(string path, IReadOnlyList<BitmapSource> pages, int jpegQuality = 88)
    {
        if (pages.Count == 0) throw new ArgumentException("Nothing to export.", nameof(pages));
        using var file = File.Create(path);
        var offsets = new List<long>();
        void Text(string s) { var b = Encoding.Latin1.GetBytes(s); file.Write(b); }
        void BeginObject(int id) { offsets.Add(file.Position); Text($"{id} 0 obj\n"); }

        Text("%PDF-1.4\n%âãÏÓ\n");
        // Object ids: 1 catalog, 2 page tree, then per page: page, content, image.
        var pageIds = Enumerable.Range(0, pages.Count).Select(i => 3 + i * 3).ToArray();
        BeginObject(1); Text("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        BeginObject(2); Text($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pages.Count} >>\nendobj\n");

        for (var i = 0; i < pages.Count; i++)
        {
            var bitmap = pages[i];
            var jpeg = Jpeg(bitmap, jpegQuality);
            var widthPt = bitmap.PixelWidth * 72.0 / bitmap.DpiX;
            var heightPt = bitmap.PixelHeight * 72.0 / bitmap.DpiY;
            var w = widthPt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var h = heightPt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            int pageId = pageIds[i], contentId = pageId + 1, imageId = pageId + 2;

            BeginObject(pageId);
            Text($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] /Resources << /XObject << /Im0 {imageId} 0 R >> >> /Contents {contentId} 0 R >>\nendobj\n");
            var content = $"q {w} 0 0 {h} 0 0 cm /Im0 Do Q\n";
            BeginObject(contentId);
            Text($"<< /Length {content.Length} >>\nstream\n{content}endstream\nendobj\n");
            BeginObject(imageId);
            Text($"<< /Type /XObject /Subtype /Image /Width {bitmap.PixelWidth} /Height {bitmap.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
            file.Write(jpeg);
            Text("\nendstream\nendobj\n");
        }

        var xref = file.Position;
        Text($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Text($"{offset:D10} 00000 n \n");
        Text($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    private static byte[] Jpeg(BitmapSource bitmap, int quality)
    {
        // JPEG has no alpha: flatten onto the (already opaque) page first.
        var opaque = new FormatConvertedBitmap(bitmap, System.Windows.Media.PixelFormats.Bgr24, null, 0);
        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(opaque));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
