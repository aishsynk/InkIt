# InkIt feature matrix

Current as of 0.0.0.2 (September 2026). Status legend:

- **Live-verified**: exercised in the running app on a real screen.
- **Tested**: covered by `tests/InkIt.Tests` or an offline test harness (no screen input needed).
- **Built**: compiles and is wired up; needs a hands-on check (hardware or user input) before it counts as verified.

## Drawing

| Feature | How to use | Status |
|---|---|---|
| Pen (15 styles), highlighter, eraser | Toolbar, P / H / E | Live-verified |
| Colours and thickness | Colour button; keys 1-6 and [ ] while drawing | Colours live-verified; keys Built |
| Smart shapes (circle, box, triangle, diamond, line, arrow; V after a line = arrow) | Pen, on by default; switch in the Pen panel; Undo once restores freehand | Tested (339/340 noisy strokes) |
| Connectors snap onto shape edges | Draw a line/arrow ending near a shape | Built |
| Shapes tool, text, sticky-note text | S, T; Text panel > Sticky note | Shapes/text live-verified; sticky note Built |
| Step numbers and stamps (tick, cross, ?, !, star) | N; Step numbers panel | Numbers live-verified; stamps Built |
| Select, move, recolour, duplicate, delete | V | Live-verified |
| Handwriting to text | Select handwriting > To text | Recognizer tested; UI Built |
| Undo / redo, two-finger tap undo, three-finger tap redo | Ctrl+Shift+Z; touch | Undo live-verified; taps Built |
| Tool wheel | Right-click or pen side button while drawing | Built |

## Presenting

| Feature | How to use | Status |
|---|---|---|
| Zoom into an area (stays put) / follow-the-mouse zoom | Ctrl+Shift+5; Zoom panel | Live-verified |
| Screenshot of an area, copied to the clipboard | Ctrl+Shift+4 | Live-verified |
| Laser pointer, spotlight | Toolbar | Live-verified |
| Focus box (dim all but an area, apps stay live) | Spotlight panel, Search | Built |
| Whiteboard / blackboard / grid | Whiteboard button | Live-verified |
| Pages of drawings, PDF export, save/open (.inkit) | Whiteboard panel, Page Up/Down, Ctrl+S/O | Tested (file round trip, PDF opened by Windows) |
| Drawings follow PowerPoint slides | Automatic during a slide show (Settings) | Built (needs PowerPoint) |
| Lesson recording to MP4 (screen or area, drawings, pointer, microphone, pause) | Record button | Video tested offline; microphone Built |
| Show on a second screen (screen, area or one window) | More > Show on Second Screen | Built (needs two monitors) |
| Break timer, freeze screen, curtain, blank screen, key display, click ripples, live typing demo | More panel, Search | Built / earlier QA |

## Everyday and expert

| Feature | How to use | Status |
|---|---|---|
| Grouped toolbar, presets, customise, drag to reorder | Toolbar | Live-verified |
| Dark / light theme | Settings | Live-verified |
| First-run tour | Automatic once; More > Quick Tour | Built |
| Search every feature, all-features browser | Ctrl+K, F10 | Live-verified |
| Global shortcut for any feature | Settings > Keyboard Shortcuts | Built |
| inkit:// links (Stream Deck, scripts), .inkit double-click, single instance | e.g. inkit://zoom | Built |
| Start with Windows | Settings > About & Feedback | Built |
| Send feedback / report a problem (version prefilled) | Tray, More, Settings | Built |
| Update check (daily, can be turned off) | Automatic; tray > Check for updates | Built |
| Crash reports | %LOCALAPPDATA%\InkIt\logs | Built |
| Panic key | Alt+Shift+X | Live-verified |

## Not included

- Webcam bubble (code exists, not wired; ask if wanted), blur/pixelate privacy pen, system-audio (loopback) recording, recording editor.
- Code signing: supported by the release script once a certificate is available.
