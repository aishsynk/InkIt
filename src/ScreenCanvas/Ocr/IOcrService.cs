using System.Drawing;

namespace ScreenCanvas.Ocr;

public interface IOcrService
{
    bool IsAvailable { get; }
    string EngineName { get; }
    Task<OcrResult> RecognizeAsync(Bitmap image, string language = "eng", CancellationToken cancellationToken = default);
}

public sealed record OcrResult(string Text, string Language, TimeSpan Duration)
{
    public string EditableText { get; set; } = Text;
    public void CopyToClipboard() => System.Windows.Clipboard.SetText(EditableText ?? string.Empty);
}
