"""Generate UI/Icons/LucideIcons.xaml from a local lucide-react package.

Usage:
    python tools/lucide/generate_lucide_xaml.py <lucide-react dir> [design src dir]

Every icon imported from 'lucide-react' in the design source is included, plus
EXTRA_ICONS used only by WPF-specific surfaces. Each icon becomes a
<Geometry x:Key="Lucide.Name"> in the 24x24 Lucide coordinate space; the
LucideIcon control strokes it at width 2 with round caps, like lucide-react.
"""
import os
import re
import sys

EXTRA_ICONS = [
    "Redo2", "Camera", "MoreVertical", "ChevronDown", "ChevronUp", "ChevronLeft", "ChevronRight",
    "Palette", "Pause", "Play", "RotateCcw", "Copy", "Download", "Timer", "Keyboard", "Terminal",
    "Folder", "Shield", "Cpu", "Layout", "Settings", "AlertCircle", "Star", "Compass", "PieChart",
    "Power", "Snowflake", "ScanText", "Bold", "Italic", "Underline", "Monitor", "Crop", "Eye", "EyeOff",
    "MousePointerClick", "Check", "X", "Minus", "Plus", "Grid", "Magnet", "Crosshair", "Sparkles",
    "Wand2", "Layers", "Sliders", "GripVertical", "RotateCw", "Search", "Trash2", "Undo2", "Minimize2",
    "PaintBucket", "Type", "ListOrdered", "Flame", "SunMedium", "ZoomIn", "ZoomOut", "BoxSelect", "Pen",
    "Highlighter", "Eraser", "Shapes", "MousePointer", "Square", "Circle", "Diamond", "ArrowRight",
    "MoveHorizontal", "SquareDot", "ScanLine", "PanelTopClose", "FileText", "PauseCircle", "ShieldAlert",
    "Clock", "Zap", "Info", "Image", "Presentation", "Hash", "CaseSensitive", "LogOut",
    # Refined toolbar set (2026-09-26)
    "MousePointer2", "PenLine", "Flashlight", "CircleDot", "SquareDashedMousePointer", "LayoutGrid",
    "WandSparkles", "SlidersHorizontal", "ArrowLeftRight", "ArrowUpDown",
]

ICON_IMPORT = re.compile(r"import\s*\{([^}]*)\}\s*from\s*'lucide-react'", re.S)
EXPORT_LINE = re.compile(r"export \{([^}]*)\} from '\./icons/([a-z0-9-]+)\.js'")
NODE = re.compile(r'\[\s*"(\w+)",\s*\{([^}]*)\}\s*\]')
ATTR = re.compile(r'(\w+):\s*"([^"]*)"')
NUMBER = re.compile(r"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")


def design_icons(src_dir):
    names = set()
    for root, _, files in os.walk(src_dir):
        for file in files:
            if file.endswith((".tsx", ".ts")):
                with open(os.path.join(root, file), encoding="utf-8") as f:
                    for block in ICON_IMPORT.findall(f.read()):
                        names.update(n.strip() for n in block.split(",") if n.strip())
    return names


def alias_map(lucide_dir):
    mapping = {}
    with open(os.path.join(lucide_dir, "dist", "esm", "lucide-react.js"), encoding="utf-8") as f:
        for exports, file in EXPORT_LINE.findall(f.read()):
            for part in exports.split(","):
                part = part.strip()
                if part.startswith("default as "):
                    mapping[part[len("default as "):]] = file
    return mapping


def fmt(v):
    v = float(v)
    return ("%.4f" % v).rstrip("0").rstrip(".") if v != int(v) else str(int(v))


def tokenize_path(d):
    """Normalise SVG path data into explicitly separated WPF path markup."""
    cmd = None
    # Arc flags may be written without separators ("a1 1 0 011 1"); re-split them.
    expanded = []
    for t in re.split(r"([MmLlHhVvCcSsQqTtAaZz])", d):
        if not t:
            continue
        if re.fullmatch(r"[MmLlHhVvCcSsQqTtAaZz]", t):
            expanded.append(t)
            cmd = t
            continue
        nums = NUMBER.findall(t)
        if cmd in ("a", "A"):
            fixed = []
            # 7 args per arc; flags (index 3,4) are single 0/1 digits possibly glued.
            raw = t.replace(",", " ")
            pos = 0
            idx = 0
            while pos < len(raw):
                ch = raw[pos]
                if ch.isspace():
                    pos += 1
                    continue
                k = idx % 7
                if k in (3, 4):
                    fixed.append(ch)
                    pos += 1
                else:
                    m = NUMBER.match(raw, pos)
                    fixed.append(m.group(0))
                    pos = m.end()
                idx += 1
            nums = fixed
        expanded.extend(fmt(n) if n not in ("0", "1") or cmd not in ("a", "A") else n for n in nums)
    # SVG treats a path's leading "m" as absolute (following pairs stay relative lineto). Each
    # lucide <path> is its own element, so make that explicit before paths are concatenated.
    if expanded and expanded[0] == "m":
        rest = expanded[3:]
        expanded = ["M", expanded[1], expanded[2]]
        if rest and not re.fullmatch(r"[MmLlHhVvCcSsQqTtAaZz]", rest[0]):
            expanded.append("l")
        expanded.extend(rest)
    return " ".join(expanded)


def circle(cx, cy, r):
    cx, cy, r = float(cx), float(cy), float(r)
    return f"M {fmt(cx - r)} {fmt(cy)} A {fmt(r)} {fmt(r)} 0 1 0 {fmt(cx + r)} {fmt(cy)} A {fmt(r)} {fmt(r)} 0 1 0 {fmt(cx - r)} {fmt(cy)} Z"


def ellipse(cx, cy, rx, ry):
    cx, cy, rx, ry = map(float, (cx, cy, rx, ry))
    return f"M {fmt(cx - rx)} {fmt(cy)} A {fmt(rx)} {fmt(ry)} 0 1 0 {fmt(cx + rx)} {fmt(cy)} A {fmt(rx)} {fmt(ry)} 0 1 0 {fmt(cx - rx)} {fmt(cy)} Z"


def rect(a):
    x, y = float(a.get("x", 0)), float(a.get("y", 0))
    w, h = float(a["width"]), float(a["height"])
    rx = float(a.get("rx", a.get("ry", 0)))
    ry = float(a.get("ry", rx))
    if rx <= 0:
        return f"M {fmt(x)} {fmt(y)} H {fmt(x + w)} V {fmt(y + h)} H {fmt(x)} Z"
    return (f"M {fmt(x + rx)} {fmt(y)} H {fmt(x + w - rx)} A {fmt(rx)} {fmt(ry)} 0 0 1 {fmt(x + w)} {fmt(y + ry)} "
            f"V {fmt(y + h - ry)} A {fmt(rx)} {fmt(ry)} 0 0 1 {fmt(x + w - rx)} {fmt(y + h)} "
            f"H {fmt(x + rx)} A {fmt(rx)} {fmt(ry)} 0 0 1 {fmt(x)} {fmt(y + h - ry)} "
            f"V {fmt(y + ry)} A {fmt(rx)} {fmt(ry)} 0 0 1 {fmt(x + rx)} {fmt(y)} Z")


def points(p, closed):
    nums = NUMBER.findall(p)
    pairs = [f"{fmt(nums[i])} {fmt(nums[i + 1])}" for i in range(0, len(nums) - 1, 2)]
    return "M " + pairs[0] + "".join(" L " + q for q in pairs[1:]) + (" Z" if closed else "")


def icon_geometry(path):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    body = text[text.index("__iconNode = ["):text.index("];")]
    figures = []
    for tag, attrs in NODE.findall(body):
        a = dict(ATTR.findall(attrs))
        if tag == "path":
            figures.append(tokenize_path(a["d"]))
        elif tag == "circle":
            figures.append(circle(a["cx"], a["cy"], a["r"]))
        elif tag == "ellipse":
            figures.append(ellipse(a["cx"], a["cy"], a["rx"], a["ry"]))
        elif tag == "rect":
            figures.append(rect(a))
        elif tag == "line":
            figures.append(f"M {fmt(a['x1'])} {fmt(a['y1'])} L {fmt(a['x2'])} {fmt(a['y2'])}")
        elif tag == "polyline":
            figures.append(points(a["points"], False))
        elif tag == "polygon":
            figures.append(points(a["points"], True))
        else:
            raise ValueError(f"Unsupported element {tag} in {path}")
    return " ".join(figures)


def main():
    lucide_dir = sys.argv[1]
    design_src = sys.argv[2] if len(sys.argv) > 2 else None
    here = os.path.dirname(os.path.abspath(__file__))
    repo = os.path.abspath(os.path.join(here, "..", ".."))
    out_dir = os.path.join(repo, "src", "ScreenCanvas", "UI", "Icons")

    names = set(EXTRA_ICONS)
    if design_src:
        names |= design_icons(design_src)
    aliases = alias_map(lucide_dir)
    missing = sorted(n for n in names if n not in aliases)
    if missing:
        raise SystemExit("Unknown lucide icons: " + ", ".join(missing))

    lines = [
        '<!-- Generated by tools/lucide/generate_lucide_xaml.py from lucide-react v0.546.0 (ISC, see LUCIDE-LICENSE.txt). Do not edit by hand. -->',
        '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
    ]
    for name in sorted(names):
        geometry = icon_geometry(os.path.join(lucide_dir, "dist", "esm", "icons", aliases[name] + ".js"))
        lines.append(f'  <Geometry x:Key="Lucide.{name}">{geometry}</Geometry>')
    lines.append("</ResourceDictionary>")
    with open(os.path.join(out_dir, "LucideIcons.xaml"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    with open(os.path.join(lucide_dir, "LICENSE"), encoding="utf-8") as src, \
            open(os.path.join(out_dir, "LUCIDE-LICENSE.txt"), "w", encoding="utf-8", newline="\n") as dst:
        dst.write("Lucide icons (lucide-react v0.546.0) are used under the following license:\n\n" + src.read())
    print(f"Wrote {len(names)} icons")


if __name__ == "__main__":
    main()
