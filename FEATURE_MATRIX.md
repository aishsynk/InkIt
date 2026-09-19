# ScreenCanvas feature matrix

Status legend: **Verified** = built and exercised in this environment; **Built** = compiled but requires hardware/runtime validation; **Foundation** = usable base exists, broader requirements remain; **Planned** = not implemented.

| Feature | Epic-Pen equivalent | ZoomIt equivalent | Our implementation | Status | Test method |
|---|---|---|---|---|---|
| Floating toolbar | Compact tool palette | Tray-driven commands | 48 px native vertical icon palette, draggable, collapsible and hideable | Runtime Verified | Visually inspected at runtime; icons, tooltips and stacking verified |
| Horizontal/vertical/docked/compact modes | Toolbar layouts | N/A | Compact vertical floating mode with one-click collapse | Foundation | Vertical/collapse runtime verified; horizontal/docking pending |
| Always-on-top/auto-hide/opacity | Toolbar behavior | N/A | Topmost and manual hide | Foundation | Window stacking smoke test |
| System tray | Tray controls | Tray controls | Native notification icon and context menu | Built | Open each menu action |
| Click-through mode | Cursor mode | Normal interaction | WS_EX_TRANSPARENT per-monitor overlay | Built | Interact with underlying apps in Cursor mode |
| Emergency release | Escape drawing | Escape modes | Global Alt+Shift+X restores cursor mode and hides toolbar | Built | Trigger while drawing and verify desktop input |
| Multi-monitor overlay | Multi-screen ink | Monitor selection | One overlay per enumerated Windows display; negative coordinates retained | Foundation | Dual-monitor/DPI hardware validation pending |
| DPI/orientation/hot-plug | DPI-aware overlay | Per-monitor behavior | Initial bounds enumeration | Planned | DPI matrix, rotate and reconnect displays |
| Freehand pen | Pen | Draw mode | WPF InkCanvas with curve fitting and pressure enabled | Built | Mouse/stylus stroke inspection |
| Highlighter | Highlighter | Highlight drawing | Translucent InkCanvas highlighter strokes | Built | Draw over light/dark content |
| Stroke/object eraser | Eraser | Undo/clear | Stroke erase plus tolerant vector hit-testing; erase participates in history | Implemented | Build verified; full pointer sequence remains manual QA |
| Pixel/partial eraser | Eraser variants | N/A | Not implemented | Planned | Stroke intersection tests |
| Undo/redo | Undo/redo | Undo | Operation history covers additions, erase and grouped Clear | Implemented | Build verified; mixed-operation runtime sequence pending |
| Clear annotations | Clear all | Clear drawings | Undoable grouped Clear across active overlays | Implemented | Build verified; multi-monitor manual QA pending |
| Color palette/recent/custom | Palette | Quick colors | Native custom color picker | Foundation | Select color and draw; palette persistence pending |
| Width/opacity controls | Pen options | Width/colors | Compact flyout with Fine, Normal, Trainer and Bold presets | Implemented | Flyout visually verified; opacity UI and persistence pending |
| Fading ink per stroke/object | Fading ink | N/A | Shared per-overlay scheduler; Off/2/5/10/30 second presets; individual ink and vector expiry with final-quarter fade | Implemented | Release/startup verified; long-session allocation profiling pending |
| Pressure sensitivity | Tablet support | N/A | Windows Ink pressure is not ignored | Built | Stylus hardware validation pending |
| Mouse/touch/stylus/multitouch | Input support | Drawing input | Windows Ink routing for mouse/touch/stylus | Foundation | Touch and simultaneous-input hardware tests |
| Shapes: line/arrow/rect/rounded/ellipse/circle | Shape tools | Drawing shortcuts | Separate vector layer with live preview for line, rectangle, rounded rectangle and ellipse | Implemented | Build/startup verified; physical drag matrix pending |
| Direction/diagram shapes | Shape tools | Drawing shortcuts | 24-choice live-preview palette: curved/elbow/connector arrows, hexagon, parallelogram, process, terminator, database, cloud, callout, trainer symbols and basics | Runtime Verified | 160×252 flyout visually verified; hardware drag validation pending |
| Text annotations | Text tool | Type mode | Inline multiline editor with size, bold, italic, underline, background/border controls; Esc cancel and Ctrl+Enter commit | Implemented | Release/startup verified; alignment/font-family UI pending |
| Number/letter training markers | N/A | N/A | Automatic number or A–Z sequence, circle/rounded-square styles, reset control | Implemented | Release/startup verified; manual start-value UI pending |
| Whiteboard/blackboard/custom image | Board mode | White/black board | Instant black, white, custom-color and transparent presentation surfaces | Implemented | Release/startup verified; image and multi-board history pending |
| Spotlight | Spotlight | N/A | Dim layer with cursor-centered circular cutout | Foundation | Cursor tracking; settings pending |
| Laser pointer/trail | Laser pointer | Pointer mode | Cursor-following glowing pointer with selected ink color | Foundation | Build/startup verified; trail and fade pending |
| Cursor halo/click animation | Cursor highlight | N/A | Not implemented | Planned | Click animation and scaling tests |
| Blur/privacy pen | Privacy tool | Blur | Not implemented | Planned | Temporary/persistent blur test |
| Static zoom/pan/annotate | N/A | Static Zoom | Frozen desktop zoom centered at pointer; wheel/1.5–4× presets, drag pan, Esc/right-click/emergency exit | Implemented | Release build/startup verified; annotation-over-zoom remains limited |
| Live zoom/interact | N/A | LiveZoom | Magnification.dll compositor transform with focus, pan and reset; no screenshot polling | Implemented | Build verified; UIAccess may deny activation |
| Area zoom | N/A | Zoom region | Not implemented | Planned | Region magnification test |
| Magnifier lens | Magnifier | N/A | Not implemented | Planned | 1.5–4x lens tests |
| Full-screen/monitor capture | Screenshot | Snip | Local GDI desktop capture: full virtual desktop or monitor under cursor, clipboard output | Implemented | Release/startup verified; pixel/mixed-DPI hardware tests pending |
| Window/region capture | Screenshot | Snip | Window capture plus live crosshair region selector with dimensions and Esc | Implemented | Build verified; runtime visual QA pending |
| PNG/JPG/copy/save | Export | Snip output | Clipboard plus native PNG/JPEG save workflow | Implemented | Release/startup verified; automated image comparison pending |
| OCR region/preview | N/A | OCR snip | Windows-native offline OCR with region workflow and editable/copy preview | Runtime Verified | Synthetic “ScreenCanvas OCR 123” recognition passed locally |
| Scrolling/panorama capture | N/A | Snip scrolling | Not implemented | Planned | Browser/document stitching tests |
| Full/monitor/window/region recording | Recording | Screen recording | Not implemented | Planned | Playback, frame pacing, targeting tests |
| MP4/GIF output | Recording formats | MP4/GIF | Not implemented | Planned | Codec and compatibility tests |
| Record pause/resume/cancel/indicator | Recording controls | Recording controls | Not implemented | Planned | State-machine tests |
| Cursor and annotation compositing | Annotated capture | Draw while recording | Not implemented | Planned | Frame-level visual verification |
| Microphone/system audio | Audio capture | Audio capture | Not implemented | Planned | A/V sync and device-switch tests |
| Volume meters/mute/noise reduction | Audio controls | N/A | Not implemented | Planned | Device and signal tests |
| Webcam bubble/shapes/blur | Webcam overlay | Webcam overlay | Not implemented | Planned | Camera/resizing/compositing tests |
| Region presets/16:9 | Region record | Region record | Not implemented | Planned | Bounds and preset tests |
| Lightweight trim/append/transition/export | Editor | Trim | Not implemented | Planned | Export duration and A/V sync tests |
| Break timer/alarm/lock | Presentation timer | Break timer | Accurate deadline timer with pause/resume/reset and emergency stop | Implemented | Build/startup verified; alarm/lock pending |
| DemoType editor/playback | Demo typing | DemoType | Inline editor, 1–60 cps Unicode playback, pause/resume/reset/emergency stop | Implemented | Build/startup verified; target-app runtime QA pending |
| Mirror screen/window/region | Second screen | Demo mirror | Not implemented | Planned | Projector/secondary-display tests |
| Screen curtain | Curtain | Blank screen | Not implemented | Planned | Reveal and emergency-exit tests |
| Board/session history | Page history | N/A | Current in-memory stroke history only | Foundation | Previous/next board pending |
| Global hotkeys | Configurable shortcuts | Configurable shortcuts | Four registered global defaults | Foundation | Conflict editor and persistence pending |
| Hotkey conflict/reset UI | Hotkey editor | Settings | Registration detects conflicts at startup | Foundation | GUI conflict/reset tests pending |
| Settings search and sections | Settings | Options | Not implemented | Planned | Search, persistence, migration tests |
| Local persistence | Preferences | Options | Not implemented | Planned | Restart and corrupt-settings tests |
| Light/dark theme | Theme | N/A | Dark toolbar theme | Foundation | Light theme pending |
| Ghost/presentation mode | Hide toolbar | Tray mode | Toolbar hides while hotkeys remain active | Built | Hide, use hotkey, tray restore |
| Launch at startup | Startup option | Startup option | Not implemented | Planned | Login restart test |
| Display targeting | Active/specific/all | Monitor selection | All displays are annotated | Foundation | Selection UI pending |
| Monitor zoom/capture/record targeting | Screen controls | Monitor controls | Not implemented | Planned | Mixed-DPI monitor matrix |
| Window lifecycle recovery | Overlay recovery | Mode exit | Disposal restores desktop interaction | Foundation | Win+D, sleep, RDP, crash tests pending |
| Idle CPU/no render loop | Lightweight overlay | Lightweight utility | Event-driven; per-active-monitor lazy overlays; fade scheduler stops when empty | Runtime Verified | 0.00% settled CPU; clean startup ~206 MB working set/~138 MB private |
| High refresh/4K/ultrawide/HDR | Display quality | Display quality | No dedicated optimization yet | Planned | Hardware test matrix |
| Accessibility | Keyboard/UIA/contrast | Accessibility | Tooltips and native controls | Foundation | Narrator, keyboard and contrast audit |
| Crash recovery | Fail-safe | Escape | Normal OS teardown plus global emergency shortcut | Foundation | Forced-process-failure test |
| Installer/release packaging | Installer | Portable binary | Not implemented | Planned | Clean-machine install/uninstall |

## Required validation environments

The repository cannot honestly mark display or input hardware requirements as verified on this development machine alone. Before release, run the complete matrix on: single 1080p, single 4K, dual mixed-DPI displays, touch hardware, stylus hardware, high-refresh display, ultrawide/HDR where available, sleep/resume, display rotation, monitor hot-plug, RDP reconnect, PowerPoint, Teams, Zoom, browsers, Visual Studio, VS Code, and PDF readers.
