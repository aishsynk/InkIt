# ScreenCanvas decisions

## 2026-09-03 — Connected palette geometry

- Decision: use one toolbar window with dynamic `[Level 2][Level 1][Main]` or `[Main][Level 1][Level 2]` columns.
- Rationale: removes detached-card presentation, eliminates hidden host width, and keeps the primary rail anchored during expansion.

## 2026-09-03 — Dimensional token system

- Decision: centralize palette, toolbar, menu, slider, swatch, and icon dimensions in application resources.
- Rationale: prevents divergent magic numbers and makes runtime geometry auditable.

## 2026-09-03 — Production packaging

- Decision: publish a win-x64 package only after the Release build and runtime validation pass.
- Rationale: production must receive the same validated package produced by the staged workflow.
- Superseded (2026-09-24): this entry originally said "framework-dependent". The actual and intended mode is self-contained. See "Self-contained publish mode" below.

## 2026-09-03 — Performance gate

- Decision: elevated idle CPU is an unresolved validation issue, not a passing metric.
- Rationale: the latest 7.34% sample conflicts with earlier 0.00% measurements and requires profiling before production approval.

## 2026-09-03 — Single attached palette replaces cascade

- Decision: retire the visible Mark → Write two-level navigation and render family categories, tool tiles, and inline properties inside one 280 DIP content-driven palette.
- Rationale: trainer workflows need one family click plus one tool/category selection, without full-height navigation columns or horizontal cascade.

## 2026-09-03 — Official Microsoft Fluent System Icons adoption

- Decision: retire all amateur, scraped, and hand-drawn wireframe icons; adopt official `microsoft/fluentui-system-icons` 24px Regular and Filled vector geometries rendered with solid fills.
- Rationale: eliminates inconsistent stroke weights, double-line wireframe artifacts, and amateur aesthetics, establishing an authentic Microsoft Fluent design language across all tools and families.

## 2026-09-03 — Sticky tools by default and direct tool switching

- Decision: drawing tools (Pen, Highlighter, Shapes, Text, Laser, Eraser) remain sticky by default after each object commit; tool switches occur directly with 1 click without intermediate Cursor or Esc; each family remembers its last-used subtool; and each tool retains its own distinct style profile (color, width, opacity) via `ToolProfileStore`.
- Rationale: mature corporate presentation tools must allow uninterrupted drawing sequences (e.g. consecutive arrows or text labels) and rapid switching between tools without redundant menu navigation or style bleeding.

## 2026-09-03 — Global Esc invariant: tool termination to Cursor

- Decision: Esc always deactivates the current active tool, cancels in-flight previews, keeps already-committed annotations, and returns the application to Cursor mode with desktop click-through enabled. Dismissing palettes without tool deactivation is handled by clicking the desktop or using the palette header's [✕] button.
- Rationale: preserves the universal expectation that Esc returns control to the operating system/desktop while ensuring in-progress mistakes can be safely aborted without erasing existing annotations.

## 2026-09-03 — Toolbar input priority and UI exclusion zone

- Decision: the ScreenCanvas toolbar, open palettes, and dialogs form an authoritative UI exclusion zone (`IUiExclusionRegionService`). When the pointer is over this zone, drawing overlays return `HTTRANSPARENT` to `WM_NCHITTEST` so that Windows routes all hover, cursor, and click events directly to the UI, enabling instant 1-click tool switching without Esc or Cursor selection.
- Rationale: users must be able to switch tools or configure colors/widths without friction or intermediate unlock steps, while active tools remain sticky outside the UI bounds.

## 2026-09-03 — Apple-grade UI/UX design system and fluid physics animations

- Decision: elevate ScreenCanvas UI into an Apple-grade presentation standard featuring translucent frosted glass materials (`#E61C1C20`), specular gradient rim highlights (`GlassSpecularBrush`), ambient occlusion drop shadows (`BlurRadius=28–32`), 16 DIP continuous squircle curves, spring scale physics micro-interactions on button hover/press, Dynamic Island floating pill collapse (48x112 DIP) with 180° rotating chevron, live active color orb, circular enamel bead swatches with concentric ring selection, and Keynote-grade dual-core laser bloom.
- Rationale: provides a premium, distraction-free corporate presentation aesthetic with delightful tactile feedback and zero idle CPU overhead (0.00%).

## 2026-09-03 — Independent floating glass islands and ultra-thin overlay scrollbar

- Decision: remove all static full-height dividers between the main rail and flyout panels in favor of independent floating islands separated by an 8 DIP gap. Replace WPF's standard 18 DIP gray scrollbar with a 4 DIP floating translucent overlay track that consumes zero content space. Declare button scale transforms directly on local template borders rather than frozen style setters to ensure hardware-accelerated 60fps micro-animations.
- Rationale: eliminates visual artifacts (such as boundary lines in transparent regions) and bulky legacy scrollbars, matching modern macOS and visionOS aesthetics.

## 2026-09-03 — Dedicated contextual progressive-disclosure inspectors & floating capsule

- Decision: retire monolithic multi-family popups; replace with dedicated contextual progressive disclosure inspectors (Pen, Highlighter, Shapes, Text, Laser, Screen, More) anchored adjacent to the primary 60px floating vertical capsule. Present real rendered stroke thickness cards (`Thin`, `Medium`, `Thick`), segmented mode pills, 7-color Apple swatches with concentric outer ring selection, and progressive links (`More Pens…`, `Effects >`).
- Rationale: provides professional clarity, restraint, and hierarchy inspired by Apple PencilKit and creative tools, eliminating cognitive overload while keeping all advanced features easily discoverable.

## 2026-09-04 — Ultra-compact precision utility strip redesign & rejection of oversized capsule

- Decision: replace the 60 DIP mobile capsule with an ultra-compact, precision desktop floating utility strip: 40.0 DIP width (max 44 DIP limit), 32 DIP button size, 20 DIP Fluent optical icon size, 2 DIP vertical button rhythm, 8 DIP corner radius, near-invisible borders (5% black / 10% white), and subtle 8px shadow. Completely eliminate the top drag pill in favor of background dragging. Remove spring scaling animations and bounce physics. Replaced Dynamic Island with an ultra-compact 40×38 DIP floating tile in collapsed mode. Contextual inspectors reduced to 230 DIP width with progressive disclosure.
- Rationale: users rejected oversized mobile capsule styling and SaaS pill cards. Desktop creative annotation utilities (Photoshop, Figma, mature drawing utilities) demand minimal chrome, almost invisible containers, and tight spatial discipline so the user focuses on their screen content and tools rather than the toolbar container.

## 2026-09-04 — Trainer-First Three-Tier Rebuild & 100+ Capability Architecture

- Decision: implement the three-tier product architecture:
  1. Core Toolbar: Exactly 13 compact controls (~42 px high horizontal, ~42 px wide vertical), toggleable via `Ctrl+Shift+O` or More menu. Single color chip orb represents active color.
  2. Contextual Options: Attached progressive disclosure inspectors with 6 core teaching colors (`#2563EB`, `#E5484D`, `#16A34A`, `#F2B705`, `#7C3AED`, `#0F172A`), visual stroke cards, and step markers.
  3. Advanced Capability Centre: Full catalog mapping 100+ capabilities across 8 categories (Annotate, Shapes, Present, Screen, Privacy, Board, Record, Tools), Command Palette (`Ctrl+Shift+P`), and Radial HUD Quick Menu.
## 2026-09-04 — Flush Twin-Dock Alignment, Persistent Annotation Mode & Docked More Sub-Menu

- Decision:
  1. Flush Height & Top Alignment (Mandatory Invariant): In vertical mode, all sub-menus (`InspectorWindow`) lock their vertical geometry to strictly match the main toolbar: `Top = _owner.Top;` and `Height = ownerH;` (557 DIP), creating a 100% flush, unified dual-dock presenter rail separated by a 4 DIP gap. The close button (`✕`) is pinned cleanly in the bottom dock (`BottomHost`) aligned with the bottom of the toolbar.
  2. Continuous Annotation Persistence: Sub-menus must never close automatically when the presenter starts drawing (`InteractionStarted`). Drawing arrows, lines, rectangles, shapes, or ink strokes leaves the sub-menu open so trainers can draw multiple items consecutively or switch properties without reopening the palette. Sub-menus only dismiss via explicit `Esc`, clicking the tool button again (toggle), clicking `Cursor (V)`, or clicking `✕`.
  3. Docked More Sub-Menu: The `More` button (`...`) no longer opens detached dialogs or raw tooltips. It opens an adjacent docked inspector containing:
     - Board Modes: Screen Canvas (transparent), Whiteboard (pure white), and Blackboard (dark chalkboard) with live active state indicators.
     - Presenter Utilities: Layout orientation toggle (↕ / ↔), Command Palette (`Ctrl+Shift+P`), and Radial Gestures Menu.
     - Teaching Tools: Break countdown timer and Screen Freeze frame.
     - Advanced Tools: Full Capability Centre dialog opener, Settings dialog opener, and distinct red-accented Exit InkIt button.
  4. Active Anchor Highlighting: The active tool button on the main toolbar remains highlighted (`SelectedBrush`) while its sub-menu is open, establishing a clear visual link between the primary tool and its docked inspector.
  5. ToolTip Non-Client Artifact Elimination: Set `HasDropShadow="False"` and `Background="Transparent"` on WPF ToolTips to eliminate rectangular gray corner non-client popup artifacts.
- Rationale: Fully satisfies the presenter requirement for uninterrupted live teaching workflows and unified aesthetic cohesion across the application.


## 2026-09-19 — Feature reduction: remove Eyedropper, Blur/Pixelate, Recording and niche diagram shapes

- Decision: remove the Eyedropper, Blur/Pixelate privacy tool, and GIF screen recording, plus 18 diagram-oriented `ShapeKind`s (Database, Cloud, Callout, Connector, Star, Check/Cross, Triangle, Hexagon, etc.). The Shapes palette keeps Line, Arrow, Double Arrow, Rectangle, Rounded Rectangle, Ellipse and Step Marker.
- Rationale: InkIt is a lightweight presenter annotation tool. These features added UI weight, dead code paths and maintenance cost without fitting the core teaching workflow. (Recorded retroactively on 2026-09-24. The removal itself happened 2026-09-19.)
- Impact: the 2026-09-24 stabilization removed the remaining traces: the dead `record.screen` command and the `Record` Capability Centre category, the Blur/Eyedropper settings sections, and a stale "connector" preset reference.

## 2026-09-24 — Self-contained publish mode

- Decision: production packages are published **self-contained** for win-x64 (`dotnet publish ... -r win-x64 --self-contained true -o artifacts/publish/win-x64`), and the Inno Setup installer packages that folder.
- Rationale: users must not need a .NET 8 runtime installed. This matches the actual publish output and the installer script. It replaces the stale "framework-dependent" wording in the 2026-09-03 packaging entry.

## 2026-09-24 — Esc is single-press tool termination, even with a palette open

- Decision: the global Esc hotkey always calls `EndCurrentTool`, which closes the palette and returns to Cursor with click-through. The earlier "first Esc closes the palette only" branch in `App.xaml.cs` was removed.
- Rationale: that branch contradicted the 2026-09-03 "Global Esc invariant" and made Esc from Shapes/Laser (whose palettes open on selection) need two presses. Palettes can still be dismissed without ending the tool via ✕ or by clicking the tool button again.

## 2026-09-25 — Adopt the InkIt workbench design as the WPF UI

- Decision: port the `inkit-screencanvas-workbench` React design into the WPF app: toolbar, inspectors, overlay features, Capability Centre, Command Palette, Radial menu, Settings, Capture preview, HUDs and command registry.
- Scope choices (user, 2026-09-25):
  1. Skip the design's Windows simulation (title bar, taskbar, Quick Settings, 8-phase dossier). A desktop app must not fake the real OS shell.
  2. Keep Blur and Recording removed, per the 2026-09-19 decision. The Diamond shape returns.
  3. Dark slate is the default theme. A matching light variant is selectable (Dark/Light/System).
  4. Existing extras the design lacks (Redo, Screenshot, More, Collapse) become customizable toolbar items that are hidden by default.
- Rationale: the user asked for the full design with nothing missing. These four choices resolve conflicts between the design and a real desktop product.
- Impact: supersedes the 2026-09-04 layout and styling decisions (compact light strip, flush twin-dock inspectors, docked More sub-menu). The earlier behavioural invariants still hold: Esc ends the active tool, sticky tools, direct switching, and the UI exclusion zone.

## 2026-09-25 — Lucide icons replace Fluent System Icons

- Decision: UI icons are Lucide (lucide-react 0.546.0, ISC), generated into XAML by `tools/lucide/generate_lucide_xaml.py` and drawn as 2px round strokes by `LucideIcon`.
- Rationale: the design uses Lucide throughout, and visual fidelity requires the same glyphs. Generating the XAML from the locally installed package gives exact geometry without hand-tracing.
- Alternatives: keep Fluent icons (visibly different from the design); download lucide-static (unnecessary, since the exact version is on disk).
- Impact: supersedes the 2026-09-03 Fluent adoption decision for new UI. `FluentIconResources.xaml` remains only for legacy windows.

## 2026-09-25 — Tool click semantics follow the design

- Decision: clicking an inactive tool activates it. Pen and Shape also open their inspector; other tools close any open inspector. Clicking the already-active tool toggles its inspector. Board, Colour and More toggle their inspectors.
- Rationale: this matches the design's `handleToolClick`, and keeps one-click direct switching.
- Impact: the Highlighter, Text, Laser, Spotlight and Zoom inspectors open on a second click rather than immediately.

## 2026-09-25 — Break timer becomes a floating widget

- Decision: the break timer is a non-activating top-right widget (MM:SS, pause/resume, reset to 5 min, close), replacing the full-screen countdown.
- Rationale: design parity. The presenter can keep working while the timer runs.

## 2026-09-25 — Trainer-first usability (supersedes parts of the design port)

- Decision: (1) Zoom defaults to "zoom into an area": drag a box, the area is frozen and enlarged on its monitor, stays put, and can be drawn on; follow-the-mouse live zoom stays available as an option in the Zoom panel (`ToolSettings.ZoomFollowsMouse`). (2) The Screenshot button is visible by default next to Zoom (settings migration v3), and a screenshot is copied to the clipboard immediately. (3) On-screen text uses plain language instead of the design's developer wording (file names, API names, sizes).
- Rationale: the user is a corporate trainer. They asked to zoom a chosen part without the view following the mouse, for the area screenshot that the old version had, and for a layman's view of the app. This request is newer than the "copy the design, missing nothing" instruction and overrides it where the two conflict.
- Alternatives: region zoom through the Magnification API (rejected: the toolbar leaves the view and the cursor moves at magnified speed, which makes drawing impractical); keeping Screenshot hidden (rejected: the user said it was missing).
- Impact: the 2026-09-25 scope choice 4 no longer applies to Screenshot. Drawings made while zoomed are discarded on exit and earlier annotations come back. Leaving a zoom returns to Cursor if the zoom had switched the pen on.
