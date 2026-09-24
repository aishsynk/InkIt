# Interaction QA results

## Runtime verified

- Application startup and responsive message loop
- Cursor-mode idle CPU: 0.00% over a settled five-second sample
- Toolbar selection and contextual flyout activation
- Emergency hotkey registration and clean process shutdown

## Fixed by this pass

- Lazy overlay creation; no full-screen overlay surfaces at startup
- Per-monitor pixel placement through `SetWindowPos` and PerMonitorV2 DPI awareness
- Active-tool toolbar highlight
- Vector-object eraser with expanded hit-test tolerance
- Ink erase operations participate in undo/redo history
- Clear is undoable and redoable as one operation
- Tool changes cancel unfinished shape previews and release mouse capture
- Shift constrains rectangles and ellipses as well as arrows
- Pen opacity is applied to actual drawing attributes
- Boards activate an annotation-capable overlay

## Not manually verified

The Windows automation run was stopped with the physical Escape key before drawing QA completed. Pen handwriting, every individual shape, width comparison, fading, text input, marker placement, erase sequences, and mixed-DPI hardware alignment therefore remain manual QA requirements and are not marked PASS.

## Performance

- Before: approximately 490 MB working set, 0.00% settled idle CPU.
- Cause: three full-screen transparent layered WPF overlay windows were created immediately at startup.
- Change: overlays are now constructed lazily on first annotation activation and empty overlays may hide safely.
- After final integration: approximately 206 MB working set and 138 MB private memory at clean Cursor-mode startup, 0.00% settled idle CPU.
- Limitation: improved materially; longer annotation/media sessions still need profiling.

## Parallel sprint validation

- Release integration build: PASS, 0 warnings/errors.
- Per-monitor lazy overlay smoke: PASS; overlays instantiate only for monitors used for annotation.
- Audio/video discovery: PASS; 4 audio devices and 1 webcam found.
- Capture selector, settings, zoom, presentation and recording modules compile and fail safely when native capability is unavailable.
- Windows-native OCR: PASS; local synthetic image recognized “ScreenCanvas OCR 123”.
- Recording output BLOCKED: Media Foundation encoder backend is not implemented; no fake files are produced.
- Static zoom: implemented with frozen capture, pointer-centered 1.5–4× scaling, wheel adjustment, pan and emergency exit; annotation-over-zoom remains limited.

## Three-level palette validation

- Managed Main / Level 1 / Level 2 palette host: PASS.
- Mark aggregation: PASS for Write, Emphasize, Point, Erase and Presets.
- Structure aggregation: PASS for Lines, Arrows, Geometry, Flow, Emphasis and Markers.
- Focus, Screen, Board and More expose only implemented commands.
- Level 1 rows use icon, label and chevron without descriptive copy.
- Main, Level 1 and Level 2 rendered at the same 538 px physical height with aligned top and bottom edges.
- Right-edge placement expands both menu levels inward without moving the main toolbar.
- Pen selection replaces Level 2 with color, width, opacity and live preview controls.
- Escape closes both menu levels and restores Cursor mode: PASS.
- Final Release build and win-x64 framework-dependent publish: PASS, 0 warnings, 0 errors.
- Published-process sample: 222.3 MB working set, 140.8 MB private memory, 1.45% CPU over 30 seconds on the validation host. The prior clean Cursor-mode settled sample remains 0.00%; extended profiling is still recommended.

## Continuous trainer palette validation

- Replaced the separated three-card treatment with one clipped, 14 px-radius outer shell and subtle 1 px internal dividers.
- Main, Level 1 and Level 2 remain aligned at 538 px while opening as one continuous surface.
- Light and dark runtime screenshots: PASS for Main, Mark hierarchy and ink-property views.
- Level 1 is 156 px; Level 2 is 284 px. Compact 36 px rows, persistent Recent shortcuts and Quick Setup reduce dead space.
- Ink properties use a narrow accent selection marker, 20 px swatches, compact width chips and thin accent-fill sliders.
- Production safety restored: hidden taskbar entry, normal ScreenCanvas title and System theme preference.
- Final Release build and win-x64 publish: PASS, 0 warnings, 0 errors.
- Final published startup: responsive; 208.0 MB working set, 140.8 MB private memory, 0.00% settled idle CPU over five seconds.

## Strict geometry and icon correction

- Runtime main-only window: 48 x 538 DIP; Level 1 and Level 2 collapsed to 0 DIP.
- Runtime Main + Level 1 window: 201 x 538 DIP; columns 48 + 1 + 152 DIP.
- Runtime fully expanded window: 450 x 538 DIP; columns 248 + 1 + 152 + 1 + 48 DIP.
- Instrumented main-rail screen X: 1416 DIP closed, 1417 DIP with Level 1, 1417 DIP with Level 2; maximum movement 1 DIP.
- Escape returned the live window to 48 x 538 DIP and Cursor mode.
- Main icon presenter: 20 DIP in 38 DIP hit targets at 1.75 DIP stroke; Level 1 icons: 18 DIP.
- Property inspector: 18 DIP swatches, four equal 54 DIP preset cells, 200 DIP sliders, 3 DIP tracks, 14 DIP thumbs, and 208 x 44 DIP preview.
- Light Main, Mark, Mark + Write and property inspector screenshots reviewed at runtime.
- Dark Main and Dark Mark + Write screenshots reviewed at runtime.
- Final startup sample after 25 seconds: 199.1 MB working set, 131.2 MB private memory, 7.34% CPU over ten seconds. CPU did not reproduce the earlier 0.00% idle result and remains open for profiling.

## Single attached palette rebuild

- Removed the visible Mark and Write navigation columns and the cascading three-column runtime layout.
- Runtime main-only width: 48 DIP. Runtime expanded width: 329 DIP (280 DIP palette + 1 DIP divider + 48 DIP rail).
- Ink Write, Highlight and Laser render by switching content inside one attached palette.
- Shapes Basic and Arrows render inside the same single-palette model.
- Write uses a two-column icon/label grid; categories use a three-column segmented selector; Recents uses three chips.
- Light runtime screenshots reviewed for main, Ink Write, Ink Highlight, Ink Laser, Shapes Basic and Shapes Arrows.
- Production Release build and win-x64 publish: PASS, 0 errors, 0 warnings.
- Published startup after 20-second settle: 193.8 MB working set, 123.2 MB private memory, 0.00% CPU over ten seconds.

## Microsoft Fluent System Icons & Tool Switching UX Validation

- Replaced all stroked wireframe icons with official Microsoft Fluent System Icons (`microsoft/fluentui-system-icons`).
- Created `src/ScreenCanvas/UI/Icons/FluentIconResources.xaml` with 112 geometries and 63 legacy aliases; deleted `IconResources.xaml`.
- Filled silhouette icon rendering applied across `ToolbarWindow.xaml`, `CategoryButton`, `ToolTile`, and flyout action rows.
- Full Icon Coverage Test (Section 17) generated and inspected at production size (`artifacts/screenshots/qa_coverage.png`).
- Main toolbar (48 DIP) inspected: `artifacts/screenshots/qa_main.png`.
- Ink flyout inspected: `artifacts/screenshots/qa_ink.png` (displays [✕] close button, filled Write category icon, sharp 18 DIP tool icons).
- Focus flyout inspected: `artifacts/screenshots/qa_focus.png` (displays [✕] close button, filled Pointer category icon, Laser/Trail/Halo tools).
- Tool switching UX: clicking primary buttons activates the tool immediately and toggles the palette; drawing interaction starts automatically dismiss the palette; overlay click-through ignores toolbar bounds; Escape dismisses open palette first without resetting active tool to Cursor.
- Release build (`dotnet build ScreenCanvas.slnx -c Release --no-restore`): PASS, 0 warnings, 0 errors.
- Framework-dependent publish (`dotnet publish -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64`): PASS.
- Published runtime performance benchmark (settled 20s): 217.29 MB working set, 138.44 MB private memory, 0.00% settled idle CPU.

## Sticky Tool Lifecycle, Direct Switching & Settings Memory Validation

- Implemented sticky drawing tools: tools remain selected and active across consecutive drawings without reselection.
- Direct tool switching: users switch directly between tools with 1 click; 0 required intermediate Cursor selections, 0 Esc presses.
- Reverted Esc to global tool termination: Esc cancels in-flight previews, retains committed annotations, and returns to Cursor mode with desktop click-through.
- Per-tool settings memory: Ballpoint, Highlighter, Arrow, Rectangle, and Laser remember their own distinct colors, widths, and opacities without cross-tool leakage.
- Last-used subtool memory: family buttons remember and restore last-used subtools (Ink -> Ballpoint, Shapes -> Arrow, Focus -> Laser).
- Acceptance test suite executed:
  - Test 1 (Section 18 Trainer Workflow Acceptance): PASS (Arrow drawn 3x consecutively; switched to Pen; wrote 2 strokes; switched to Highlighter; highlighted 2 areas; switched to Laser; used 2x; Esc restored Cursor + click-through).
  - Test 2 (Section 19 Direct Tool Switching Loop): PASS (Arrow → Rectangle → Pen → Eraser → Arrow; 0 intermediate Cursor selections, 0 Esc presses).
  - Test 3 (Section 11 Per-Tool Settings Memory): PASS (Ballpoint Blue/4px, Arrow Red/6px, Highlighter Gold/18px/115 preserved independently).
  - Test 4 (Section 10 Last-Used Subtool Memory): PASS (Shapes restored Arrow, Ink restored Ballpoint).
- Release build (`dotnet build ScreenCanvas.slnx -c Release --no-restore`): PASS, 0 errors, 0 warnings.
- Framework-dependent publish (`dotnet publish src/ScreenCanvas/ScreenCanvas.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64`): PASS.
- Published runtime performance benchmark (settled 20s): 195.82 MB working set, 121.75 MB private memory, 0.00% settled idle CPU.

## Toolbar Input Override & UI Exclusion Zone Validation

- Introduced `IUiExclusionRegionService` in `IOverlayManager` and implemented on `ToolbarWindow`.
- Intercepted `WM_NCHITTEST` in `OverlayWindow`'s `HwndSource` WndProc hook, returning `HTTRANSPARENT` over UI exclusion bounds (`ToolbarChrome`, open `ToolFlyout`, popups).
- Windows routes all native hover, cursor, tooltip, and click events directly to `ToolbarWindow` without deactivating the active drawing tool.
- Maintained topmost Z-order via `NativeMethods.HwndTopMost` and `EnsureToolbarTopmost()`.
- Active shape/pen drags retain pointer capture mid-drag to prevent accidental clicks or drawing into the toolbar; MouseUp immediately restores toolbar interactivity.
- Automated acceptance test suite executed:
  - Toolbar Access While Active: PASS (Pen, Arrow, Highlighter, Laser active; toolbar points confirmed as UI; hover/clicks functional).
  - Direct Switching: PASS (Pen → Arrow, Arrow → Highlighter, Highlighter → Laser, Laser → Pen switched directly in 1 click without Esc).
  - Palette Interaction: PASS (Pen active, opened Ink palette, changed to Fountain, changed color to Gold, changed width to 7px, returned to desktop and drew immediately).
  - Esc: PASS (Active tool ended, Cursor selected, click-through restored).
  - Z-Order / Hit Test: PASS (Toolbar and palette top-priority in hit-testing; shape drag capture retained and released).
- Release build (`dotnet build ScreenCanvas.slnx -c Release --no-restore`): PASS, 0 errors, 0 warnings.
- Framework-dependent publish (`dotnet publish src/ScreenCanvas/ScreenCanvas.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64`): PASS.
- Published runtime performance benchmark (settled 20s): 195.68 MB working set, 121.48 MB private memory, 0.32% settled idle CPU.

## Apple-Grade UI/UX Design System & Animation Overhaul Validation

- Frosted Glass Materials: Adopted translucent background `#E61C1C20`, specular highlight gradient border `GlassSpecularBrush` (`#3DFFFFFF` $\rightarrow$ `#0CFFFFFF`), and 28–32px ambient occlusion drop shadows.
- Continuous Squircles: Upgraded corners to 16 DIP smooth continuous radii on toolbar rail and flyout panels.
- Micro-Interactions: Added spring physics scale animations on button hover (`Scale: 1.0 -> 1.06`, 120ms cubic ease-out) and press (`Scale: 1.06 -> 0.93`, 60ms spring compression).
- Dynamic Island Minimization: Collapse button and double-click trigger smooth height morphing from 538 DIP down to a compact 48x112 DIP floating pill, rotating the chevron 180° with spring physics. Retains drag handle, live active color orb, and active tool button.
- Live Active Color Orb: Added dynamic illuminated color bead on the primary toolbar rail reflecting the current tool's active color with a neon glow aura. Clicking orb toggles ink palette.
- Circular Enamel Beads: Replaced square color swatches with 18 DIP enamel color beads featuring hover expansion (1.2x scale) and concentric outer accent rings on the selected swatch.
- Apple Keynote Laser Bloom: Upgraded `CreateLaserDot` with an intense white hot-core center (`StrokeThickness=2.5, Stroke=White`) and 24px neon bloom.
- Visual inspection & screenshots:
  - `artifacts/screenshots/qa_apple_main.png` (Full frosted glass toolbar with single unified active color orb).
  - `artifacts/screenshots/qa_apple_ink.png` (Expanded ink palette with ultra-thin 4 DIP floating scrollbar, no gray boundaries, circular enamel beads).
  - `artifacts/screenshots/qa_apple_compact.png` (Dynamic Island 138 DIP compact floating pill with visible Up chevron).
- Automated Acceptance Suite: PASS (100% success across all 5 verification categories).
- Release build (`dotnet build ScreenCanvas.slnx -c Release --no-restore`): PASS, 0 errors, 0 warnings.
- Framework-dependent publish (`dotnet publish src/ScreenCanvas/ScreenCanvas.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64`): PASS.
- Published runtime performance benchmark (settled 20s): 224.99 MB working set, 145.54 MB private memory, 0.64% settled idle CPU.

## Apple-Grade Floating Creative-Tool Redesign Validation

- 60px Floating Vertical Capsule: Near-white translucent material `rgba(250, 250, 252, 0.93)`, 20 DIP continuous squircle corners, 42px hit area, 22px optical icon size, 4px vertical rhythm, subtle centered drag pill affordance.
- Unified Iconography: Cohesive Fluent System Icons Rounded family (22px optical size, identical stroke weight and bounding box).
- Direct Main Rail Tool Order:
  - Move/Drag affordance
  - Pointer (Cursor)
  - Pen (direct 1-click write)
  - Highlighter (direct 1-click highlight)
  - Shapes
  - Text
  - Spotlight / Laser
  - Screen / Board
  - (Separator)
  - Undo, Redo, Clear
  - (Separator)
  - More (Options)
  - Collapse / Expand (Dynamic Island)
- Active Selection State: Soft blue translucent pill background (`rgba(0, 122, 255, 0.14)`), Apple system blue icon (`#007AFF`), 140ms ease-out transitions.
- Contextual Progressive Disclosure Inspectors:
  - Pen Inspector: Segmented nib pills (`[Ballpoint] [Fountain] [Pencil] [Marker]`), 7 Apple swatches (`Black`, `Red`, `Yellow`, `Green`, `Cyan`, `Blue`, `Purple`) with concentric outer ring + `+` custom picker, visual rendered stroke cards (`Thin 2px`, `Medium 4.5px`, `Thick 8px`), live stroke preview, and progressive links (`More Pens…`, `Effects >`).
  - Highlighter Inspector: `[Freehand]` / `Straight Line` pills, vibrant translucent palette, fine/medium/broad cards, live preview.
  - Shapes Inspector: 6-shape primary grid (`Rectangle`, `Rounded`, `Ellipse`, `Line`, `Arrow`, `Double Arrow`), color swatches, stroke cards, `More Shapes…` progressive disclosure.
  - Text Inspector: Font size chips (16, 22, 30, 42), bold/italic/background toggles, color swatches.
  - Laser Inspector: Laser / Spotlight / Halo & Pulse pills, laser colors.
  - Screen & Board Inspector: Capture tools, Whiteboard/Blackboard/Clear board surfaces.
- Visual inspection & screenshots:
  - `artifacts/screenshots/qa_apple_main.png` (Main 60px floating vertical capsule toolbar).
  - `artifacts/screenshots/qa_apple_pen.png` (Contextual Pen Inspector with nibs, swatches, and visual stroke cards).
  - `artifacts/screenshots/qa_apple_highlighter.png` (Contextual Highlighter Inspector with modes and translucent palette).
  - `artifacts/screenshots/qa_apple_shapes.png` (Contextual Shapes Inspector with primary grid and swatches).
  - `artifacts/screenshots/qa_apple_compact.png` (Dynamic Island 128 DIP compact capsule).
- Automated Acceptance Suite (14 Invariants): PASS (100% success).
- Release build (`dotnet build ScreenCanvas.slnx -c Release --no-restore`): PASS, 0 errors, 0 warnings.
- Framework-dependent publish (`dotnet publish src/ScreenCanvas/ScreenCanvas.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64`): PASS.
- Published runtime performance benchmark (settled 20s): 201.02 MB working set, 126.53 MB private memory, 0.32% settled idle CPU.

## InkIt Product Branding, Transparent Icon & Self-Contained Installer Validation

- Product Renaming: Application assembly and binary named `InkIt.exe` / `InkIt.dll`, window title set to "InkIt", assembly manifest identity set to `InkIt.app`.
- Transparent Icon: Processed user-uploaded brush stroke asset into `InkIt.ico` and `InkIt.png`. All background pixels are 100% transparent alpha `0`. Generated multi-resolution Windows icon layers: 256x256, 128x128, 64x64, 48x48, 32x32, 16x16 with 32-bit RGBA channels. Embedded as `<ApplicationIcon>` into `InkIt.exe`.
- Self-Contained Dependency Bundling: Published with `--self-contained true` to `artifacts/publish/win-x64/`. The output bundles the complete .NET 8 runtime (`coreclr.dll`, `clrjit.dll`), WPF engine (`wpfgfx_cor3.dll`, `PresentationCore.dll`), WinForms, and all required BCL dependencies. Users do not need .NET 8 or any external runtimes pre-installed.
- Production Windows Setup Installer: Compiled `artifacts/installer/InkIt_Setup_v1.0.0.exe` (54.1 MB) using Inno Setup 6. Features:
  - Clean installation into `{autopf}\InkIt`
  - Start Menu & Desktop shortcuts with transparent `InkIt.ico`
  - Registered Windows Add/Remove Programs uninstaller
  - OS check for 64-bit Windows 10/11 (min build 19041)
  - Optional post-install launch
- Executable Test Run: `artifacts/publish/win-x64/InkIt.exe` launched cleanly, identified process `InkIt.exe`, consumed 59.1 MB memory at launch, and terminated cleanly.



## Stabilization pass — 2026-09-24 (Claude Opus 5.5)

Method: real runtime input driven from PowerShell. UI Automation located toolbar buttons, `SetCursorPos`/`mouse_event`/`keybd_event` sent input, and GDI screenshots plus pixel sampling verified the results. Overlay click-through was checked from its `WS_EX_TRANSPARENT` extended style. Live Zoom was checked via `MagGetFullscreenTransform`. Tested on a 1920×1080 display at 100% scale. Release build first, then the published self-contained exe.

| Area | Result |
| --- | --- |
| Toolbar launch / drag-move / position persists across restart | PASS |
| Toolbar and palette stay above the overlay (ink drawn across the toolbar renders underneath) | PASS |
| Palette open / close / toggle, no stuck palette | PASS (after fixing Present, see below) |
| Direct switching Pen → Arrow → Highlighter → Laser → Pen, one click each, no Esc | PASS |
| Sticky Pen (3 strokes), sticky Arrow (3 arrows), sticky Rectangle (2), sticky Text (2 entries) | PASS |
| Hover and click on toolbar while Pen is active | PASS |
| Single Esc → Cursor + click-through from Pen, Arrow, Laser, Highlighter | PASS (after fix, see below) |
| Undo / Redo / Clear / Undo-after-Clear (pixel-verified); palette stays open | PASS |
| Shapes Line, Arrow, Rectangle, Ellipse: live preview, reversed drag direction, size | PASS |
| Text: click → type → commit, then a second text box without reselecting | PASS |
| Spotlight, Live Zoom (2.0× → Esc → 1.0×), Screenshot region selector (Esc cancels), Whiteboard (Esc exits) | PASS |
| Capability Centre (7 categories, 46 commands), Command Palette, Settings sections | PASS: no Record/Blur/Eyedropper/diagram-shape entries |
| More → Exit clean shutdown (Release and published exe) | PASS |
| Published exe: startup, Pen, direct switch to Arrow, Esc, clean exit | PASS |

Defects found and fixed in this pass:
- Present palette never appeared. `Present_Click` executed the `present` command, which re-invoked `Present_Click` through `onInspectCategory` and toggled the just-opened palette shut. The handler now sets Laser directly.
- Esc with a palette open (always the case for Shapes and Laser) only closed the palette, so a second Esc was needed. The global Esc now always ends the tool (per the Global Esc invariant).
- Drawing a Line opened a text editor. A failed automated replacement had changed `ShapeKind.Callout` to `ShapeKind.Line`. Removed.
- Line default width silently changed 4 → 6 because of duplicate `ToolProfileStore` defaults left by the same replacement. Removed the duplicates.
- Dead remnants of removed features: the `record.screen` command (did nothing when run) and the empty `Record` Capability Centre category, the Blur settings section, blur/eyedropper overlay code, a "connector" preset id. Removed.
- CS0169 warning: unused `InspectorWindow._showMoreShapes`. Removed.

Performance (published self-contained exe, 12 logical cores):
- Clean startup, 20 s settle, 10 s sample: **216.1 MB working set, 140.9 MB private, 0.00% idle CPU**.
- After a drawing session (overlay and palettes created): 315.6 MB working set, 227.1 MB private, 0.00% idle CPU.

Build: `dotnet build ScreenCanvas.slnx -c Release --no-incremental` → 0 errors, 0 warnings.
