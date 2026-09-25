using ScreenCanvas.Core;
using MediaColor = System.Windows.Media.Color;

namespace ScreenCanvas.Overlay;

public enum ToolDeactivationReason
{
    ToolSwitch,
    Escape,
    CursorSelected,
    EmergencyRelease
}

public interface IUiExclusionRegionService
{
    bool IsPointOverUi(System.Windows.Point screenPixelPoint);
}

public interface IOverlayManager
{
    event EventHandler? ToolChanged;
    event EventHandler? BoardChanged;
    event EventHandler? InteractionStarted;
    /// <summary>Raised when canvas options (snap, grid, modifiers, curtain, spotlight, board) change.</summary>
    event EventHandler? OptionsChanged;
    /// <summary>Raised when a two-finger pinch on the overlay asks for a new magnifier factor.</summary>
    event EventHandler<double>? PinchZoomRequested;
    void RequestPinchZoom(double factor);
    /// <summary>Raised when zoom-to-area starts, moves to a new area or ends.</summary>
    event EventHandler? ZoomAreaChanged;
    /// <summary>Raised when the zoom bar asks to pick a different area.</summary>
    event EventHandler? ZoomAreaRequested;
    bool IsZoomAreaActive { get; }
    /// <summary>Shows a frozen snapshot of <paramref name="pixelBounds"/> enlarged on the display that contains it.</summary>
    void ShowZoomArea(System.Windows.Media.ImageSource image, System.Drawing.Rectangle pixelBounds);
    void ExitZoomArea();
    void RequestZoomArea();
    BoardKind CurrentBoard { get; }
    void SetBoardKind(BoardKind kind);
    /// <summary>Applies a change to the shared tool settings, refreshes overlays and (optionally) persists it.</summary>
    void UpdateOptions(Action<ToolSettings> change, bool persist = true);
    void NotifyInteractionStarted();
    IUiExclusionRegionService? ExclusionService { get; set; }
    MediaColor? CurrentBoardColor { get; }
    bool IsPointOverUi(System.Windows.Point screenPixelPoint);
    ToolSettings Settings { get; }
    bool IsDrawing { get; }
    
    // Zoom State Access for coordinate translation
    double CurrentZoomFactor { get; }
    System.Windows.Point CurrentZoomOrigin { get; }
    
    void DeactivateCurrentTool(ToolDeactivationReason reason);
    void ActivateTool(ToolKind newTool);
    void SetTool(ToolKind tool);
    void SetPenMode(PenMode mode);
    void SetShape(ShapeKind shape);
    void SetColor(MediaColor color);
    void SetThickness(double thickness);
    void SetOpacity(byte opacity);
    void SetPressureEnabled(bool enabled);
    void SetShapeFill(bool enabled);
    void SetFade(TimeSpan? duration);
    void ToggleDrawing();
    void Undo();
    void Redo();
    void Clear();
    void ResetMarkerSequence();
    void ConfigureMarkers(bool letters, bool square, int start);
    void ToggleBoard(bool dark);
    void SetBoard(MediaColor? color);
    void EmergencyStop();
    IDisposable SuspendAnnotations();
}
