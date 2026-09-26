using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;

namespace ScreenCanvas.Overlay;

/// <summary>
/// The .inkit file: a zip with one entry per page for ink (ISF) and one for shapes, text and stamps (XAML),
/// plus a small manifest. Lets a trainer prepare drawings in advance and reopen them in class.
/// </summary>
public static class InkFile
{
    public const string Extension = ".inkit";
    public const string Filter = "InkIt drawings (*.inkit)|*.inkit";

    private sealed record Manifest(string App, string Version, int Pages);

    public static void Save(string path, IReadOnlyList<InkPage> pages)
    {
        var temp = path + ".tmp";
        using (var file = File.Create(temp))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
        {
            Write(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(new Manifest("InkIt", AppInfo.Version, pages.Count)));
            for (var i = 0; i < pages.Count; i++)
            {
                Write(zip, $"page-{i + 1}/ink.isf", OverlayWindow.SaveStrokes(pages[i].Strokes));
                var shapes = pages[i].Shapes.Select(XamlWriter.Save).ToArray();
                Write(zip, $"page-{i + 1}/shapes.json", JsonSerializer.SerializeToUtf8Bytes(shapes));
            }
        }
        File.Move(temp, path, overwrite: true);
    }

    public static IReadOnlyList<InkPage> Load(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var manifest = JsonSerializer.Deserialize<Manifest>(Read(zip, "manifest.json") ?? throw new InvalidDataException("Not an InkIt drawings file."))
                       ?? throw new InvalidDataException("Not an InkIt drawings file.");
        var pages = new List<InkPage>();
        for (var i = 1; i <= manifest.Pages; i++)
        {
            var page = new InkPage();
            if (Read(zip, $"page-{i}/ink.isf") is { } ink) page.Strokes = OverlayWindow.LoadStrokes(ink);
            if (Read(zip, $"page-{i}/shapes.json") is { } json)
                foreach (var xaml in JsonSerializer.Deserialize<string[]>(json) ?? [])
                    if (XamlReader.Parse(xaml) is UIElement element) page.Shapes.Add(element);
            pages.Add(page);
        }
        return pages;
    }

    private static void Write(ZipArchive zip, string name, byte[] data)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(data);
    }

    private static byte[]? Read(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        if (entry is null) return null;
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
