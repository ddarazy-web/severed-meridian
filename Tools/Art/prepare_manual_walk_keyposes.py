"""Prepare six deterministic front-walk key poses for manual Aseprite assembly."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps

from build_nonpixel_walk_sheet import normalize_frames, remove_edge_background


def normalize_single(path: Path) -> Image.Image:
    source = Image.open(path)
    return normalize_frames([remove_edge_background(source)])[0]


def mirror_lower_body(frame: Image.Image, seam_top: int = 156, seam_bottom: int = 168) -> Image.Image:
    mirrored = ImageOps.mirror(frame)
    result = frame.copy()
    mask = Image.new("L", frame.size, 0)
    values = mask.load()
    for y in range(seam_top, frame.height):
        opacity = 255 if y >= seam_bottom else round(255 * (y - seam_top) / (seam_bottom - seam_top))
        for x in range(64, 192):
            values[x, y] = opacity
    result.alpha_composite(Image.composite(mirrored, Image.new("RGBA", frame.size), mask))
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--contact-right", required=True, type=Path)
    parser.add_argument("--recoil-right", required=True, type=Path)
    parser.add_argument("--passing-right", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    contact_right = normalize_single(args.contact_right)
    recoil_right = normalize_single(args.recoil_right)
    passing_right = normalize_single(args.passing_right)

    frames = (
        ("01_left_contact", mirror_lower_body(contact_right)),
        ("02_left_recoil", mirror_lower_body(recoil_right)),
        ("03_right_passing", passing_right),
        ("04_right_contact", contact_right),
        ("05_right_recoil", recoil_right),
        ("06_left_passing", mirror_lower_body(passing_right)),
    )

    frame_dir = args.output_dir / "Frames"
    preview_dir = args.output_dir / "Preview"
    frame_dir.mkdir(parents=True, exist_ok=True)
    preview_dir.mkdir(parents=True, exist_ok=True)

    strip = Image.new("RGBA", (256 * len(frames), 256))
    contact = Image.new("RGBA", (768, 512), (235, 224, 199, 255))
    gif_frames: list[Image.Image] = []

    for index, (name, frame) in enumerate(frames):
        frame.save(frame_dir / f"chr_protagonist_male_stage_01_walk_down_{name}.png", optimize=True)
        strip.alpha_composite(frame, (index * 256, 0))
        contact.alpha_composite(frame, ((index % 3) * 256, (index // 3) * 256))
        gif_canvas = Image.new("RGBA", (256, 256), (235, 224, 199, 255))
        gif_canvas.alpha_composite(frame)
        gif_frames.append(gif_canvas.convert("P", palette=Image.Palette.ADAPTIVE, colors=255))

    strip.save(args.output_dir / "chr_protagonist_male_stage_01_walk_down_keyposes.png", optimize=True)
    draw = ImageDraw.Draw(contact)
    draw.line((256, 0, 256, 512), fill=(126, 112, 88, 90), width=1)
    draw.line((512, 0, 512, 512), fill=(126, 112, 88, 90), width=1)
    draw.line((0, 256, 768, 256), fill=(126, 112, 88, 90), width=1)
    contact.save(preview_dir / "chr_protagonist_male_stage_01_walk_down_keyposes_contact.png", optimize=True)
    gif_frames[0].save(
        preview_dir / "chr_protagonist_male_stage_01_walk_down_suggested_order.gif",
        save_all=True,
        append_images=gif_frames[1:],
        duration=125,
        loop=0,
        disposal=2,
        optimize=False,
    )

    manifest = {
        "status": "MANUAL_ASSEMBLY_KEYPOSES",
        "frameSize": [256, 256],
        "fpsSuggestion": 8,
        "pivotNormalized": [0.5, 0.105469],
        "kneePatches": False,
        "suggestedOrder": [name for name, _ in frames],
        "editableOrder": True,
        "notes": [
            "Frames are intentionally separate for manual ordering in Aseprite.",
            "Opposite-foot variants mirror only the lower body; waist equipment is not mirrored.",
            "Treat the GIF as a suggested order, not an approved final animation.",
        ],
    }
    (args.output_dir / "chr_protagonist_male_stage_01_walk_down_keyposes.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
