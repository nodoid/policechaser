#!/usr/bin/env python3
"""Extracts SCREEN$ loading screens from a .tap file as PNGs.

Used only as visual reference while matching the look of the 8-bit original.
No extracted data is shipped with the game.

    python3 tools/fuse_reference.py /tmp/chasehq/chase.tap /tmp/chasehq/screens
"""
import os
import struct
import sys
import zlib

PALETTE = [(0, 0, 0), (0, 0, 205), (205, 0, 0), (205, 0, 205),
           (0, 205, 0), (0, 205, 205), (205, 205, 0), (205, 205, 205)]
BRIGHT = [(0, 0, 0), (0, 0, 255), (255, 0, 0), (255, 0, 255),
          (0, 255, 0), (0, 255, 255), (255, 255, 0), (255, 255, 255)]


def write_png(path, data):
    width, height = 256, 192
    pixels = [[(0, 0, 0)] * width for _ in range(height)]
    for y in range(height):
        offset = ((y & 0xC0) << 5) | ((y & 0x07) << 8) | ((y & 0x38) << 2)
        for xb in range(32):
            bits = data[offset + xb]
            attr = data[6144 + (y // 8) * 32 + xb]
            ink, paper = attr & 7, (attr >> 3) & 7
            palette = BRIGHT if attr & 0x40 else PALETTE
            for bit in range(8):
                pixels[y][xb * 8 + bit] = palette[ink] if bits & (0x80 >> bit) else palette[paper]

    raw = b"".join(b"\x00" + bytes(v for px in row for v in px) for row in pixels)

    def chunk(tag, payload):
        return (struct.pack(">I", len(payload)) + tag + payload +
                struct.pack(">I", zlib.crc32(tag + payload) & 0xffffffff))

    png = (b"\x89PNG\r\n\x1a\n" +
           chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)) +
           chunk(b"IDAT", zlib.compress(raw, 9)) +
           chunk(b"IEND", b""))
    with open(path, "wb") as handle:
        handle.write(png)


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    tap_path, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    data = open(tap_path, "rb").read()

    index = 0
    count = 0
    while index + 2 <= len(data):
        length = struct.unpack("<H", data[index:index + 2])[0]
        index += 2
        block = data[index:index + length]
        index += length
        if len(block) < 2:
            break
        if block[0] != 0 or len(block) < 19:
            continue

        header = block[1:18]
        kind = header[0]
        name = header[1:11].decode("latin1").strip("\x00 ")
        size = struct.unpack("<H", header[11:13])[0]
        if kind != 4 or size != 6912:
            continue

        payload_length = struct.unpack("<H", data[index:index + 2])[0]
        index += 2
        payload = data[index + 1:index + 1 + 6912]
        index += payload_length
        out = os.path.join(out_dir, f"{count:02d}_{name.replace(' ', '_') or 'screen'}.png")
        write_png(out, payload)
        print("wrote", out)
        count += 1

    print(f"{count} screen(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
