#!/usr/bin/env python3
"""Renders the store gameplay videos.

Drives the desktop self-test harness at a fixed timestep, frames each capture on
the same navy livery as the store screenshots, and encodes the sequence with
ffmpeg:

    ~/Movies/PoliceChaser-macos-gameplay.mp4   1920x1080, 30 fps
    ~/Movies/PoliceChaser-ipad-gameplay.mp4    1920x1080, 30 fps, touch pads

The captured PNGs live in artifacts/video-frames/<name> only while a video is
being encoded, so nothing large is left behind.

    python3 tools/gen_gameplay_videos.py
"""

import os
import shutil
import subprocess
import sys

try:
    from PIL import Image, ImageDraw, ImageFilter
except ImportError:  # pragma: no cover - developer tooling only
    sys.exit("Pillow is required: python3 -m pip install pillow")

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)

import gen_icons  # noqa: E402  (same folder)
import gen_screenshots  # noqa: E402  (frame geometry, shared captions)

PROJECT = gen_screenshots.PROJECT
FRAMES = os.path.join(ROOT, "artifacts", "video-frames")
OUT = os.path.join(os.path.expanduser("~"), "Movies")

FPS = 30
SECONDS = 12
WARMUP = 45

FRAME_W, FRAME_H = gen_screenshots.FRAME_W, gen_screenshots.FRAME_H
GAME_W, GAME_H = gen_screenshots.GAME_W, gen_screenshots.GAME_H
GAME_X, GAME_Y = gen_screenshots.GAME_X, gen_screenshots.GAME_Y
CAPTION_FONT = gen_screenshots.CAPTION_FONT

# name, harness screen, caption, output file
VIDEOS = [
    ("macos", "race", "REAR-VIEW PURSUIT", "PoliceChaser-macos-gameplay.mp4"),
    ("ipad", "pads", "TOUCH CONTROLS", "PoliceChaser-ipad-gameplay.mp4"),
]


def plate(caption):
    """Builds one static store frame; the gameplay window is pasted in per frame."""
    frame = gen_icons.splash_background(FRAME_W, FRAME_H)

    # Soft siren bloom behind the framed capture.
    bloom = Image.new("RGBA", (FRAME_W, FRAME_H), (0, 0, 0, 0))
    ImageDraw.Draw(bloom).ellipse(
        [GAME_X - 140, GAME_Y - 100, GAME_X + GAME_W + 140, GAME_Y + GAME_H + 100],
        fill=(46, 74, 150, 90))
    frame.alpha_composite(bloom.filter(ImageFilter.GaussianBlur(120)))

    pen = ImageDraw.Draw(frame)
    pen.rectangle([GAME_X - 2, GAME_Y - 2, GAME_X + GAME_W + 1, GAME_Y + GAME_H + 1],
                  outline=(247, 201, 72, 150), width=2)
    pen.rectangle([0, GAME_Y - 6, FRAME_W, GAME_Y - 4], fill=(247, 201, 72, 80))

    lockup = gen_icons.render_lockup(190)
    frame.alpha_composite(lockup, (56, (GAME_Y - lockup.height) // 2))

    width = sum(pen.textlength(char, font=CAPTION_FONT) + 2.0 for char in caption) - 2.0
    gen_icons.tracked(pen, (FRAME_W - 56 - width, 58), caption, CAPTION_FONT,
                      (247, 201, 72, 255), 2.0)

    return frame.convert("RGB")


def capture(name, screen, frames):
    directory = os.path.join(FRAMES, name)
    shutil.rmtree(directory, ignore_errors=True)
    os.makedirs(directory)

    print("capture", name, "(", screen, frames, "frames )")
    subprocess.run(
        ["dotnet", "run", "--project", PROJECT, "-c", "Debug", "--no-build", "--",
         "--selftest", screen, "--video", directory,
         "--video-warmup", str(WARMUP), "--video-frames", str(frames), "--fps", str(FPS)],
        cwd=ROOT, check=True)

    written = sorted(name for name in os.listdir(directory) if name.endswith(".png"))
    if len(written) != frames:
        raise SystemExit("expected %d frames for %s, got %d" % (frames, name, len(written)))
    return directory, written


def encode(directory, written, caption, filename):
    target = os.path.join(OUT, filename)
    base = plate(caption)

    print("encode", target)
    command = [
        "ffmpeg", "-y", "-v", "error",
        "-f", "rawvideo", "-pix_fmt", "rgb24",
        "-s", "%dx%d" % (FRAME_W, FRAME_H), "-r", str(FPS), "-i", "-",
        "-c:v", "libx264", "-preset", "medium", "-crf", "20",
        "-pix_fmt", "yuv420p", "-movflags", "+faststart",
        target,
    ]

    process = subprocess.Popen(command, stdin=subprocess.PIPE)
    try:
        for name in written:
            with Image.open(os.path.join(directory, name)) as shot:
                canvas = gen_screenshots.fit_canvas(shot.convert("RGB"))
            frame = base.copy()
            frame.paste(canvas, (GAME_X, GAME_Y))
            process.stdin.write(frame.tobytes())
    finally:
        process.stdin.close()
        if process.wait() != 0:
            raise SystemExit("ffmpeg failed while writing " + filename)

    return target


def main():
    if shutil.which("ffmpeg") is None:
        raise SystemExit("ffmpeg is required: brew install ffmpeg")

    os.makedirs(OUT, exist_ok=True)

    print("building desktop capture harness")
    subprocess.run(["dotnet", "build", PROJECT, "-c", "Debug", "--nologo"],
                   cwd=ROOT, check=True, stdout=subprocess.DEVNULL)

    frames = FPS * SECONDS
    for name, screen, caption, filename in VIDEOS:
        directory, written = capture(name, screen, frames)
        target = encode(directory, written, caption, filename)
        shutil.rmtree(directory, ignore_errors=True)
        print("wrote", target)


if __name__ == "__main__":
    main()
