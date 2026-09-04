"""Build a normalized 4-direction walk sheet from four generated 3x2 grids."""

from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path
from statistics import median

from PIL import Image, ImageDraw


CELL_SIZE = 256
CONTENT_HEIGHT = 222
BASELINE_FROM_TOP = 229
FRAMES_PER_DIRECTION = 6
DIRECTIONS = ("down", "left", "right", "up")


def is_background(rgb: tuple[int, int, int]) -> bool:
    red, green, blue = rgb
    luminance = (red + green + blue) / 3.0
    chroma = max(rgb) - min(rgb)
    return luminance >= 220 and chroma <= 35


def remove_edge_background(source: Image.Image) -> Image.Image:
    rgb = source.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()
    visited = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def enqueue(x: int, y: int) -> None:
        index = y * width + x
        if visited[index] or not is_background(pixels[x, y]):
            return
        visited[index] = 1
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

    rgba = Image.new("RGBA", rgb.size)
    output = rgba.load()
    for y in range(height):
        row = y * width
        for x in range(width):
            if visited[row + x]:
                output[x, y] = (0, 0, 0, 0)
            else:
                red, green, blue = pixels[x, y]
                output[x, y] = (red, green, blue, 255)
    return rgba


def split_grid(source: Image.Image) -> list[Image.Image]:
    transparent = remove_edge_background(source)
    width, height = transparent.size
    frames: list[Image.Image] = []
    for row in range(2):
        top = row * height // 2
        bottom = (row + 1) * height // 2
        for column in range(3):
            left = column * width // 3
            right = (column + 1) * width // 3
            frames.append(transparent.crop((left, top, right, bottom)))
    return frames


def keep_largest_component(frame: Image.Image) -> Image.Image:
    alpha = frame.getchannel("A")
    width, height = alpha.size
    values = alpha.load()
    visited = bytearray(width * height)
    components: list[list[tuple[int, int]]] = []

    for start_y in range(height):
        for start_x in range(width):
            start_index = start_y * width + start_x
            if visited[start_index] or values[start_x, start_y] == 0:
                continue
            visited[start_index] = 1
            queue: deque[tuple[int, int]] = deque([(start_x, start_y)])
            component: list[tuple[int, int]] = []
            while queue:
                x, y = queue.popleft()
                component.append((x, y))
                for neighbor_x, neighbor_y in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if not (0 <= neighbor_x < width and 0 <= neighbor_y < height):
                        continue
                    index = neighbor_y * width + neighbor_x
                    if visited[index] or values[neighbor_x, neighbor_y] == 0:
                        continue
                    visited[index] = 1
                    queue.append((neighbor_x, neighbor_y))
            components.append(component)

    if not components:
        return frame
    keep = set(max(components, key=len))
    cleaned = frame.copy()
    cleaned_alpha = cleaned.getchannel("A")
    cleaned_values = cleaned_alpha.load()
    for y in range(height):
        for x in range(width):
            if (x, y) not in keep:
                cleaned_values[x, y] = 0
    cleaned.putalpha(cleaned_alpha)
    return cleaned


def normalize_frames(frames: list[Image.Image]) -> list[Image.Image]:
    frames = [keep_largest_component(frame) for frame in frames]
    boxes = [frame.getchannel("A").getbbox() for frame in frames]
    if any(box is None for box in boxes):
        raise ValueError("A generated grid cell does not contain a visible character")
    heights = [box[3] - box[1] for box in boxes if box is not None]
    scale = CONTENT_HEIGHT / median(heights)

    normalized: list[Image.Image] = []
    for frame, box in zip(frames, boxes, strict=True):
        assert box is not None
        cropped = frame.crop(box)
        target_width = max(1, round(cropped.width * scale))
        target_height = max(1, round(cropped.height * scale))
        resized = cropped.resize((target_width, target_height), Image.Resampling.LANCZOS)

        cell = Image.new("RGBA", (CELL_SIZE, CELL_SIZE))
        x = round((CELL_SIZE - target_width) / 2)
        y = BASELINE_FROM_TOP - target_height
        cell.alpha_composite(resized, (x, y))
        normalized.append(cell)
    return normalized


def build_preview(frames_by_direction: dict[str, list[Image.Image]], destination: Path) -> None:
    preview_frames: list[Image.Image] = []
    placements = {
        "down": (0, 0),
        "left": (CELL_SIZE, 0),
        "right": (0, CELL_SIZE),
        "up": (CELL_SIZE, CELL_SIZE),
    }
    for frame_index in range(FRAMES_PER_DIRECTION):
        canvas = Image.new("RGBA", (CELL_SIZE * 2, CELL_SIZE * 2), (235, 224, 199, 255))
        draw = ImageDraw.Draw(canvas)
        draw.line((CELL_SIZE, 0, CELL_SIZE, CELL_SIZE * 2), fill=(126, 112, 88, 90), width=1)
        draw.line((0, CELL_SIZE, CELL_SIZE * 2, CELL_SIZE), fill=(126, 112, 88, 90), width=1)
        for direction, position in placements.items():
            canvas.alpha_composite(frames_by_direction[direction][frame_index], position)
        preview_frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))

    destination.parent.mkdir(parents=True, exist_ok=True)
    preview_frames[0].save(
        destination,
        save_all=True,
        append_images=preview_frames[1:],
        duration=125,
        loop=0,
        disposal=2,
        optimize=False,
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    for direction in DIRECTIONS:
        parser.add_argument(f"--{direction}", required=True, type=Path)
    parser.add_argument("--sheet", required=True, type=Path)
    parser.add_argument("--preview", required=True, type=Path)
    parser.add_argument("--metadata", required=True, type=Path)
    parser.add_argument("--id", default="chr_protagonist_male_stage_01_walk_candidate_01")
    args = parser.parse_args()

    frames_by_direction: dict[str, list[Image.Image]] = {}
    for direction in DIRECTIONS:
        source = Image.open(getattr(args, direction))
        frames_by_direction[direction] = normalize_frames(split_grid(source))

    sheet = Image.new(
        "RGBA",
        (CELL_SIZE * FRAMES_PER_DIRECTION, CELL_SIZE * len(DIRECTIONS)),
    )
    for row, direction in enumerate(DIRECTIONS):
        for column, frame in enumerate(frames_by_direction[direction]):
            sheet.alpha_composite(frame, (column * CELL_SIZE, row * CELL_SIZE))

    args.sheet.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.sheet, optimize=True)
    build_preview(frames_by_direction, args.preview)

    metadata = {
        "id": args.id,
        "status": "PRODUCTION_CANDIDATE_REVIEW_REQUIRED",
        "cellSize": [CELL_SIZE, CELL_SIZE],
        "sheetSize": list(sheet.size),
        "directions": list(DIRECTIONS),
        "framesPerDirection": FRAMES_PER_DIRECTION,
        "fps": 8,
        "rowOrder": list(DIRECTIONS),
        "pivotNormalized": [0.5, round((CELL_SIZE - BASELINE_FROM_TOP) / CELL_SIZE, 6)],
        "filterMode": "Bilinear",
        "alpha": "RGBA",
        "buildIncluded": False,
        "reference": "ArtSource/Concepts/Characters/chr_protagonist_male_stage_01_walk_down_concept_01.png",
    }
    args.metadata.parent.mkdir(parents=True, exist_ok=True)
    args.metadata.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
