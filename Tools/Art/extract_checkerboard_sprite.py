"""Convert a light checkerboard-backed generated image into a transparent sprite.

This is a deterministic post-process for generated concept candidates. It only
removes light, low-chroma pixels connected to the canvas boundary, then resizes
the result without changing the character's composition.
"""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image


def is_background_candidate(rgb: tuple[int, int, int]) -> bool:
    red, green, blue = rgb
    luminance = (red + green + blue) / 3.0
    chroma = max(rgb) - min(rgb)
    return luminance >= 215 and chroma <= 20


def extract(source: Path, destination: Path, size: int) -> None:
    image = Image.open(source).convert("RGB")
    width, height = image.size
    pixels = image.load()

    background = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def enqueue(x: int, y: int) -> None:
        index = y * width + x
        if background[index] or not is_background_candidate(pixels[x, y]):
            return
        background[index] = 1
        queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        if x > 0:
            enqueue(x - 1, y)
        if x + 1 < width:
            enqueue(x + 1, y)
        if y > 0:
            enqueue(x, y - 1)
        if y + 1 < height:
            enqueue(x, y + 1)

    output = Image.new("RGBA", image.size)
    output_pixels = output.load()
    for y in range(height):
        row_offset = y * width
        for x in range(width):
            if background[row_offset + x]:
                output_pixels[x, y] = (0, 0, 0, 0)
            else:
                red, green, blue = pixels[x, y]
                output_pixels[x, y] = (red, green, blue, 255)

    output = output.resize((size, size), Image.Resampling.LANCZOS)
    destination.parent.mkdir(parents=True, exist_ok=True)
    output.save(destination, optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--size", type=int, default=256)
    args = parser.parse_args()
    extract(args.source, args.destination, args.size)


if __name__ == "__main__":
    main()
