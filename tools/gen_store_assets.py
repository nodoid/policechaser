#!/usr/bin/env python3
"""Bakes a store-listing image set for every platform into Stores/.

Sizes follow the live store documentation (checked November 2026):

    Apple    iPhone 6.9" 2868x1320, iPhone 6.3" 2622x1206,
             iPad 13" 2752x2064, Mac 2880x1800 (16:10)
    Google   phone 1920x1080, tablet 2560x1440, feature graphic 1024x500
    Microsoft desktop screenshots 1920x1080 and 3840x2160
    Xbox     screenshots 1920x1080 and 3840x2160, key art

    python3 tools/gen_store_assets.py

The raw 1280x960 captures in artifacts/screenshots/raw are the source (run
tools/gen_screenshots.py first). Store frames carry the wordmark and a caption;
Microsoft and Xbox screenshots stay clean, as those stores reject marketing
overlays on screenshots.
"""

import os
import sys

try:
    from PIL import Image, ImageDraw, ImageFilter, ImageFont
except ImportError:  # pragma: no cover - developer tooling only
    sys.exit("Pillow is required: python3 -m pip install pillow")

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)

import gen_icons  # noqa: E402

RAW = os.path.join(ROOT, "artifacts", "screenshots", "raw")
OUT = os.path.join(ROOT, "Stores")

CAPTIONS = [
    ("race", "REAR-VIEW PURSUIT"),
    ("race2", "NEON GRID"),
    ("race3", "ARMED SUSPECTS SHOOT BACK"),
    ("crash", "TRAFFIC HURTS"),
    ("busted", "RAM HIM AND HE IS BUSTED"),
    ("pads", "TOUCH PADS ON MOBILE"),
    ("mission", "TWO SERIES, NINE PURSUITS"),
    ("scores", "TOP TEN HIGH SCORES"),
]

# Microsoft wants gameplay only: no captions, no logos, no marketing copy.
CLEAN = ["race", "race2", "race3", "crash", "busted", "scores"]


def shot(name):
    path = os.path.join(RAW, name + ".png")
    if not os.path.exists(path):
        raise SystemExit("missing capture %s (run tools/gen_screenshots.py)" % path)
    return Image.open(path).convert("RGB")


def cover(img, width, height):
    """Scale and centre-crop so the image fills the frame."""
    scale = max(width / img.width, height / img.height)
    size = (round(img.width * scale), round(img.height * scale))
    resized = img.resize(size, Image.Resampling.LANCZOS)
    left = (resized.width - width) // 2
    top = (resized.height - height) // 2
    return resized.crop((left, top, left + width, top + height))


def backdrop(img, width, height, darkness=170):
    """Blurred, darkened cover of the capture, used behind the framed shot."""
    blur = cover(img, width, height).filter(ImageFilter.GaussianBlur(max(24, width // 40)))
    shade = Image.new("RGBA", (width, height), (4, 6, 16, darkness))
    blur = blur.convert("RGBA")
    blur.alpha_composite(shade)
    return blur


def captioned(name, caption, width, height, path):
    """Store frame: capture on the navy livery with wordmark and caption."""
    frame = gen_icons.splash_background(width, height)

    game_h = round(height * 0.889)
    game_w = round(game_h * 4 / 3)
    game_x = (width - game_w) // 2
    game_y = round(height * 0.0926)

    bloom = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    pad_x = round(width * 0.073)
    pad_y = round(height * 0.093)
    ImageDraw.Draw(bloom).ellipse(
        [game_x - pad_x, game_y - pad_y, game_x + game_w + pad_x, game_y + game_h + pad_y],
        fill=(46, 74, 150, 90))
    frame.alpha_composite(bloom.filter(ImageFilter.GaussianBlur(max(60, width // 16))))

    frame.alpha_composite(shot(name).resize((game_w, game_h), Image.Resampling.LANCZOS).convert("RGBA"),
                          (game_x, game_y))

    pen = ImageDraw.Draw(frame)
    line = max(2, round(width / 960))
    pen.rectangle([game_x - line, game_y - line, game_x + game_w + line - 1,
                   game_y + game_h + line - 1], outline=(247, 201, 72, 150), width=line)
    pen.rectangle([0, game_y - 3 * line, width, game_y - 2 * line], fill=(247, 201, 72, 80))

    lockup = gen_icons.render_lockup(round(width * 0.099))
    frame.alpha_composite(lockup, (round(width * 0.029), (game_y - lockup.height) // 2))

    font = ImageFont.truetype(gen_icons.FONT_PATH, round(height * 0.0315))
    tracking = max(1.0, width * 0.00104)
    caption_width = sum(pen.textlength(char, font=font) + tracking for char in caption) - tracking
    gen_icons.tracked(pen, (width - round(width * 0.029) - caption_width, round(height * 0.054)),
                      caption, font, (247, 201, 72, 255), tracking)

    frame.convert("RGB").save(path, optimize=True)


def clean(name, width, height, path):
    """Microsoft/Xbox screenshot: gameplay only, full-bleed 16:9."""
    frame = backdrop(shot(name), width, height)

    game_h = round(height * 0.94)
    game_w = round(game_h * 4 / 3)
    game_x = (width - game_w) // 2
    game_y = (height - game_h) // 2
    frame.alpha_composite(shot(name).resize((game_w, game_h), Image.Resampling.LANCZOS).convert("RGBA"),
                          (game_x, game_y))

    pen = ImageDraw.Draw(frame)
    line = max(2, round(width / 1280))
    pen.rectangle([game_x, game_y, game_x + game_w - 1, game_y + game_h - 1],
                  outline=(247, 201, 72, 70), width=line)
    frame.convert("RGB").save(path, optimize=True)


def feature_graphic(width, height, path):
    frame = backdrop(shot("race3"), width, height, darkness=185)
    lockup = gen_icons.render_lockup(round(width * 0.46))
    frame.alpha_composite(lockup, ((width - lockup.width) // 2,
                                   (height - lockup.height) // 2 - round(height * 0.02)))

    pen = ImageDraw.Draw(frame)
    font = ImageFont.truetype(gen_icons.FONT_PATH, round(height * 0.062))
    text = "NINE MISSIONS - THREE SKILL LEVELS"
    tracking = max(1.0, width * 0.0022)
    text_width = sum(pen.textlength(char, font=font) + tracking for char in text) - tracking
    gen_icons.tracked(pen, ((width - text_width) / 2, height * 0.88), text, font,
                      (247, 201, 72, 235), tracking)
    frame.convert("RGB").save(path, optimize=True)


def key_art(path, width, height):
    gen_icons.render_splash(width, height).convert("RGB").save(path, optimize=True)


def branded_hero(width, height, path):
    """Titled hero / super hero art: wordmark plus a sharp gameplay panel."""
    frame = backdrop(shot("race3"), width, height, darkness=120)

    game_h = round(height * 0.72)
    game_w = round(game_h * 4 / 3)
    game_x = width - game_w - round(width * 0.04)
    game_y = round(height * 0.09)
    frame.alpha_composite(shot("race3").resize((game_w, game_h), Image.Resampling.LANCZOS).convert("RGBA"),
                          (game_x, game_y))

    pen = ImageDraw.Draw(frame)
    line = max(2, round(width / 960))
    pen.rectangle([game_x, game_y, game_x + game_w - 1, game_y + game_h - 1],
                  outline=(247, 201, 72, 120), width=line)

    lockup = gen_icons.render_lockup(round(width * 0.32))
    frame.alpha_composite(lockup, (round(width * 0.05), round(height * 0.12)))

    fade = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    fpen = ImageDraw.Draw(fade)
    start = int(height * 0.84)
    for y in range(start, height):
        alpha = int(110 * (y - start) / max(1, height - start))
        fpen.line([(0, y), (width, y)], fill=(4, 6, 16, alpha))
    frame.alpha_composite(fade)
    frame.convert("RGB").save(path, optimize=True)


def art_only_square(size, path):
    """Featured promotional square: artwork only - no title, logos or marketing text."""
    frame = backdrop(shot("race3"), size, size, darkness=170)
    game_w = round(size * 0.96)
    game_h = round(game_w * 0.75)
    frame.alpha_composite(shot("race3").resize((game_w, game_h), Image.Resampling.LANCZOS).convert("RGBA"),
                          ((size - game_w) // 2, (size - game_h) // 2))
    frame.convert("RGB").save(path, optimize=True)


def main():
    made = []

    # Older runs wrote a square with the wordmark; the store requires artwork only.
    for stale in ("windows", "xbox"):
        old = os.path.join(OUT, stale, "art", "square-1080x1080.png")
        if os.path.exists(old):
            os.remove(old)

    def save(path, width, height):
        made.append((os.path.relpath(path, ROOT), width, height))

    apps = [
        ("apple/iphone-6.9", 2868, 1320, CAPTIONS),
        ("apple/iphone-6.3", 2622, 1206, CAPTIONS),
        ("apple/ipad-13", 2752, 2064, CAPTIONS),
        ("apple/mac", 2880, 1800,
         [item for item in CAPTIONS if item[0] != "pads"] +
         [("briefing", "BRIEFED BEFORE YOU DEPLOY")]),
        ("android/phone-16x9", 1920, 1080, CAPTIONS),
        ("android/tablet-16x9", 2560, 1440, CAPTIONS),
    ]

    for folder, width, height, items in apps:
        directory = os.path.join(OUT, folder)
        os.makedirs(directory, exist_ok=True)
        for index, (name, caption) in enumerate(items, start=1):
            path = os.path.join(directory, "%02d-%s.png" % (index, name))
            captioned(name, caption, width, height, path)
            save(path, width, height)
            print("wrote", os.path.relpath(path, ROOT))

    directory = os.path.join(OUT, "android")
    os.makedirs(directory, exist_ok=True)
    path = os.path.join(directory, "feature-graphic-1024x500.png")
    feature_graphic(1024, 500, path)
    save(path, 1024, 500)

    for store in ("windows", "xbox"):
        for width, height in ((1920, 1080), (3840, 2160)):
            directory = os.path.join(OUT, store, "screenshots-%dx%d" % (width, height))
            os.makedirs(directory, exist_ok=True)
            for index, name in enumerate(CLEAN, start=1):
                target = os.path.join(directory, "%02d-%s.png" % (index, name))
                clean(name, width, height, target)
                save(target, width, height)

        art = os.path.join(OUT, store, "art")
        os.makedirs(art, exist_ok=True)
        for name, width, height in (("hero-1920x1080", 1920, 1080),
                                    ("titled-hero-1920x1080", 1920, 1080),
                                    ("super-hero-3840x2160", 3840, 2160)):
            target = os.path.join(art, name + ".png")
            branded_hero(width, height, target)
            save(target, width, height)

        poster = os.path.join(art, "poster-720x1080.png")
        key_art(poster, 720, 1080)
        save(poster, 720, 1080)

        square = os.path.join(art, "featured-promo-square-1080x1080.png")
        art_only_square(1080, square)
        save(square, 1080, 1080)

    # Microsoft Store key art doubles as the desktop tile set.
    for name, size in (("storelogo-300x300", 300), ("square-44x44", 44),
                       ("square-150x150", 150)):
        target = os.path.join(OUT, "windows", "art", name + ".png")
        gen_icons.render_icon(size).convert("RGB").save(target, optimize=True)
        save(target, size, size)

    print("\n%d images" % len(made))
    for rel, width, height in made:
        print("  %-58s %dx%d" % (rel, width, height))


if __name__ == "__main__":
    main()
