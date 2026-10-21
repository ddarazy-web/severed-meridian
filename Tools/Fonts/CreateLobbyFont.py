"""로비 한글 폰트 재생성. Python + fonttools==4.60.1 필요.

저장소 루트에서 실행한다. 기존 원본/OFL은 보존하고 파생 폰트만 생성한다.
"""
from pathlib import Path
from fontTools import subset
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[2]
font = TTFont(root / "Assets/Fonts/MoonRabbitUI-Regular.ttf")
subsetter = subset.Subsetter(options=subset.Options())
subsetter.populate(unicodes=(
    list(range(32, 256)) + list(range(0xAC00, 0xD7A4))
    + list(range(0x3131, 0x318F)) + list(range(0x2000, 0x2070))
))
subsetter.subset(font)
for name in font["name"].names:
    if name.nameID in (1, 3, 4, 6):
        value = "MoonRabbit Lobby" if name.nameID in (1, 4) else "MoonRabbitLobby-Regular"
        name.string = value.encode(name.getEncoding())
destination = root / "Assets/Fonts/Lobby/MoonRabbitLobby-Regular.ttf"
destination.parent.mkdir(parents=True, exist_ok=True)
font.save(destination)
print(f"{destination}: {destination.stat().st_size:,} bytes")
