using System.Windows.Ink;
using Windows.UI.Input.Inking;

namespace ScreenCanvas.Overlay;

/// <summary>Turns handwritten ink into text with the handwriting recognizer built into Windows (offline).</summary>
public static class HandwritingRecognizer
{
    public static async Task<string?> RecognizeAsync(IEnumerable<Stroke> strokes)
    {
        var builder = new InkStrokeBuilder();
        var container = new InkStrokeContainer();
        foreach (var stroke in strokes)
        {
            var points = stroke.StylusPoints.Select(p => new Windows.Foundation.Point(p.X, p.Y)).ToList();
            if (points.Count == 1) points.Add(new Windows.Foundation.Point(points[0].X + 0.5, points[0].Y + 0.5));
            if (points.Count > 1) container.AddStroke(builder.CreateStroke(points));
        }
        if (container.GetStrokes().Count == 0) return null;
        try
        {
            var recognizer = new InkRecognizerContainer();
            var results = await recognizer.RecognizeAsync(container, InkRecognitionTarget.All);
            var words = results.Select(r => r.GetTextCandidates().FirstOrDefault()).Where(w => !string.IsNullOrWhiteSpace(w));
            var text = string.Join(" ", words);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }
}
