"""Compose a 3x2 animation source grid by selecting cells from source grids."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def cell_box(size: tuple[int, int], index: int) -> tuple[int, int, int, int]:
    width, height = size
    row, column = divmod(index, 3)
    return (
        column * width // 3,
        row * height // 2,
        (column + 1) * width // 3,
        (row + 1) * height // 2,
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--left-cycle", required=True, type=Path)
    parser.add_argument("--right-cycle", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    left_source = Image.open(args.left_cycle).convert("RGB")
    right_source = Image.open(args.right_cycle).convert("RGB")
    if left_source.size != right_source.size:
        raise ValueError("Source grids must have the same dimensions")

    # Left contact -> left recoil -> left passing, then the matching right half.
    selection = (
        (left_source, 0),
        (left_source, 1),
        (left_source, 3),
        (right_source, 0),
        (right_source, 1),
        (right_source, 3),
    )
    output = Image.new("RGB", left_source.size, "white")
    for destination_index, (source, source_index) in enumerate(selection):
        destination_box = cell_box(output.size, destination_index)
        cell = source.crop(cell_box(source.size, source_index))
        output.paste(cell, destination_box)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    output.save(args.output, optimize=True)


if __name__ == "__main__":
    main()
