using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ScreenCanvas.Ocr;

public sealed class LocalOcrService : IOcrService
{
    private readonly OcrEngine? _engine;
    public bool IsAvailable => _engine is not null;
    public string EngineName => IsAvailable ? "Windows OCR (local)" : "Windows OCR language pack unavailable";

    public LocalOcrService(string? ignored = null)
    {
        try { _engine = OcrEngine.TryCreateFromUserProfileLanguages(); } catch { _engine = null; }
    }

    public async Task<OcrResult> RecognizeAsync(Bitmap image, string language = "", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_engine is null) throw new NotSupportedException("Windows OCR is unavailable. Install a Windows OCR language pack in Windows Language settings.");
        using var encoded = new MemoryStream();
        image.Save(encoded, ImageFormat.Png);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(encoded.ToArray());
            await writer.StoreAsync();
            await writer.FlushAsync();
        }
        stream.Seek(0); cancellationToken.ThrowIfCancellationRequested();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var timer = Stopwatch.StartNew();
        var result = await _engine.RecognizeAsync(softwareBitmap);
        timer.Stop(); cancellationToken.ThrowIfCancellationRequested();
        return new OcrResult(result.Text.Trim(), _engine.RecognizerLanguage.LanguageTag, timer.Elapsed);
    }
}
