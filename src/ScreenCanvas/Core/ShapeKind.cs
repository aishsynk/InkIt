namespace ScreenCanvas.Core;

public enum ShapeKind
{
    Line,
    Arrow,
    DoubleArrow,
    Rectangle,
    RoundedRectangle,
    Ellipse,
    Diamond,
    /// <summary>Only produced by smart shapes (a hand-drawn triangle).</summary>
    Triangle
}
