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
