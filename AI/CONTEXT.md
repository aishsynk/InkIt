# InkIt durable context

## Product

- Windows screen-annotation and presentation application, branded as **InkIt**.
- WPF application targeting `net8.0-windows10.0.19041.0`.
- Uses PerMonitorV2 DPI awareness.
- Solution: `ScreenCanvas.slnx`; application project: `src/ScreenCanvas/ScreenCanvas.csproj` (produces `InkIt.exe`).
- Self-contained win-x64 publish output: `artifacts/publish/win-x64/`.
- Production setup installer: `artifacts/installer/InkIt_Setup_v1.0.0.exe`.
- Application Icon: transparent brush stroke artwork (`src/ScreenCanvas/Assets/InkIt.ico` with 16 to 256px layers).

## Current UI architecture

- Three-tier progressive disclosure model: Core Toolbar (13 controls) → Contextual Options → Advanced Capability Centre.
- Toolbar is a compact floating bar (~42 px high horizontal, ~42 px wide vertical, 8 px corner radius, 32 px hit targets, 20 px Fluent icons) with dynamic orientation toggle (`Ctrl+Shift+O` or More menu).
- Dedicated attached contextual progressive disclosure inspectors float adjacent to the toolbar (Pen, Highlighter, Shapes, Text, Laser/Present, Zoom, Color).
- 6 primary teaching colors: Blue `#2563EB`, Red `#E5484D`, Green `#16A34A`, Amber `#F2B705`, Purple `#7C3AED`, Adaptive Neutral (`#0F172A` / `#FFFFFF`), represented by one active color chip orb on the toolbar.
- Presenter capabilities: Sequential Step Markers (1, 2, 3...), code focus slit band (`CodeFocusService`), non-activating keyboard visualizer HUD (`KeyVisualizerService`).
- Advanced hubs:
  - Capability Centre (`CapabilityCentreWindow`): 100+ commands across 8 categories (Annotate, Shapes, Present, Screen, Privacy, Board, Record, Tools).
  - Command Palette (`CommandPaletteWindow`): `Ctrl+Shift+P` / `Ctrl+K` keyboard-first command launcher.
  - Radial Quick Menu (`RadialMenuWindow`): circular HUD around cursor.
- Interaction contract: `ANY TOOL → ESC → CURSOR → CLICK-THROUGH IMMEDIATELY ACTIVE`. Overlays are `WS_EX_TRANSPARENT`.
- Input priority: `IUiExclusionRegionService` with `WM_NCHITTEST` `HTTRANSPARENT` pass-through ensures toolbar and floating inspectors always have higher priority than the transparent overlay, enabling direct 1-click tool switching without Esc.

## Validation

- Runtime and geometry evidence is recorded in `QA_RESULTS.md` and `walkthrough.md`.
- Release build command: `dotnet build ScreenCanvas.slnx -c Release --no-restore` (0 warnings, 0 errors).
- Self-contained publish command: `dotnet publish src/ScreenCanvas/ScreenCanvas.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64`.
- Installer compile command: `& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" packaging\installer.iss` (outputs `artifacts/installer/InkIt_Setup_v1.0.0.exe`).
- Performance validation: settled clean startup sample of the published `InkIt.exe` win-x64 package measured **0.00% idle CPU** over 10-second sample (225.8 MB working set, 144.8 MB private memory).

## Azure guidance

- No Azure dependency or deployment target is currently documented.
- If Azure work is requested, validate identity and subscription first with `az account show`.
- Reuse existing approved resources and deployment standards. Do not create resources unless explicitly required.
- Never record secrets or connection strings in repository files.
