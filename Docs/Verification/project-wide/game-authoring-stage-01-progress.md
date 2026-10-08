# 게임·제작 도구 분리 1단계 — 구현·검증 기록

2026-10-08 · 1단계 구현·검증 완료

## 구현 결과

- PuzzlePlayRequest는 레벨 스냅샷·번호·시드·표현 DTO를 저장한다. 입력 바이트와 재귀 표현 데이터는 복사하며 CreateDefinition/CreateVisuals도 독립 사본을 반환한다.
- PuzzlePlayContext는 제품 플랫폼과 별개로 정식 실행/시험 실행을 구분한다. 시험 기본값은 메모리 기반 튜토리얼 기록과 다음 레벨 이동 금지이며, 팩 시험은 기존과 같이 다음 레벨 이동을 명시적으로 허용한다.
- CreateTest에 정식 PlayerPrefs 공급자를 전달하면 거절한다. 기존 Editor의 SessionState 완료·학습 ID 콜백은 재사용한다.
- PuzzleGameSession의 공통 InitializeAsync(request, context, token)는 첫 await 전에 실행권을 확보한다. 요청/문맥 누락이나 잘못된 스냅샷이 정식 기본 레벨 로딩으로 우회하지 않는다. 실패/취소는 호출자에게 전달하며 기존 int/ownedDefinition 오버로드의 동작은 보존한다.
- PuzzleEditorLauncher는 기존 파일·SessionState·씬 왕복 역할을 유지하며 새로운 공통 진입을 호출한다. 정상 종료로 발생한 취소는 실패 예외로 보고하지 않는다.
- 기존 퍼즐 규칙, 렌더링, 레벨/요소 원본, 프로필, 배포 형식은 변경하지 않는다.

구조:

    Unity 에디터 파일/씬 어댑터 ─┐
                                ├→ PuzzlePlayRequest + PuzzlePlayContext
    향후 공통 레벨툴 씬 ───────┘                 ↓
                                        PuzzleGameSession
                                                ↓
                              기존 퍼즐 실행·보드·튜토리얼
                                                ↓
                            정식 완료 저장 / 시험 전용 완료 저장

## 작업 판단과 범위

- 기존 work 체크아웃을 유지했다. 사용자의 브랜치 유지·커밋 금지 지시에 따라 새 체크아웃이나 커밋을 만들지 않았다.
- 기존에 실제 영속 저장이 연결된 부분은 튜토리얼 완료·학습 ID 승계다. 미연결된 아이템 경제나 SDK를 새 추상화/가짜 서비스로 만들지 않았다. [기능·의존 조사](game-authoring-stage-01-dependency-audit.md) 참조.
- 새 asmdef는 보류했다. 기본 어셈블리에 남은 Levels/Elements/Simulation/Tutorial 의존으로 광범위 이동이 필요하기 때문이다. Runtime 공개 계약과 Editor 폴더 경계를 먼저 확보했고, 데이터/편집 분리 단계에서 실제 필요한 어셈블리 경계를 적용한다.
- 기존 왕복 검사가 팩1 전용 Encode로 튜토리얼 레벨을 저장하려 해 실패했다. 일반 왕복 시험 데이터는 튜토리얼을 명시적으로 비우고 Snapshot으로 저장한다. 정식 기본 레벨 비교는 실제 튜토리얼 시드 및 고정 공급을 반영한다.
- 기존 수명주기 검사의 Run은 배치 종료를 수행하지 않아 명시적 배치 래퍼 RunLifecycle을 추가했다. 실패 시 기존 검사 정리가 끝나기 전에 종료하지 않는다. 기존 Run 메뉴 진입은 보존한다.
- 독립 검토에서 로딩 시작 직후 취소만으로는 아틀라스 해제 증거가 부족함을 확인했다. 실제 Addressables InternalId 변환 시점에 취소를 주입해 artwork.pending, 소유 핸들, Changed 구독 정리를 확인하도록 보강했다.
- 정상 로딩 종료가 실패 메시지로 나타나는 회귀를 추가 검사로 재현한 뒤 취소 분기로 수정했다.
- 기능 격리를 넘어선 플랫폼 SDK/네이티브 플러그인·콘텐츠 포함 검사는 6단계다. 이번 소스 검사를 플랫폼별 배포 검증으로 표현하지 않는다.

## 검증 실행과 증거

검증 원본은 Tests/Editor에 두고 ProjectTests.ps1로 일시 연결했다. Player와 Addressables 콘텐츠 빌드는 호출하지 않는다.

| 검사 | 명령의 -Method 값 | 확인 범위 |
|---|---|---|
| 공통 계약/실제 세션 | GameScreen.Editor.PuzzlePlayBoundaryVerification.Run | 스냅샷·표현 사본, 모드, 기존 경로 동등성, 취소/실패, 재시작/아이템, 저장·리소스 격리 |
| 기존 에디터 12가지 왕복 | GameScreen.Editor.PuzzlePlayBoundaryVerification.RunLifecycle | 에셋/팩·시드·원본/씬/창 상태·종료·취소·반복 진입 |
| 조기 종료 회귀 | GameScreen.Editor.PuzzlePlayBoundaryVerification.RunCancellation | 정상 로딩 취소를 실패 메시지로 보고하지 않음 |
| 런타임 UI 시제품 | GameScreen.Editor.AuthoringRuntimeUIProbe.Run | 실제 Player 패널·셀 선택·속성 변경·200행 스크롤·렌더 |

원본 로그:
- Logs/GameAuthoringStage01/contract-red.txt: 새 Runtime 실행 계약이 없는 상태의 실패.
- Logs/GameAuthoringStage01/baseline-lifecycle.txt: 변경 전 기준 검사. 첫 9개 시나리오 이후 구형 기대값 문제로 실패했으므로 전체 기준 통과로 주장하지 않는다.
- Logs/GameAuthoringStage01/cancellation-red.txt: 조기 종료가 “초기화 실패”로 표시되는 재현.
- Logs/GameAuthoringStage01/boundary-results.txt: 최종 공통 세션 검사 결과.
- Logs/GameAuthoringStage01/final-lifecycle.txt: 최종 에디터 왕복 결과.
- Logs/GameAuthoringStage01/UIProbe/results.txt 및 runtime-panel.png: 시제품 검사와 렌더.

## 사용자 추가 결정과 다음 단계

레벨툴 씬 완성 후 Unity에서도 같은 씬을 기본 레벨 편집 화면으로 사용한다. 4단계에서 공통 씬과 기존 메뉴 진입을 제공하고, 5단계에서 필수 기능 동등성과 저장/미저장 보호·플레이 복귀를 검증한 뒤 전환한다. 기존 EditorWindow의 중복 편집 화면은 제거/비활성화하고 씬 실행·구형 데이터 가져오기 등 필요한 어댑터만 남긴다.

2단계는 JSON 계약과 비파괴 시험 내보내기/읽기/입력 변환이다. 아직 SO 원본과 기존 제작 UI를 교체하지 않는다.

## 남은 제한

- Windows 레벨툴 독립 씬과 앱은 아직 없다. 이번 시제품은 검사 전용이며 제품 씬으로 등록하지 않는다.
- UI 시제품은 합성 포인터/내비게이션과 스크롤을 확인한다. 실제 Windows OS 입력, 고해상도 배율, 한글 IME, 전체 편집 UX는 4/5단계 검증이다.
- Android/iOS/Steam/Windows 도구의 실제 빌드·기기 실행과 전체 콘텐츠 분리는 수행하지 않았다.
- HTML 매뉴얼·커밋·푸시는 수행하지 않았다.

## 최종 결과와 완료 조건 대응

- 공통 계약/Play Mode 검사: 69개 PASS 확인 항목, 실패 0, exit 0. 실제 아틀라스 요청 도중 취소 후 pending=0·소유 핸들 반환, 세 차례 반복 생성/파기 후 아틀라스와 Changed 구독 정리를 포함한다.
- 기존 에디터 왕복: case 0~11의 12개 시나리오 전체 완료, exit 0. 추가한 case 8의 EarlyExitIsNotFailure도 통과했다.
- 런타임 UI: 12개 PASS 확인 항목, 실패 0, exit 0. 1024×768 렌더 이미지도 직접 확인했다.
- 기존 Build Profile·데이터·씬/프리팹·ProjectSettings·Packages의 최종 diff 없음. 기존 목표/계획 문서 변경을 보존했다.
- 테스트 연결 해제. 검사 전용 levels-900001 파일이 Assets에 남지 않음을 확인했다.
- 별도 리뷰에서 Critical 제품 결함 없음. 리소스 검증 공백은 보강했고, 정상 취소 메시지 회귀는 RED→GREEN으로 수정했다. 미처리 리뷰 항목 없음.

G1: 의존 조사 문서. G2~G5: 공통 계약·세션 검사 및 실제 연결 조사. G6: 에디터 왕복의 미저장 원본/씬/창 선택 보존. G7: 취소·실패·반복·핸들·구독 검사. G8: UI 시제품과 asmdef 결정. G9: 최종 diff와 테스트 연결 상태. G10: 아래 후속 문서 및 완료 보고.

[2단계 계획](../../Planning/project-wide/game-authoring-stage-02-plan.md) · [목표](../../Goals/project-wide/game-authoring-stage-02-goal.md) · [실행문](../../Commands/project-wide/game-authoring-stage-02-command.md). 2단계 구현은 착수하지 않았다.