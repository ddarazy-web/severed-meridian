"""Generate six explicit front-walk pose guides for image-reference generation."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw


OUTPUT = Path(
    r"C:\Projects\Git\ServeredMeridian\ArtSource\Production\Characters\Candidates\ManualAssembly\PoseGuides"
)


POSES = (
    # name, body_y, left_hand, right_hand, left_knee, left_foot, right_knee, right_foot
    ("01_left_contact", 0, (185, 315), (325, 340), (220, 370), (190, 465), (288, 365), (315, 425)),
    ("02_left_recoil", 12, (195, 337), (315, 342), (225, 385), (205, 465), (285, 390), (305, 452)),
    ("03_right_passing", 0, (205, 330), (305, 330), (230, 380), (220, 465), (282, 355), (292, 420)),
    ("04_right_contact", 0, (185, 340), (325, 315), (224, 365), (200, 425), (292, 370), (322, 465)),
    ("05_right_recoil", 12, (195, 342), (315, 337), (227, 390), (205, 452), (287, 385), (307, 465)),
    ("06_left_passing", 0, (205, 330), (305, 330), (230, 355), (218, 420), (282, 380), (292, 465)),
)


def joint(draw: ImageDraw.ImageDraw, point: tuple[int, int], color: tuple[int, int, int, int]) -> None:
    x, y = point
    draw.ellipse((x - 8, y - 8, x + 8, y + 8), fill=color)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    contact = Image.new("RGBA", (768, 512), (248, 246, 239, 255))
    for index, (name, body_y, left_hand, right_hand, left_knee, left_foot, right_knee, right_foot) in enumerate(POSES):
        image = Image.new("RGBA", (512, 512), (248, 246, 239, 255))
        draw = ImageDraw.Draw(image)
        head_center = (256, 110 + body_y)
        shoulder_left = (220, 195 + body_y)
        shoulder_right = (292, 195 + body_y)
        hip_left = (232, 305 + body_y)
        hip_right = (280, 305 + body_y)

        draw.line((128, 465, 384, 465), fill=(40, 40, 40, 255), width=4)
        draw.ellipse(
            (head_center[0] - 62, head_center[1] - 62, head_center[0] + 62, head_center[1] + 62),
            outline=(55, 55, 55, 255),
            width=10,
        )
        draw.polygon(
            (shoulder_left, shoulder_right, hip_right, hip_left),
            outline=(55, 55, 55, 255),
            fill=(190, 190, 185, 255),
        )

        left_color = (54, 105, 180, 255)
        right_color = (190, 70, 55, 255)
        draw.line((shoulder_left, left_hand), fill=left_color, width=18)
        draw.line((shoulder_right, right_hand), fill=right_color, width=18)
        draw.line((hip_left, left_knee, left_foot), fill=left_color, width=24, joint="curve")
        draw.line((hip_right, right_knee, right_foot), fill=right_color, width=24, joint="curve")
        draw.line((left_foot[0] - 15, left_foot[1], left_foot[0] + 18, left_foot[1]), fill=left_color, width=12)
        draw.line((right_foot[0] - 15, right_foot[1], right_foot[0] + 18, right_foot[1]), fill=right_color, width=12)

        for point in (shoulder_left, hip_left, left_knee, left_hand, left_foot):
            joint(draw, point, left_color)
        for point in (shoulder_right, hip_right, right_knee, right_hand, right_foot):
            joint(draw, point, right_color)

        image.save(OUTPUT / f"pose_{name}.png", optimize=True)
        preview = image.resize((256, 256), Image.Resampling.LANCZOS)
        contact.alpha_composite(preview, ((index % 3) * 256, (index // 3) * 256))
    contact.save(OUTPUT / "pose_guides_contact.png", optimize=True)


if __name__ == "__main__":
    main()
