#!/usr/bin/env python3
"""Bakes the Police Chaser app icon and the launch/splash artwork.

Everything is drawn from scratch with Pillow - no third-party artwork - from one
procedural design, then rasterised per platform and size:

    assets/icon-1024.png                full-bleed master (store art, docs)
    assets/AppIcon.icns                 macOS bundle icon (padded, rounded)
    assets/*Logo*.png, SplashScreen*.png   Windows MSIX tiles and splash
    src/PoliceChaser.Desktop/Icon.ico   Windows executable icon
    src/PoliceChaser.iOS/...            app icon sets (light/dark/tinted) + launch lockup
    src/PoliceChaser.Android/...        launcher icons, adaptive icon, splash logo

The icon is a full-bleed square: iOS, Android and Windows apply their own mask,
so only the macOS/window-tile variant carries the rounded shape and its shadow.
iOS 18 dark and tinted appearance variants come from the same design with their
own palettes; the tinted one is greyscale so the system can tint it.

    python3 -m pip install pillow
    python3 tools/gen_icons.py
"""

import io
import json
import os
import struct
import subprocess
import sys

try:
    from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont
except ImportError:  # pragma: no cover - developer tooling only
    sys.exit("Pillow is required: python3 -m pip install pillow")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
FONT_PATH = os.path.join(ROOT, "src", "PoliceChaser.Core", "Graphics", "Fonts",
                         "ChakraPetch-SemiBold.ttf")

DESIGN = 1024           # logical icon canvas; every coordinate below is in this space
SUPERSAMPLE_MAX = 2048  # cap so a 1024 master is rendered at 2x rather than 4x


class Palette:
    """Colours of the default (light) icon."""

    navy_top = (9, 14, 34)
    navy_bottom = (21, 33, 68)
    shield_top = (15, 23, 50)
    shield_bottom = (7, 11, 26)
    gold = (247, 201, 72)
    gold_light = (255, 226, 132)
    gold_dark = (176, 132, 30)
    blue = (43, 91, 215)
    blue_deep = (24, 55, 142)
    blue_light = (128, 174, 255)
    red = (226, 58, 69)
    red_light = (255, 148, 148)
    white = (244, 246, 255)
    glass = (43, 59, 94)
    glass_light = (88, 116, 162)
    dark = (13, 19, 34)
    housing = (28, 37, 58)
    tyre = (10, 12, 20)
    tyre_hub = (38, 42, 58)
    bumper = (52, 58, 78)
    plate = (198, 202, 214)
    dim = (150, 168, 205)


class DarkPalette(Palette):
    """iOS 18 dark appearance: the same livery, lit from the siren only."""

    navy_top = (3, 5, 14)
    navy_bottom = (10, 16, 34)
    shield_top = (10, 15, 32)
    shield_bottom = (3, 5, 14)
    gold = (214, 168, 58)
    gold_light = (255, 216, 124)
    gold_dark = (138, 100, 24)
    blue = (36, 76, 194)
    blue_deep = (18, 42, 112)
    blue_light = (108, 150, 238)
    red = (198, 44, 54)
    red_light = (240, 116, 116)
    white = (222, 226, 240)
    glass = (33, 46, 74)
    glass_light = (74, 98, 140)
    dark = (8, 11, 20)
    housing = (18, 24, 38)
    tyre = (5, 6, 11)
    tyre_hub = (26, 29, 40)
    bumper = (42, 46, 62)
    plate = (176, 180, 194)
    dim = (140, 152, 178)


class TintedPalette(Palette):
    """iOS 18 tinted appearance: greyscale, so the system tint reads cleanly."""

    navy_top = (26, 26, 28)
    navy_bottom = (44, 44, 47)
    shield_top = (16, 16, 18)
    shield_bottom = (8, 8, 9)
    gold = (170, 170, 174)
    gold_light = (214, 214, 218)
    gold_dark = (108, 108, 112)
    blue = (120, 120, 124)
    blue_deep = (88, 88, 92)
    blue_light = (172, 172, 178)
    red = (200, 200, 204)
    red_light = (232, 232, 236)
    white = (238, 238, 242)
    glass = (66, 66, 70)
    glass_light = (100, 100, 104)
    dark = (18, 18, 20)
    housing = (34, 34, 37)
    tyre = (12, 12, 13)
    tyre_hub = (60, 60, 64)
    bumper = (94, 94, 98)
    plate = (200, 200, 204)
    dim = (150, 150, 154)


def lerp(first, second, t):
    return tuple(round(first[i] + (second[i] - first[i]) * t) for i in range(3))


def a(colour, alpha):
    return (colour[0], colour[1], colour[2], alpha)


def opaque(colour):
    return a(colour, 255)


def supersample(px):
    return max(192, min(SUPERSAMPLE_MAX, px * 4))


class Painter:
    """Draws the 1024-unit icon design onto a square RGBA canvas."""

    def __init__(self, size):
        self.size = size
        self.k = size / DESIGN
        self.img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def s(self, value):
        return value * self.k

    def rect(self, x0, y0, x1, y1, colour):
        self.d.rectangle([self.s(x0), self.s(y0), self.s(x1), self.s(y1)], fill=colour)

    def round_rect(self, x0, y0, x1, y1, radius, colour):
        self.d.rounded_rectangle([self.s(x0), self.s(y0), self.s(x1), self.s(y1)],
                                 radius=self.s(radius), fill=colour)

    def ellipse(self, x0, y0, x1, y1, colour):
        self.d.ellipse([self.s(x0), self.s(y0), self.s(x1), self.s(y1)], fill=colour)

    def polygon(self, points, colour):
        self.d.polygon([(self.s(x), self.s(y)) for x, y in points], fill=colour)

    def outline(self, points, colour, width):
        path = [(self.s(x), self.s(y)) for x, y in points]
        self.d.line(path + [path[0]], fill=colour, width=max(1, round(self.s(width))),
                    joint="curve")


def glow(painter, cx, cy, rx, ry, colour, blur):
    layer = Image.new("RGBA", (painter.size, painter.size), (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse(
        [painter.s(cx - rx), painter.s(cy - ry), painter.s(cx + rx), painter.s(cy + ry)],
        fill=colour)
    layer = layer.filter(ImageFilter.GaussianBlur(max(1.0, painter.s(blur))))
    painter.img.alpha_composite(layer)


def gradient_polygon(painter, points, top, bottom):
    mask = Image.new("L", (painter.size, painter.size), 0)
    ImageDraw.Draw(mask).polygon(
        [(painter.s(x), painter.s(y)) for x, y in points], fill=255)
    ys = [painter.s(y) for _, y in points]
    y0 = int(min(ys))
    y1 = int(max(ys))
    gradient = Image.new("RGBA", (painter.size, painter.size))
    pen = ImageDraw.Draw(gradient)
    for y in range(y0, min(y1 + 1, painter.size)):
        t = (y - y0) / max(1, y1 - y0)
        pen.line([(0, y), (painter.size, y)], fill=lerp(top, bottom, t))
    painter.img.paste(gradient, (0, 0), mask)


def draw_backdrop(painter, palette):
    pen = painter.d

    # Night-sky gradient.
    for y in range(painter.size):
        t = y / (painter.size - 1)
        pen.line([(0, y), (painter.size, y)],
                 fill=lerp(palette.navy_top, palette.navy_bottom, t))

    # Faint hazard stripes.
    stripes = Image.new("RGBA", (painter.size, painter.size), (0, 0, 0, 0))
    spen = ImageDraw.Draw(stripes)
    step = max(8, round(painter.size / 11))
    width = max(2, round(painter.size / 46))
    for x in range(-painter.size, painter.size * 2, step):
        spen.line([(x, 0), (x + painter.size, painter.size)],
                  fill=(255, 255, 255, 9), width=width)
    painter.img.alpha_composite(stripes)

    # Siren bloom behind the light bar.
    glow(painter, 430, 266, 200, 110, a(palette.red, 80), 96)
    glow(painter, 594, 266, 200, 110, a(palette.blue, 80), 96)


def draw_emblem(painter, palette):
    outer = [(512, 112), (892, 236), (892, 606), (512, 940), (132, 606), (132, 236)]
    inner = [(512, 158), (864, 268), (864, 594), (512, 896), (160, 594), (160, 268)]

    painter.polygon(outer, palette.gold)
    painter.outline(outer, palette.gold_dark, 7)
    gradient_polygon(painter, inner, palette.shield_top, palette.shield_bottom)
    painter.outline(inner, a(palette.gold_light, 90), 5)

    # Roof-mounted light bar.
    painter.round_rect(346, 228, 678, 300, 30, opaque(palette.housing))
    painter.round_rect(360, 242, 503, 288, 17, palette.red)
    painter.round_rect(521, 242, 664, 288, 17, palette.blue)
    painter.round_rect(366, 247, 497, 262, 8, a(palette.red_light, 170))
    painter.round_rect(527, 247, 658, 262, 8, a(palette.blue_light, 170))

    # Rear view of the police car: wheels first so the body tucks over them.
    painter.round_rect(262, 598, 332, 722, 24, palette.tyre)
    painter.round_rect(692, 598, 762, 722, 24, palette.tyre)
    painter.round_rect(274, 638, 320, 672, 12, opaque(palette.tyre_hub))
    painter.round_rect(704, 638, 750, 672, 12, opaque(palette.tyre_hub))

    painter.round_rect(372, 300, 652, 470, 36, palette.dark)
    painter.round_rect(388, 316, 636, 452, 28, palette.glass)
    painter.round_rect(404, 330, 620, 378, 18, a(palette.glass_light, 150))

    painter.round_rect(292, 424, 732, 660, 42, palette.white)

    # Tail lights.
    painter.round_rect(314, 446, 404, 504, 16, palette.red)
    painter.round_rect(620, 446, 710, 504, 16, palette.red)
    painter.round_rect(320, 452, 398, 474, 10, a(palette.red_light, 190))
    painter.round_rect(626, 452, 704, 474, 10, a(palette.red_light, 190))

    # Livery.
    painter.rect(292, 522, 732, 566, palette.blue)
    painter.rect(292, 566, 732, 578, palette.blue_deep)
    for i in range(0, 9, 2):
        painter.rect(314 + i * 46, 596, 314 + i * 46 + 46, 620, palette.blue)

    # Bumper and plate.
    painter.round_rect(306, 636, 718, 676, 20, palette.bumper)
    painter.round_rect(458, 642, 566, 670, 12, palette.plate)

    # Pursuit chevron under the car.
    painter.polygon([(512, 716), (608, 764), (608, 790), (512, 742), (416, 790), (416, 764)],
                    palette.gold)


def render_icon(px, rounded=None, palette=Palette):
    canvas = supersample(px)
    painter = Painter(canvas)
    draw_backdrop(painter, palette)
    draw_emblem(painter, palette)
    img = painter.img.resize((px, px), Image.Resampling.LANCZOS)

    if rounded:
        mask = Image.new("L", (px, px), 0)
        ImageDraw.Draw(mask).rounded_rectangle(
            [0, 0, px - 1, px - 1], radius=round(px * rounded), fill=255)
        img.putalpha(ImageChops.multiply(img.getchannel("A"), mask))
    return img


def render_emblem(px, palette=Palette):
    canvas = supersample(px)
    painter = Painter(canvas)
    draw_emblem(painter, palette)
    return painter.img.resize((px, px), Image.Resampling.LANCZOS)


def adaptive_foreground(px):
    """108dp adaptive-icon layer: emblem inside the 66dp safe zone."""
    inner = round(px * 0.80)
    layer = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    layer.alpha_composite(render_emblem(inner), ((px - inner) // 2, (px - inner) // 2))
    return layer


# --------------------------------------------------------------------- lockup

LOCKUP_W = 560
LOCKUP_H = 184


def tracked(pen, xy, text, font, fill, spacing):
    x, y = xy
    for char in text:
        pen.text((x, y), char, font=font, fill=fill, anchor="ls")
        x += pen.textlength(char, font=font) + spacing


def render_lockup(width):
    """Badge plus wordmark, used by every launch/splash screen."""
    scale = width / LOCKUP_W
    height = round(LOCKUP_H * scale)
    ss = 4
    img = Image.new("RGBA", (LOCKUP_W * ss, LOCKUP_H * ss), (0, 0, 0, 0))
    pen = ImageDraw.Draw(img)

    img.alpha_composite(render_emblem(176 * ss), (8 * ss, 6 * ss))

    title = ImageFont.truetype(FONT_PATH, 60 * ss)
    tagline = ImageFont.truetype(FONT_PATH, 18 * ss)

    x = 198 * ss
    pen.text((x, 74 * ss), "POLICE", font=title, fill=Palette.white, anchor="ls")
    pen.text((x, 134 * ss), "CHASER", font=title, fill=Palette.gold, anchor="ls")

    rule = pen.textlength("CHASER", font=title)
    pen.rectangle([x, 146 * ss, x + rule, 149 * ss], fill=a(Palette.gold, 190))
    tracked(pen, (x + 2 * ss, 172 * ss), "A 128K-STYLE PURSUIT", tagline,
            a(Palette.dim, 255), 2.2 * ss)

    return img.resize((width, height), Image.Resampling.LANCZOS)


def splash_background(width, height):
    img = Image.new("RGBA", (width, height))
    pen = ImageDraw.Draw(img)
    for y in range(height):
        pen.line([(0, y), (width, y)],
                 fill=lerp(Palette.navy_top, Palette.navy_bottom, y / max(1, height - 1)))

    stripes = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    spen = ImageDraw.Draw(stripes)
    step = max(18, width // 13)
    for x in range(-height, width + height, step):
        spen.line([(x, 0), (x + height, height)], fill=(255, 255, 255, 9),
                  width=max(2, width // 60))
    img.alpha_composite(stripes)
    return img


def render_splash(width, height):
    img = splash_background(width, height)
    lockup = render_lockup(round(width * 0.74))
    img.alpha_composite(lockup, ((width - lockup.width) // 2,
                                 (height - lockup.height) // 2))
    return img


# ---------------------------------------------------------------------- files

def save(img, path, rgb=False):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    (img.convert("RGB") if rgb else img).save(path, optimize=True)
    return path


def write_ico(path, images):
    blobs = []
    for img in images:
        buffer = io.BytesIO()
        img.save(buffer, "PNG")
        blobs.append((img.width, buffer.getvalue()))

    header = struct.pack("<HHH", 0, 1, len(blobs))
    directory = b""
    payload = b""
    offset = 6 + 16 * len(blobs)
    for size, data in blobs:
        directory += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32,
                                 len(data), offset)
        payload += data
        offset += len(data)
    with open(path, "wb") as handle:
        handle.write(header + directory + payload)
    return path


def macos_tile(px):
    """Padded rounded tile with a soft shadow, as macOS and Windows tiles expect."""
    inner = round(px * 0.815)
    art = render_icon(inner, rounded=0.225)
    shadow = Image.new("RGBA", (inner, inner), (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 90), (0, 0, inner, inner), art.getchannel("A"))
    shadow = shadow.filter(ImageFilter.GaussianBlur(max(1.0, inner * 0.02)))

    tile = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    left = (px - inner) // 2
    tile.alpha_composite(shadow, (left, left + round(px * 0.012)))
    tile.alpha_composite(art, (left, left))
    return tile


def write_android(root, master):
    resources = os.path.join(root, "src", "PoliceChaser.Android", "Resources")

    def icon(px):
        return master.resize((px, px), Image.Resampling.LANCZOS) if px > 160 \
            else render_icon(px)

    save(icon(192), os.path.join(resources, "Drawable", "Icon.png"))

    densities = {"mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192}
    for density, px in densities.items():
        save(icon(px), os.path.join(resources, "mipmap-" + density, "Icon.png"))

    # Adaptive icon: 108dp foreground layers plus a flat background colour.
    for density, dp in (("mdpi", 108), ("hdpi", 162), ("xhdpi", 216),
                        ("xxhdpi", 324), ("xxxhdpi", 432)):
        save(adaptive_foreground(dp),
             os.path.join(resources, "mipmap-" + density, "icon_foreground.png"))

    save(render_lockup(560), os.path.join(resources, "drawable-xhdpi", "splash_logo.png"))
    for density, width in (("mdpi", 280), ("hdpi", 420), ("xxhdpi", 840), ("xxxhdpi", 1120)):
        save(render_lockup(width),
             os.path.join(resources, "drawable-" + density, "splash_logo.png"))


def write_ios(root, master):
    assets = os.path.join(root, "src", "PoliceChaser.iOS", "Assets.xcassets")
    icon_set = os.path.join(assets, "AppIcon.appiconset")

    # Xcode 16+ single-size icon: one 1024px image per appearance, and actool
    # generates every device size from it. Xcode only accepts dark/tinted
    # luminosity variants in this format.
    images = []
    keep = {"Contents.json"}
    for suffix, art, appearance in (
            ("", master, None),
            ("-dark", render_icon(1024, palette=DarkPalette), "dark"),
            ("-tinted", render_icon(1024, palette=TintedPalette), "tinted")):
        filename = "icon-1024x1024@1x%s.png" % suffix
        save(art.resize((1024, 1024), Image.Resampling.LANCZOS),
             os.path.join(icon_set, filename), rgb=True)
        entry = {"filename": filename, "idiom": "universal",
                 "platform": "ios", "size": "1024x1024"}
        if appearance:
            entry["appearances"] = [{"appearance": "luminosity",
                                     "value": appearance}]
        images.append(entry)
        keep.add(filename)

    for name in os.listdir(icon_set):
        if name not in keep:
            os.remove(os.path.join(icon_set, name))

    with open(os.path.join(icon_set, "Contents.json"), "w") as handle:
        json.dump({"images": images, "info": {"version": 1, "author": "xcode"}},
                  handle, indent=2)

    with open(os.path.join(assets, "Contents.json"), "w") as handle:
        json.dump({"info": {"version": 1, "author": "xcode"}}, handle, indent=2)

    # Launch lockup: a landscape badge/wordmark rather than a square app tile.
    launch = os.path.join(assets, "LaunchLogo.imageset")
    for scale, width in ((1, 520), (2, 1040), (3, 1560)):
        name = "LaunchLogo.png" if scale == 1 else "LaunchLogo@%dx.png" % scale
        save(render_lockup(width), os.path.join(launch, name))
    with open(os.path.join(launch, "Contents.json"), "w") as handle:
        json.dump({
            "images": [
                {"filename": "LaunchLogo.png", "idiom": "universal", "scale": "1x"},
                {"filename": "LaunchLogo@2x.png", "idiom": "universal", "scale": "2x"},
                {"filename": "LaunchLogo@3x.png", "idiom": "universal", "scale": "3x"},
            ],
            "info": {"version": 1, "author": "xcode"},
        }, handle, indent=2)

    # Loose CFBundleIconFiles copies for the Info.plist fallback path.
    icons = os.path.join(root, "src", "PoliceChaser.iOS", "Icons")
    loose = [("Icon-20", 2, 40), ("Icon-20", 3, 60), ("Icon-29", 2, 58),
             ("Icon-29", 3, 87), ("Icon-40", 2, 80), ("Icon-40", 3, 120),
             ("Icon-60", 2, 120), ("Icon-60", 3, 180), ("Icon-76", 1, 76),
             ("Icon-76", 2, 152), ("Icon-83.5", 2, 167)]
    for base, scale, px in loose:
        save(master.resize((px, px), Image.Resampling.LANCZOS),
             os.path.join(icons, "%s@%dx.png" % (base, scale)), rgb=True)
    save(master, os.path.join(icons, "Icon-1024.png"), rgb=True)


def write_windows(root, master):
    def icon(px):
        return master.resize((px, px), Image.Resampling.LANCZOS) if px > 160 \
            else render_icon(px)

    scales = {"scale-100": 1.0, "scale-125": 1.25, "scale-150": 1.5,
              "scale-200": 2.0, "scale-400": 4.0}

    for name, base in (("StoreLogo", 50), ("Square44x44Logo", 44),
                       ("Square150x150Logo", 150)):
        save(macos_tile(base), os.path.join(ASSETS, name + ".png"))
        for label, factor in scales.items():
            save(macos_tile(round(base * factor)),
                 os.path.join(ASSETS, "%s.%s.png" % (name, label)))

    for target in (16, 24, 32, 48, 256):
        save(macos_tile(target),
             os.path.join(ASSETS, "Square44x44Logo.targetsize-%d.png" % target))

    save(render_splash(620, 300), os.path.join(ASSETS, "SplashScreen.png"))
    for label, factor in scales.items():
        save(render_splash(round(620 * factor), round(300 * factor)),
             os.path.join(ASSETS, "SplashScreen.%s.png" % label))


def main():
    os.makedirs(ASSETS, exist_ok=True)

    master = render_icon(1024)
    save(master.convert("RGB"), os.path.join(ASSETS, "icon-1024.png"))

    def icon(px):
        return master.resize((px, px), Image.Resampling.LANCZOS) if px > 160 \
            else render_icon(px)

    # ---- macOS .icns (padded rounded tile with baked shadow) --------------
    iconset = os.path.join(ASSETS, "AppIcon.iconset")
    for size in (16, 32, 64, 128, 256, 512):
        save(macos_tile(size), os.path.join(iconset, "icon_%dx%d.png" % (size, size)))
        save(macos_tile(size * 2),
             os.path.join(iconset, "icon_%dx%d@2x.png" % (size, size)))
    icns = os.path.join(ASSETS, "AppIcon.icns")
    try:
        subprocess.run(["iconutil", "-c", "icns", iconset, "-o", icns],
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except (OSError, subprocess.CalledProcessError):
        print("warning: iconutil unavailable; AppIcon.icns not rebuilt")

    # ---- Windows .ico for the desktop executable -------------------------
    write_ico(os.path.join(ROOT, "src", "PoliceChaser.Desktop", "Icon.ico"),
              [icon(size) for size in (16, 24, 32, 48, 64, 128, 256)])

    # ---- Windows MSIX tiles and splash -----------------------------------
    write_windows(ROOT, master)

    # ---- iOS -------------------------------------------------------------
    write_ios(ROOT, master)

    # ---- Android ---------------------------------------------------------
    write_android(ROOT, master)

    print("icons and splash screens baked from the 1024px master")


if __name__ == "__main__":
    main()
