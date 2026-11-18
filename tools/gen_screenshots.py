#!/usr/bin/env python3
"""Renders the promotional screenshot set.

Drives the desktop build's existing self-test harness over the screens worth
showing, then frames each capture on the game's navy livery:

    artifacts/screenshots/raw/<screen>.png         native 1280x960 capture
    artifacts/screenshots/store/NN-<screen>.png    captioned 1920x1080 store frame

    python3 tools/gen_screenshots.py
"""

import os
import subprocess
import sys

try:
    from PIL import Image, ImageDraw, ImageFilter, ImageFont
except ImportError:  # pragma: no cover - developer tooling only
    sys.exit("Pillow is required: python3 -m pip install pillow")

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)

import gen_icons  # noqa: E402  (same folder)

PROJECT = os.path.join(ROOT, "src", "PoliceChaser.Desktop", "PoliceChaser.Desktop.csproj")
OUT = os.path.join(ROOT, "artifacts", "screenshots")
RAW = os.path.join(OUT, "raw")
STORE = os.path.join(OUT, "store")

FRAME_W, FRAME_H = 1920, 1080
GAME_W, GAME_H = 1280, 960
GAME_X = (FRAME_W - GAME_W) // 2
GAME_Y = 100

CAPTION_FONT = ImageFont.truetype(gen_icons.FONT_PATH, 34)

# screen, frames to simulate, store caption
SHOTS = [
    ("title", 90, "NINE MISSIONS - THREE SKILL LEVELS"),
    ("difficulty", 60, "BEGINNER, MEDIUM OR HARD"),
    ("mission", 60, "TWO SERIES, NINE PURSUITS"),
    ("briefing", 60, "BRIEFED BEFORE YOU DEPLOY"),
    ("race", 240, "REAR-VIEW PURSUIT"),
    ("race2", 240, "NEON GRID"),
    ("race3", 300, "ARMED SUSPECTS SHOOT BACK"),
    ("crash", 60, "TRAFFIC HURTS"),
    ("busted", 60, "RAM HIM AND HE IS BUSTED"),
    ("gameover", 60, "BEAT THE CLOCK"),
    ("scores", 60, "TOP TEN HIGH SCORES"),
    ("pads", 120, "TOUCH PADS ON MOBILE"),
]


def capture(screen, frames):
    os.makedirs(RAW, exist_ok=True)
    target = os.path.join(RAW, screen + ".png")
    if os.path.exists(target):
        os.remove(target)

    print("capture", screen)
    subprocess.run(
        ["dotnet", "run", "--project", PROJECT, "-c", "Debug", "--no-build", "--",
         "--selftest", screen, "--frames", str(frames), "--shot", target],
        cwd=ROOT, check=True, stdout=subprocess.DEVNULL)

    if not os.path.exists(target):
        raise SystemExit("no capture written for " + screen)

    normalize(target)
    return target


def fit_canvas(img):
    """Crop the window's pillarboxes so the capture is the 4:3 canvas size.

    The desktop window can be shorter than the preferred 960px backbuffer (menu bar
    or dock), so the game letterboxes its canvas inside it.
    """
    width, height = img.size
    if (width, height) == (GAME_W, GAME_H):
        return img

    if width * GAME_H > height * GAME_W:
        crop_w = round(height * GAME_W / GAME_H)
        left = (width - crop_w) // 2
        box = (left, 0, left + crop_w, height)
    else:
        crop_h = round(width * GAME_H / GAME_W)
        top = (height - crop_h) // 2
        box = (0, top, width, top + crop_h)

    return img.crop(box).resize((GAME_W, GAME_H), Image.Resampling.LANCZOS)


def normalize(path):
    """Crop a capture to the 4:3 canvas size and write it back in place."""
    with Image.open(path) as img:
        if img.size != (GAME_W, GAME_H):
            fit_canvas(img).save(path)


def compose(shot_path, caption, out_path):
    frame = gen_icons.splash_background(FRAME_W, FRAME_H)

    # Soft siren bloom behind the framed capture.
    bloom = Image.new("RGBA", (FRAME_W, FRAME_H), (0, 0, 0, 0))
    ImageDraw.Draw(bloom).ellipse(
        [GAME_X - 140, GAME_Y - 100, GAME_X + GAME_W + 140, GAME_Y + GAME_H + 100],
        fill=(46, 74, 150, 90))
    frame.alpha_composite(bloom.filter(ImageFilter.GaussianBlur(120)))

    with Image.open(shot_path) as shot:
        frame.alpha_composite(shot.convert("RGBA"), (GAME_X, GAME_Y))

    pen = ImageDraw.Draw(frame)
    pen.rectangle([GAME_X - 2, GAME_Y - 2, GAME_X + GAME_W + 1, GAME_Y + GAME_H + 1],
                  outline=(247, 201, 72, 150), width=2)
    pen.rectangle([0, GAME_Y - 6, FRAME_W, GAME_Y - 4], fill=(247, 201, 72, 80))

    lockup = gen_icons.render_lockup(190)
    frame.alpha_composite(lockup, (56, (GAME_Y - lockup.height) // 2))

    # Right-align the caption against the right margin.
    caption_width = sum(pen.textlength(char, font=CAPTION_FONT) + 2.0
                        for char in caption) - 2.0
    gen_icons.tracked(pen, (FRAME_W - 56 - caption_width, 58), caption, CAPTION_FONT,
                      (247, 201, 72, 255), 2.0)

    frame.convert("RGB").save(out_path, optimize=True)


def main():
    os.makedirs(STORE, exist_ok=True)

    print("building desktop capture harness")
    subprocess.run(["dotnet", "build", PROJECT, "-c", "Debug", "--nologo"],
                   cwd=ROOT, check=True, stdout=subprocess.DEVNULL)

    for index, (screen, frames, caption) in enumerate(SHOTS, start=1):
        raw = capture(screen, frames)
        target = os.path.join(STORE, "%02d-%s.png" % (index, screen))
        compose(raw, caption, target)
        print("framed", os.path.relpath(target, ROOT))

    # The wordmark used across the frames, handy as a standalone asset.
    gen_icons.render_lockup(1040).save(os.path.join(OUT, "wordmark-1040.png"))
    print("wrote", os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
