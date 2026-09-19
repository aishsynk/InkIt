# ScreenCanvas

ScreenCanvas is an independent Windows presentation overlay built with C# and WPF on .NET 8. It aims to combine fast screen annotation with zoom, capture, recording, and presenter utilities while remaining lightweight and fully native.

## Current milestone

The repository currently contains the runnable Phase 1/2 foundation:

- 48 px icon-first vertical toolbar with collapse and contextual flyouts
- notification-area menu
- one transparent overlay per monitor
- safe click-through cursor mode
- mouse, touch, and pressure-aware Windows Ink input
- pen, highlighter, stroke eraser, color selection
- live-preview vector lines, arrows, double arrows, rectangles, rounded rectangles, ellipses, triangles and diamonds
- advanced live-preview corporate diagram, connector, callout and trainer symbols
- formatted inline text, number/letter markers, per-object fade presets and laser pointer foundation
- black/white/custom presentation boards
- full-desktop and monitor capture to clipboard or PNG/JPEG
- undo, redo, and clear
- blackboard surface and cursor spotlight foundations
- global hotkeys, including an emergency release shortcut

This is not yet feature-complete. See [FEATURE_MATRIX.md](FEATURE_MATRIX.md) for validated status and [QA_RESULTS.md](QA_RESULTS.md) for interaction-pass results.

The compact **More** flyout exposes native live zoom/reset, break timer, DemoType, freeze, curtain, and searchable settings. Capture now includes an interactive region selector.
OCR uses the offline Windows OCR engine and requires an installed Windows OCR language pack. Static Zoom supports wheel zoom, drag pan, number-key presets, and Esc/right-click exit.

## Run

```powershell
dotnet run --project src/ScreenCanvas/ScreenCanvas.csproj
```

Hotkeys:

- `Ctrl+Shift+2` — toggle annotation
- `Ctrl+Shift+Z` — undo
- `Ctrl+Shift+Delete` — clear
- `Alt+Shift+X` — emergency stop: restore click-through and hide the toolbar

## Build

```powershell
dotnet build ScreenCanvas.slnx -c Release
```

## Safety

The overlay starts in click-through mode. The emergency shortcut remains global while the toolbar is hidden. Exiting from the tray closes every overlay and unregisters all hotkeys.
