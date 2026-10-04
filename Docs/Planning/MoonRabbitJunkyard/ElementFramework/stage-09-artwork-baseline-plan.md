# EF-09 — 보드 표현 경로·프레임·아틀라스 대응 기준 계획

상태: **완료**, 2026-10-04. 추가1615 PASS / 0 FAIL, 기존 파워 검사 통과. 실제 관찰233건.

목표: 표현 정의 전환 전에 Editor 편집/플레이/재생 보드와 런타임 월드 보드가 사용하는 같은 종류·상태의 그림 경로, 프레임, 아틀라스 주소를 현재 기준으로 확보한다. 이번에는 매핑 조회/아트 메타데이터만 다룬다.

연결: [가이드라인](integration-guideline.md) · [EF-08 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-08-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-09-artwork-baseline-goal.md).

실행 담당: superpowers:executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 작은 작업 단위

1. 기존 작업·Editor·보호 파일을 기록하고 PuzzleArtworkPaths, LevelBoardArtwork와 실제 편집/플레이/재생/월드 보드 호출부, BoardSpriteAtlas.AddressFor와 아트/아틀라스/Addressables 메타데이터를 읽는다. 레벨을 메모리로 구성하고 원본은 저장하지 않는다.
2. 기존 PuzzleArtworkVerification.Run은 파워 경로의 파일 존재만 검사하고 배치 Editor 종료를 포함하므로 사용자 Editor에서는 호출하지 않는다. 안전한 별도 실행에서 재사용한다. BoardArtworkVerification/RabbitArtworkVerification은 UI/비동기 로드를 포함하므로 전체 Run을 임의 실행하지 않고 안전한 사례만 검토/재사용한다.
3. 일반5색·파워4종/로켓 방향·회수·장애물 내구도/색·발전기 충전 상태·덮개/바닥·벽/통로/전선/출구 등 실제 사용 중인 매핑을 조회해 원본 이미지/스프라이트와 대응한다. 제거 본체/빈칸/비활성/곰팡이 은폐의 null 계약도 확보한다. 같은 경로/키가 필요한 Editor·런타임 경계를 확인하고 차이는 실제 기록한다.
4. 파워/드론/발전기 등 현재 프레임 명칭·순서·sprite rect/피벗/크기와 BoardSpriteAtlas 주소/등록·패커블 관계를 읽기 전용으로 확인한다. 텍스처 전체 크기와 개별 프레임 크기를 구분하고 기존 확정 파일/GUID를 보존한다. 아틀라스 재패킹·이미지 생성/수정·빌드·실제 번들 로드는 하지 않는다.
5. 누락 검사/관찰만 Editor 전용 Baseline.cs/meta로 추가한다. 입력 종류/상태·좌표·경로·프레임·주소·GUID/이미지 메타데이터·null/일치/차이를 Logs/ElementFramework/Stage09에 기록한다. 별도 Editor 검사의 종료 코드/필수 FAIL·예외0과 원본 해시 보존을 확인한다.
6. stage-09-progress.md와 계획/목표를 갱신하고 실제 결과로 EF-10을 정한다. 기본 후보는 리소스 준비 집합·풀 수명 기준이며 봇 공개 관찰과 한 단계로 합치지 않는다. 다음 문서/명령문만 제공하고 EF-10 구현은 하지 않는다.

## 제약·위험

생산 규칙·매핑·이미지/프리팹/씬/아틀라스·Addressables·저장 포맷·GUID·기존 작업을 보존한다. 빌드·임의 커밋·사용자 Unity 종료·씬 저장 금지. 리소스 준비/풀 전환·새 표현 정의·시각 디자인 변경·드론 비행·UI·봇은 제외한다.

파일 존재나 주소 등록을 실제 화면 렌더링/번들 로드 성공으로 표현하지 않는다. 매핑 조회가 비동기 아틀라스 로드를 요구하면 안전한 별도 Editor/읽기 전용 경로를 먼저 판단하고 현재 단계 범위를 넘어 새 로딩 경로를 만들지 않는다. 필수 실패는 원인과 수정 경계를 보고하며 생산 수정을 임의 확대하지 않는다.

실제 결과: [EF-09 완료 보고](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-09-progress.md). 유효 Editor 경로는 로드 금지에 따라 소스/호출부 대조, 런타임 경로·Sprite/atlas 메타데이터와 Editor null은 직접 검사. 기존 원본1254px3개/바닥 차이는 보존하고 보고했다. EF-10은 리소스 준비/소유권만 상세화하며 풀은 분리한다.
