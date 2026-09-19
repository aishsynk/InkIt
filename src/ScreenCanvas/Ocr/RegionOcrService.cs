using ScreenCanvas.Capture;
using System.Windows;

namespace ScreenCanvas.Ocr;

public sealed class RegionOcrService(ICaptureService capture, IOcrService ocr)
{
    public async Task<OcrResult?> RecognizeInteractiveAsync(Window? owner = null, string language = "eng", CancellationToken cancellationToken = default)
    {
        using var bitmap = capture.CaptureInteractiveRegion(owner);
        return bitmap is null ? null : await ocr.RecognizeAsync(bitmap, language, cancellationToken);
    }
}
