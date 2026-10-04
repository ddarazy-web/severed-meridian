# EF-09 — 보드 표현 경로·프레임·아틀라스 대응 기준 결과

상태: **완료**, 2026-10-04(KST). EF-10 문서 준비, 구현 미착수.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-09-artwork-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-09-artwork-baseline-goal.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경·실제 실행

Assets/Scripts/Features/GameScreen/Editor/Tests/ArtworkBaselineVerification.cs와 meta만 추가했다. 생산 규칙·표현 코드·원본 아트/프리팹/씬/아틀라스·Addressables·저장 포맷·기존 작업은 변경하지 않았다.

Unity6000.3.10f1 별도 배치 Editor에서 기존 GameScreen.Editor.PuzzleArtworkVerification.Run 통과(파워4종×2방향8경로, 결과 요약1줄), 종료0. 추가 Levels.Editor.ArtworkBaselineVerification.Run은 **1615 PASS / 0 FAIL**, 종료0. 증거: Logs/ElementFramework/Stage09/power.log, power-results.txt, power-execution.json, baseline.log, baseline-results.txt, baseline-execution.json.

artwork-observations.jsonl 실제 기록 **233건**: 고유 이미지231개와 null/frame 계약2건. 경로별 입력 상태/설명·주소·GUID·패커블·import된 크기·sprite rect/피벗/PPU를 기록했다. 같은 이미지가 여러 입력으로 조회되면 검사 자체는 실행하고 관찰은 고유 경로로 중복 제거한다. 입력 Snapshot은 읽기용 문자열이며 내부 JSON이 아니다. image-source-sizes.json에 PNG IHDR에서 읽은231개 원본 크기와 import 크기/드론 프레임 영역을 별도로 기록했다.

## 요구사항 대응·검증 방법

| 요구사항 | 실제 근거 |
| --- | --- |
| 편집/플레이/재생/월드 호출부 | mapping-source-evidence.json의11개 실제 소스/해시/줄 번호. LevelBoardView/LevelFlowOverlay는 LevelBoardArtwork, 플레이/초기 후보/재생은 RuntimeBoardArtwork.Bind→같은 LevelBoardArtwork, 월드는 PuzzleWorldBoard.Draw→PuzzleArtworkPaths/Get |
| 상태별 경로·원본 대응 | 런타임 메서드 직접 조회. 일반5색·파워/로켓 방향·회수·상자1~6/고철1~5/회수캡슐1~5/색잠금5색1~3/철근상자5색1~9/발전기필요3~5 각충전0~필요량·거미줄/먼지1~3·곰팡이·바닥/벽/통로/전선/단자/출구·미션 그림 |
| null/무변경 | Empty/색 없는 일반/비활성/곰팡이의 Content=null, 덮개 없음/Dust0=null, 제거된 비발전기 본체=null. 파워/본체/곰팡이 조회 전후 Snapshot 동일. Editor 무로드 null 메서드 직접 호출 및 RequestedAtlasCount 무증가 |
| 이미지·프레임 메타데이터 | AssetDatabase.LoadAllAssetsAtPath의 실제 Sprite rect/피벗/PPU·Texture 크기. PNG 원본 크기 별도. 로켓 기본→2→3→4, 충전01~04, 효과 프레임 명칭/순서와 드론 전체 시트/마스크 순서는 실제 Playback 소스 대응 |
| 아틀라스 대응 | 실제 BoardSpriteAtlas.AddressFor 호출, 이미지/부모 폴더 GUID의 packable YAML 포함, 실제 Addressables 아틀라스 entry.address 동일. 재패킹/번들 로드 없음 |
| 보존 | 시작 보호1785파일 SHA256 기존 변경0, 신규 검사/meta2개만 추가. 빌드·임의 커밋·씬 저장·사용자 Unity 종료·이미지 생성/수정·아틀라스 재패킹 없음 |

유효한 Editor 이미지 메서드는 호출하면 Addressables 로드를 시작하므로 **유효 Editor 매핑은 경로 구성 소스와 실제 호출부를 대조했다. 해당 메서드의 동적 이미지 로드는 실행하지 않았다.** 런타임 경로 조회와 원본 Sprite/atlas/address metadata는 직접 실행했다. Editor와 런타임의 유효 종류/색/내구도/충전 범위에서 토끼·파워·장애물·층·장치의 같은 원본 키를 사용하는 것을 소스로 확인했다. 이를 실제 Editor 화면/번들 렌더링 성공으로 표현하지 않는다.

## 차이·남은 문제

1. **바닥 표현 차이**: 편집 보드는 LevelBoardArtwork.Floor에서4개 사분면별 center/edge/outer/inner 타일을 선택한다. 현재 월드 보드의 Draw는 PuzzleArtworkPaths.Floor(center)를 쓴다. 실제 테두리/모서리 타일 원본도 존재하며 대응을 기록했다. 이번에 통일하지 않았다.
2. **원본 PNG 크기 예외3개**: PowerBlocks/moon-bomb-v1, collection-drone-v1, rainbow-magnet-v1은1254×1254로2의 승수가 아니다. 실제 import는256×256이다. 원본 크기 규칙의 기존 미충족 사항이며 새 검사 오류는 아니다. 원본 보존 범위라 수정/재생성하지 않았다. 향후 아트 정리 작업에서 별도 처리해야 한다.
3. 드론 collection-drone-rotor-4frames-v1은원본/import512×512, 단일 전체 Sprite다. 프레임 영역은256×256이며0상단왼쪽→1상단오른쪽→2하단왼쪽→3하단오른쪽을 마스크/전체 시트 위치 이동으로 보여준다. 독립 Sprite4개로 저장된 것으로 오해하지 않는다. .06초 순환과 시트2배 크기 표시를 소스로 확인했다.
4. 유효하지 않은 입력 경계는 동일하지 않다. Editor는 색/내구도 범위를 검사하고 발전기 충전을 clamp한다. 런타임 경로는 유효한 상태를 전제로 문자열을 구성한다. 예컨대 Web0 경로 구성과 Editor null이 다를 수 있다. 현재 유효 상태 기준과 입력 검증 경계를 구분했고 생산 정책을 바꾸지 않았다.

## 검토·한계

추가 검사는 최초 통과 후 입력 상태/GUID 유효성 기록을 보완하고 최종 전체를 재실행했다. 중간 통과 자료는 pre-input-record-*로 보존했다. 필수 FAIL/예외0, GUID/해시/diff/문서 링크·호출부·상태 범위·frame 순서를 자체 검토했다. 계획에 따라 별도 요청 없는 병렬 에이전트는 사용하지 않았다.

Play Mode·실기기·화면/겹침/마스크 렌더링·실제 번들 로드는 미실행이다. UI 비동기 전체 BoardArtworkVerification/RabbitArtworkVerification은 이번 메타데이터 범위에서 실행하지 않았다. Sprite 읽기가 원본 텍스처/아틀라스 등록과 대응한다는 근거이며 runtime Addressables 로드/메모리 성능을 증명하지 않는다. 검사 범위의 미해결 필수 실패는 없고 위의 기존 표현/원본 크기 차이는 그대로 남았다. 새 표현 정의·드론 비행·리소스 준비/풀·봇 전환은 미구현이다.

## 다음 단계

[EF-10 리소스 준비 기준 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-10-resource-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-10-resource-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-10-command.md).

한 단계 크기를 유지하기 위해 리소스 준비와 소유권만 다음에 다룬다. 렌더러/효과 오브젝트 풀과 봇 공개 관찰은 별도 후속 단계 후보로 남기며 상세 문서는 아직 작성하지 않는다. EF-10 구현 미착수.
