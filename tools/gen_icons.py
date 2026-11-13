#!/usr/bin/env python3
"""Generates the original Police Chaser icon set for every platform.

Everything here is drawn from scratch (no third-party artwork). Run from the
repository root:  python3 tools/gen_icons.py
"""
import os
import struct
import zlib

SIZE = 1024
NAVY = (9, 13, 30, 255)
NAVY_LIGHT = (20, 30, 62, 255)
BLUE = (34, 74, 176, 255)
YELLOW = (250, 205, 72, 255)
RED = (222, 52, 58, 255)
WHITE = (244, 246, 255, 255)
DARK = (18, 22, 40, 255)
GLASS = (46, 66, 104, 255)
BLACK = (8, 10, 18, 255)

canvas = [[NAVY for _ in range(SIZE)] for _ in range(SIZE)]


def put(x, y, colour):
    if 0 <= x < SIZE and 0 <= y < SIZE:
        canvas[y][x] = colour


def rect(x, y, w, h, colour):
    for yy in range(max(0, y), min(SIZE, y + h)):
        row = canvas[yy]
        for xx in range(max(0, x), min(SIZE, x + w)):
            row[xx] = colour


def rounded_rect(x, y, w, h, r, colour):
    for yy in range(max(0, y), min(SIZE, y + h)):
        row = canvas[yy]
        for xx in range(max(0, x), min(SIZE, x + w)):
            dx = max(x + r - xx, 0, xx - (x + w - 1 - r))
            dy = max(y + r - yy, 0, yy - (y + h - 1 - r))
            if dx * dx + dy * dy <= r * r:
                row[xx] = colour


def ellipse(cx, cy, rx, ry, colour):
    for yy in range(cy - ry, cy + ry + 1):
        for xx in range(cx - rx, cx + rx + 1):
            nx = (xx - cx) / rx
            ny = (yy - cy) / ry
            if nx * nx + ny * ny <= 1.0:
                put(xx, yy, colour)


def polygon(points, colour):
    n = len(points)
    for yy in range(SIZE):
        xs = []
        for i in range(n):
            x1, y1 = points[i]
            x2, y2 = points[(i + 1) % n]
            if (y1 <= yy < y2) or (y2 <= yy < y1):
                t = (yy - y1) / (y2 - y1)
                xs.append(x1 + t * (x2 - x1))
        xs.sort()
        for i in range(0, len(xs) - 1, 2):
            for xx in range(int(xs[i]), int(xs[i + 1]) + 1):
                put(xx, yy, colour)


# --- background -------------------------------------------------------------
rounded_rect(0, 0, SIZE, SIZE, 190, NAVY)
rounded_rect(34, 34, SIZE - 68, SIZE - 68, 160, NAVY_LIGHT)
for i in range(0, SIZE, 64):
    rect(0, i, SIZE, 2, (14, 20, 44, 255))

# Diagonal hazard stripes in the corners.
for i in range(-400, 500, 46):
    polygon([(0, 250 + i), (90, 250 + i), (0, 340 + i)], (24, 34, 68, 255))

# --- shield -----------------------------------------------------------------
shield = [(512, 96), (886, 214), (886, 566), (512, 928), (138, 566), (138, 214)]
polygon(shield, YELLOW)
inner = [(512, 126), (856, 236), (856, 552), (512, 888), (168, 552), (168, 236)]
polygon(inner, NAVY)

# --- light bar --------------------------------------------------------------
rounded_rect(300, 268, 424, 88, 22, DARK)
rect(316, 282, 188, 60, RED)
rect(520, 282, 188, 60, BLUE)
rect(316, 282, 188, 16, (255, 138, 138, 255))
rect(520, 282, 188, 16, (150, 200, 255, 255))
ellipse(410, 312, 150, 70, (255, 60, 60, 255)) if False else None

# --- police car (rear view) -------------------------------------------------
rect(268, 470, 488, 208, WHITE)
rounded_rect(268, 470, 488, 208, 40, WHITE)
rect(268, 556, 488, 54, BLUE)
rect(268, 610, 488, 12, YELLOW)
# windows
rounded_rect(336, 404, 352, 104, 26, DARK)
rounded_rect(352, 416, 320, 84, 20, GLASS)
# tail lights
rounded_rect(292, 496, 92, 46, 12, RED)
rounded_rect(640, 496, 92, 46, 12, RED)
rect(300, 506, 76, 14, (255, 150, 140, 255))
rect(648, 506, 76, 14, (255, 150, 140, 255))
# bumper
rect(276, 668, 472, 34, (60, 64, 78, 255))
rect(452, 656, 120, 22, (200, 202, 214, 255))
# wheels
rounded_rect(226, 596, 92, 120, 26, BLACK)
rounded_rect(706, 596, 92, 120, 26, BLACK)
# chequer band
for i in range(8):
    x = 300 + i * 56
    rect(x, 636, 28, 16, BLUE if i % 2 == 0 else WHITE)
    rect(x + 28, 636, 28, 16, WHITE if i % 2 == 0 else BLUE)

# --- chevrons at the base ---------------------------------------------------
for i, offset in enumerate((0, 74)):
    colour = YELLOW if i == 0 else (240, 240, 250, 255)
    polygon([(300, 800 + offset), (512, 720 + offset), (724, 800 + offset),
             (724, 836 + offset), (512, 756 + offset), (300, 836 + offset)], colour)


def write_png(path, pixels, width, height):
    raw = b"".join(
        b"\x00" + bytes(v for px in row for v in px) for row in pixels
    )

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data +
                struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff))

    png = (b"\x89PNG\r\n\x1a\n" +
           chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) +
           chunk(b"IDAT", zlib.compress(raw, 9)) +
           chunk(b"IEND", b""))
    with open(path, "wb") as handle:
        handle.write(png)


def write_ico(path, png_paths):
    count = len(png_paths)
    header = struct.pack("<HHH", 0, 1, count)
    directory = b""
    images = b""
    offset = 6 + 16 * count
    for png_path in png_paths:
        with open(png_path, "rb") as handle:
            data = handle.read()
        with open(png_path.replace(".png", ".png"), "rb") as handle:
            pass
        size = 0
        name = os.path.basename(png_path)
        try:
            size = int(name.split("-")[0].replace("icon", ""))
        except ValueError:
            size = 256
        directory += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(data), offset)
        images += data
        offset += len(data)
    with open(path, "wb") as handle:
        handle.write(header + directory + images)


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    assets = os.path.join(root, "assets")
    os.makedirs(assets, exist_ok=True)

    master = os.path.join(assets, "icon-1024.png")
    write_png(master, canvas, SIZE, SIZE)
    print("wrote", master)

    # ---- Android ---------------------------------------------------------
    android_drawable = os.path.join(root, "src", "PoliceChaser.Android", "Resources", "Drawable")
    os.makedirs(android_drawable, exist_ok=True)
    os.system(f'sips -z 192 192 "{master}" --out "{os.path.join(android_drawable, "Icon.png")}" >/dev/null')

    for density, size in (("mdpi", 48), ("hdpi", 72), ("xhdpi", 96), ("xxhdpi", 144), ("xxxhdpi", 192)):
        folder = os.path.join(root, "src", "PoliceChaser.Android", "Resources", "mipmap-" + density)
        os.makedirs(folder, exist_ok=True)
        os.system(f'sips -z {size} {size} "{master}" --out "{os.path.join(folder, "Icon.png")}" >/dev/null')

    # ---- iOS -------------------------------------------------------------
    icon_set = os.path.join(root, "src", "PoliceChaser.iOS", "Assets.xcassets", "AppIcon.appiconset")
    os.makedirs(icon_set, exist_ok=True)
    specs = [
        ("iphone", "20x20", 2, 40), ("iphone", "20x20", 3, 60),
        ("iphone", "29x29", 2, 58), ("iphone", "29x29", 3, 87),
        ("iphone", "40x40", 2, 80), ("iphone", "40x40", 3, 120),
        ("iphone", "60x60", 2, 120), ("iphone", "60x60", 3, 180),
        ("ipad", "20x20", 1, 20), ("ipad", "20x20", 2, 40),
        ("ipad", "29x29", 1, 29), ("ipad", "29x29", 2, 58),
        ("ipad", "40x40", 1, 40), ("ipad", "40x40", 2, 80),
        ("ipad", "76x76", 1, 76), ("ipad", "76x76", 2, 152),
        ("ipad", "83.5x83.5", 2, 167),
        ("ios-marketing", "1024x1024", 1, 1024),
    ]
    images_json = []
    for device, points, scale, pixels in specs:
        filename = f"icon-{points.replace('.', '_')}@{scale}x.png"
        target = os.path.join(icon_set, filename)
        if pixels == 1024:
            with open(master, "rb") as source, open(target, "wb") as dest:
                dest.write(source.read())
        else:
            os.system(f'sips -z {pixels} {pixels} "{master}" --out "{target}" >/dev/null')
        images_json.append({
            "size": points,
            "idiom": device,
            "filename": filename,
            "scale": f"{scale}x",
        })

    import json
    with open(os.path.join(icon_set, "Contents.json"), "w") as handle:
        json.dump({"images": images_json, "info": {"version": 1, "author": "xcode"}}, handle, indent=2)
    with open(os.path.join(root, "src", "PoliceChaser.iOS", "Assets.xcassets", "Contents.json"), "w") as handle:
        json.dump({"info": {"version": 1, "author": "xcode"}}, handle, indent=2)

    # ---- macOS icns ------------------------------------------------------
    iconset = os.path.join(assets, "AppIcon.iconset")
    os.makedirs(iconset, exist_ok=True)
    for size in (16, 32, 64, 128, 256, 512):
        os.system(f'sips -z {size} {size} "{master}" --out "{os.path.join(iconset, f"icon_{size}x{size}.png")}" >/dev/null')
        os.system(f'sips -z {size*2} {size*2} "{master}" --out "{os.path.join(iconset, f"icon_{size}x{size}@2x.png")}" >/dev/null')
    os.system(f'iconutil -c icns "{iconset}" -o "{os.path.join(assets, "AppIcon.icns")}" >/dev/null 2>&1')

    # ---- Windows ico -----------------------------------------------------
    ico_sources = []
    for size in (16, 24, 32, 48, 64, 128, 256):
        path = os.path.join(assets, f"icon{size}.png")
        os.system(f'sips -z {size} {size} "{master}" --out "{path}" >/dev/null')
        ico_sources.append(path)

    desktop = os.path.join(root, "src", "PoliceChaser.Desktop")
    os.makedirs(desktop, exist_ok=True)
    write_ico(os.path.join(desktop, "Icon.ico"), ico_sources)
    # ---- iOS loose icons (CFBundleIconFiles) -----------------------------
    ios_icons = os.path.join(root, "src", "PoliceChaser.iOS", "Icons")
    os.makedirs(ios_icons, exist_ok=True)
    for base, scale, pixels in (
        ("Icon-20", 2, 40), ("Icon-20", 3, 60),
        ("Icon-29", 2, 58), ("Icon-29", 3, 87),
        ("Icon-40", 2, 80), ("Icon-40", 3, 120),
        ("Icon-60", 2, 120), ("Icon-60", 3, 180),
        ("Icon-76", 1, 76), ("Icon-76", 2, 152),
        ("Icon-83.5", 2, 167)):
        target = os.path.join(ios_icons, f"{base}@{scale}x.png")
        os.system(f'sips -z {pixels} {pixels} "{master}" --out "{target}" >/dev/null')
    with open(master, "rb") as source, open(os.path.join(ios_icons, "Icon-1024.png"), "wb") as dest:
        dest.write(source.read())

    # ---- iOS launch screen assets ----------------------------------------
    assets_ios = os.path.join(root, "src", "PoliceChaser.iOS", "Assets.xcassets")
    launch_images = os.path.join(assets_ios, "LaunchLogo.imageset")
    launch_colors = os.path.join(assets_ios, "LaunchBackground.colorset")
    os.makedirs(launch_images, exist_ok=True)
    os.makedirs(launch_colors, exist_ok=True)
    os.system(f'sips -z 256 256 "{master}" --out "{os.path.join(launch_images, "LaunchLogo.png")}" >/dev/null')
    os.system(f'sips -z 512 512 "{master}" --out "{os.path.join(launch_images, "LaunchLogo@2x.png")}" >/dev/null')
    os.system(f'sips -z 768 768 "{master}" --out "{os.path.join(launch_images, "LaunchLogo@3x.png")}" >/dev/null')
    with open(os.path.join(launch_images, "Contents.json"), "w") as handle:
        json.dump({
            "images": [
                {"filename": "LaunchLogo.png", "idiom": "universal", "scale": "1x"},
                {"filename": "LaunchLogo@2x.png", "idiom": "universal", "scale": "2x"},
                {"filename": "LaunchLogo@3x.png", "idiom": "universal", "scale": "3x"},
            ],
            "info": {"version": 1, "author": "xcode"},
        }, handle, indent=2)
    with open(os.path.join(launch_colors, "Contents.json"), "w") as handle:
        json.dump({
            "colors": [{
                "color": {
                    "color-space": "srgb",
                    "components": {"alpha": "1.000", "blue": "0.118", "green": "0.051", "red": "0.035"},
                },
                "idiom": "universal",
            }],
            "info": {"version": 1, "author": "xcode"},
        }, handle, indent=2)

    # ---- Windows store assets -------------------------------------------
    windows_assets = os.path.join(root, "assets")
    for name, pixels in (("StoreLogo", 50), ("Square44x44Logo", 44), ("Square150x150Logo", 150)):
        os.system(f'sips -z {pixels} {pixels} "{master}" --out "{os.path.join(windows_assets, name + ".png")}" >/dev/null')
    os.system(f'sips -z 48 48 "{master}" --out "{os.path.join(windows_assets, "Square44x44Logo.targetsize-48.png")}" >/dev/null')

    print("icons done")


if __name__ == "__main__":
    main()
