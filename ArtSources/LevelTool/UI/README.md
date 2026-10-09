# 레벨툴 전용 UI 이미지

2026-10-09. 생성 도구: 내장 image_gen. API/자격 증명 사용 없음.

이 폴더는 생성 원본 보관용이며 Assets 밖에 있어 Unity 빌드에 포함되지 않는다.
실사용 파일: `Assets/Textures/LevelTool/UI/Sprites/`.
panel 128×128, button/input 128×64, icons 256×256. 생성 원본의 투명 여백과 크기만 Unity 준비 검사에서 정규화했다.
아이콘은 4×4 마스크 시트이며 Unity importer의 FromGrayScale 알파를 사용한다. 원본 PNG의 검정색은 실제 UI에서 투명해진다.

아틀라스: `Assets/Textures/LevelTool/UI/LevelToolUI.spriteatlasv2`.
회전/타이트 패킹 없음, 패딩 4, 최대 512, RGBA32. 패널류 border는 각 16이며 UI 표시 slice scale은 0.5다.
Addressables 주소 `LevelToolUI`, 그룹 `Level Tool UI`. atlas includeInBuild=false, 그룹은 Windows Level Editor 제품에서만 포함한다.
게임용 Board Artwork 목록에는 이 전용 폴더가 포함되지 않는다. 개별 PNG는 Addressables 엔트리나 씬 직접 참조로 등록하지 않는다.
기존 콘텐츠 빌드 훅을 재사용한다. 이번 작업은 Player/번들 빌드를 실행하지 않는다.

## 최종 생성 프롬프트

### panel
Create a production UI texture asset, NOT a mockup: exactly one empty square panel for a polished desktop level editor, dark desaturated navy-gray, subtle soft bevel and a very thin pale cool edge. Orthographic front-facing perfectly axis aligned rounded rectangle; no perspective, no text, no icons, no screws, no ornate decoration. 1024x1024 PNG transparent canvas. Panel outer bounds x=32 y=32 width=960 height=960, small corner radius 48px, gentle soft shadow contained within outer 32px margin. Central region uniform flat #263341, no gradients or marks across the center; any bevel/light edge must stay within 96px of canvas edges so this can be sliced as a 9-slice UI panel. Sophisticated understated tool UI, matches clean dark slate and mint-accent editor. Smooth clean antialiased silhouette, no visible checkerboard.

### button
Production nine-slice UI button texture, one single empty horizontal rounded rectangle, transparent background, aspect ratio 2:1. Front facing perfectly flat orthographic, not a mockup. Dark slate-blue desktop editor matching a subtle navy panel, clean understated premium tool UI. Thin cool gray edge with subtle bevel and narrow top highlight, small 6 percent corner radius, no excessive shine, no glass, no grunge, no text or icons. Flat uniform opaque #394c5d center. Button occupies 94 percent of canvas with narrow transparent padding. All bevel details confined to outer 10 percent, center must stretch cleanly. 1024x512 canvas, power-of-two dimensions. No drop shadow extending far outside. Single button only.

### input
One production UI nine-slice input-field background texture. Empty horizontal recessed rounded rectangle, dark charcoal navy center #17212c, subtle inset edge shadow and fine muted slate rim. Clean professional desktop level editor, understated and softly tactile, no dramatic glow. Front-on orthographic, absolutely no perspective, text, icons, decoration or checkerboard. Flat opaque uniform center, all details at edges. Small rounded corners. 1024x512 PNG transparent canvas, rectangle fills nearly whole canvas with only 3 percent padding. Single asset only, not a screenshot or presentation.

### icons (채택본)
A perfectly clean monochrome UI icon MASK sprite sheet, pure solid white icons (#FFFFFF) on perfectly solid black background (#000000). ZERO texture, noise, stipple, shine, shadow, color, grunge, gray decoration. It should look exactly like a simple vector icon library screenshot. Square 1024x1024 canvas, EXACT regular four columns by four rows, each icon centered in its own equal square cell, at least 20% cell padding. Uniform thick white outline strokes with rounded ends, highly legible simple geometry. Row1: open folder, floppy disk, undo curved-left-arrow, redo curved-right-arrow. Row2: play triangle, circled checkmark, search magnifying glass, mouse pointer. Row3: paintbrush, eraser, four crop/fit corner brackets, question mark inside circle. Row4: plus, minus, trashcan, clipboard. NO labels, NO grid lines, NO text except question mark. All negative space must be absolute featureless black. The black background will be used as alpha by a game texture importer.

투명 배경으로 생성한 최초 아이콘과 수정본은 점무늬 노이즈 때문에 채택하지 않았다.

## 적용·검증

- 레벨툴 좌우 패널·팝업·버튼·입력창에 9슬라이스 적용. 주요 툴바/편집 버튼은 같은 아틀라스에서 아이콘 프레임을 사용한다.
- `Logs/LevelToolUiStage01/artwork-retry.log`: SpriteAtlasV2, 4개 스프라이트, 2의 승수 크기, border, Editor 미리보기 패킹, 제품 4종 포함/제외, 게임 씬 의존성·게임 아틀라스 중복 제외 통과.
- `Logs/TestHarness/20261009-144719-813-Run.log`: 4개 해상도 및 도움말/모달, 실제 전용 아틀라스 로드·아이콘·상단 글자 폭 검사 통과.
- `Logs/TestHarness/20261009-144845-627-Run.log`: 입력 확정 저장·실제 버튼/메뉴 클릭·텍스트 클립보드·단축키·재활성화·게임 시험 복귀 회귀 통과.
- 화면: `Logs/LevelToolUiStage01/artwork-final.png`.
- 테스트 연결 해제 완료. Player/Addressables 빌드와 실제 배포 산출물 크기 검증은 실행하지 않았다. 커밋·푸시·HTML 수정 없음.
