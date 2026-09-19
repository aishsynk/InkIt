using System.Drawing;
using ScreenCanvas.Ocr;

using var bitmap = new Bitmap(900, 180);
using (var graphics = Graphics.FromImage(bitmap))
{
    graphics.Clear(Color.White);
    using var font = new Font("Segoe UI", 52, FontStyle.Bold);
    graphics.DrawString("ScreenCanvas OCR 123", font, Brushes.Black, 18, 42);
}
var service = new LocalOcrService();
Console.WriteLine($"Available={service.IsAvailable}; Engine={service.EngineName}");
if (!service.IsAvailable) return 2;
var result = await service.RecognizeAsync(bitmap);
Console.WriteLine(result.Text);
return result.Text.Contains("ScreenCanvas", StringComparison.OrdinalIgnoreCase) ? 0 : 3;
