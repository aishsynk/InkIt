using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ScreenCanvas.UI.Theme;

/// <summary>Tailwind palette values used by the InkIt design (dark slate chrome with coloured accents).</summary>
public static class Tw
{
    public static readonly Color Slate50 = Hex("#F8FAFC");
    public static readonly Color Slate100 = Hex("#F1F5F9");
    public static readonly Color Slate200 = Hex("#E2E8F0");
    public static readonly Color Slate300 = Hex("#CBD5E1");
    public static readonly Color Slate400 = Hex("#94A3B8");
    public static readonly Color Slate500 = Hex("#64748B");
    public static readonly Color Slate600 = Hex("#475569");
    public static readonly Color Slate700 = Hex("#334155");
    public static readonly Color Slate800 = Hex("#1E293B");
    public static readonly Color Slate900 = Hex("#0F172A");
    public static readonly Color Slate950 = Hex("#020617");

    public static readonly Color Blue100 = Hex("#DBEAFE");
    public static readonly Color Blue200 = Hex("#BFDBFE");
    public static readonly Color Blue300 = Hex("#93C5FD");
    public static readonly Color Blue400 = Hex("#60A5FA");
    public static readonly Color Blue500 = Hex("#3B82F6");
    public static readonly Color Blue600 = Hex("#2563EB");
    public static readonly Color Blue700 = Hex("#1D4ED8");
    public static readonly Color Blue900 = Hex("#1E3A8A");
    public static readonly Color Blue950 = Hex("#172554");

    public static readonly Color Indigo300 = Hex("#A5B4FC");
    public static readonly Color Indigo400 = Hex("#818CF8");
    public static readonly Color Indigo500 = Hex("#6366F1");
    public static readonly Color Indigo600 = Hex("#4F46E5");
    public static readonly Color Indigo700 = Hex("#4338CA");
    public static readonly Color Indigo950 = Hex("#1E1B4B");

    public static readonly Color Purple200 = Hex("#E9D5FF");
    public static readonly Color Purple300 = Hex("#D8B4FE");
    public static readonly Color Purple400 = Hex("#C084FC");
    public static readonly Color Purple500 = Hex("#A855F7");
    public static readonly Color Purple600 = Hex("#9333EA");
    public static readonly Color Purple700 = Hex("#7E22CE");
    public static readonly Color Purple950 = Hex("#3B0764");

    public static readonly Color Amber300 = Hex("#FCD34D");
    public static readonly Color Amber400 = Hex("#FBBF24");
    public static readonly Color Amber500 = Hex("#F59E0B");
    public static readonly Color Amber600 = Hex("#D97706");
    public static readonly Color Amber700 = Hex("#B45309");
    public static readonly Color Amber800 = Hex("#92400E");
    public static readonly Color Amber950 = Hex("#451A03");

    public static readonly Color Rose300 = Hex("#FDA4AF");
    public static readonly Color Rose400 = Hex("#FB7185");
    public static readonly Color Rose500 = Hex("#F43F5E");
    public static readonly Color Rose600 = Hex("#E11D48");
    public static readonly Color Rose700 = Hex("#BE123C");
    public static readonly Color Rose800 = Hex("#9F1239");
    public static readonly Color Rose950 = Hex("#4C0519");

    public static readonly Color Red300 = Hex("#FCA5A5");
    public static readonly Color Red400 = Hex("#F87171");
    public static readonly Color Red500 = Hex("#EF4444");
    public static readonly Color Red600 = Hex("#DC2626");
    public static readonly Color Red800 = Hex("#991B1B");
    public static readonly Color Red950 = Hex("#450A0A");

    public static readonly Color Emerald300 = Hex("#6EE7B7");
    public static readonly Color Emerald400 = Hex("#34D399");
    public static readonly Color Emerald500 = Hex("#10B981");
    public static readonly Color Emerald600 = Hex("#059669");

    public static readonly Color Sky200 = Hex("#BAE6FD");
    public static readonly Color Sky300 = Hex("#7DD3FC");
    public static readonly Color Sky400 = Hex("#38BDF8");

    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);

    /// <summary>A frozen brush for <paramref name="color"/> at <paramref name="alpha"/> (0..1).</summary>
    public static SolidColorBrush B(Color color, double alpha = 1)
    {
        var brush = new SolidColorBrush(WithAlpha(color, alpha * color.A / 255d));
        brush.Freeze();
        return brush;
    }
}
