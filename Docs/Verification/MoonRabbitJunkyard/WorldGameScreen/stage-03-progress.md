# 3단계 실행 기록

계획: [에디터 게임 실행](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-plan.md)

상태: **3단계 구현·검증 완료**. 최종 자동 확인 202 PASS, 독립 검토 통과. 4단계는 시작하지 않았다.

## 실행 판단

- Ruling: 기존 `work` 체크아웃과 열려 있는 ServeredMeridian Editor에서 진행한다. 사용자 지정 프로젝트와 실제 편집 상태 보존을 위해 새 체크아웃/두 번째 Unity 실행을 하지 않는다. 자동 커밋하지 않는다.
- Ruling: 실행 기록은 이 문서에 보존하고 임시 검사 산출물은 `Logs/PuzzleEditorLaunchVerification`에 둔다. 커밋을 만들지 않는 작업이므로 완료 뒤에도 검증 기록을 유지한다.
- Pre-flight: 작업 1 Capture/CreateDefinition → 작업 2 SessionState 전달: bytes/번호/시드 고정, 사본 소유권을 세션에 한 번 이전한다.
- Pre-flight: 작업 2 IsBusy/Launch/Finished → 작업 3 UI: 창 InstanceID로 살아 있는 원래 창만 복귀한다.
- Pre-flight: 작업 1 세션 → 작업 4 회귀: 기존 번호 초기화 경로와 Prepare를 공유하며 기본 Start 경쟁을 막는다.

## 진행

- 작업 1: `RunData` 9개 PASS. 요청 타입이 없을 때 실패한 `data-red.txt`를 보존했다. 에셋 미저장 이동 수 37, 캡처 뒤 변경 49, 원본 컬렉션 비공유, 실제 생성 팩 입력, 누락·손상·번호 누락, 팩 변경 뒤 사본 고정, 50/51 경계를 확인했다. 세션/취소 검사는 진행 중이다.
- 작업 2: 요청 실행기 누락 RED → 첫 실제 도메인 리로드 왕복 GREEN(37 이동 사본, 원래 시작 씬 복원). 두 레벨 × 두 입력 × 리로드 켜짐/꺼짐 확장 검사를 진행 중이다.
- 작업 3: 버튼 누락 RED → `RunUI` 3개 PASS. 기존 플레이 테스트, 별도 게임 실행 버튼, 기본 입력/시드, 선택 없음 비활성화를 확인했다. 실제 버튼 캡처는 대기 중이다.
- 작업 4: 대기.

## 검사 중 발견

- 확장 왕복 검사의 `SetLevel`을 비공개 reflection으로 찾던 검사 오류를 수정했다. 실제 메서드는 public이므로 직접 호출한다. 제품 코드 실패로 분류하지 않는다.
- Ruling: 저장된 실제 레벨은 1개뿐이므로 두 레벨 교차 검사는 900001/900002 번호의 임시 사본과 검사 전용 팩으로 수행한다. 원본 Level_01/실제 팩의 해시·메모리·dirty 상태는 별도로 비교한다. 임시 팩은 검사 후 제거한다.

## 현재 확인한 결과

- 첫 전체 왕복 검사: 113 PASS. 리로드 켜짐/꺼짐, 900001/900002, 에셋 37회 이동/팩 23회 이동, 독립 시작 보드 기준, 미저장 씬·편집값·dirty 상태, 시작 씬 override, 중복 실행 차단, 진입 취소, 로드 중 종료, 닫힌 창 미재생성, 사본 해제, 다음 일반 Play 레벨 1/시드 12345를 확인했다.
- 버튼 경유와 창 소스/시드의 리로드 후 보존을 추가한 최종 왕복 검사를 실행 중이다.
- `RunRejectedInputs`: 선택 없음, 번호 0, 게임 씬 누락 3 PASS. 씬 누락 검사는 AssetDatabase로 잠시 경로를 옮겨 확인한 뒤 원래 경로와 GUID를 복원했다.
- `PuzzleGameplayVerification.Run`: 29 PASS.
- `PuzzleGameSceneVerification.Run`: 16 PASS. 실제 Input System 교환, 로켓 스프라이트, 발동, 씬 재진입을 포함한다.
- `BoardArtworkLifecycleVerification.Run`: 5 PASS. Play 종료 뒤 재컴파일·데이터 재로드 없이 편집 이미지가 복구됐다.
- 버튼/소스·시드 리로드 보존을 추가한 왕복 검사: 129 PASS.
- 직접 마우스 조작: 에셋 입력 1레벨/12345로 실행, 4행 8열↔5행 8열 교환 후 이동 20→19, 로켓 이미지 생성, 로켓 클릭 후 이동 19→18과 연쇄 처리를 확인했다.
- 정상 Play 종료를 진입 취소로 표시하는 결함 발견: `CheckCancelledEntry`가 종료 리로드 중 먼저 실행됐다. `NormalExitIsNotReportedAsCancellation`의 실제 왕복 RED를 확인하고, 미소비 bytes가 있는 요청만 취소 감시하도록 수정했다. 최종 왕복 재검사 중이다.

## 화면 증거

- [실행 전 편집창](../../../../Logs/PuzzleEditorLaunchVerification/editor-before.jpg)
- [선택 에셋으로 게임 진입](../../../../Logs/PuzzleEditorLaunchVerification/game-entered.jpg)
- [교환 후 로켓 생성](../../../../Logs/PuzzleEditorLaunchVerification/game-created-rocket.jpg)
- [로켓 발동 후 연쇄](../../../../Logs/PuzzleEditorLaunchVerification/game-after-power.jpg)

검사 환경: 실행 중인 ServeredMeridian Unity 6000.3.10f1 Windows Editor. 플레이어 빌드·Android/iOS 실기기는 이번 범위에서 실행하지 않았다.

## 최종 검증·검토

- 최종 자동 확인: **202 PASS / 0 FAIL**. 입력 9, UI 3, 오류 입력 3, Play 왕복 137, 게임 규칙 29, 실제 씬 16, 편집 이미지 수명 5.
- 정상 종료 안내 회귀: `normal-exit-red.txt`의 실패 → 최종 `lifecycle-results.txt`의 8개 정상 종료 메시지 확인으로 GREEN.
- 에셋/MemoryPack 모두 실제 마우스로 게임 플레이 버튼을 눌러 진입했다. MemoryPack 실행 종료 뒤 편집 보드의 기존 배치 이미지와 정상 복귀 안내를 확인했다.
- 독립 읽기 전용 최종 리뷰: Critical/Important/Minor 0. 리뷰는 코드·최종 검사 로그·편집/로켓 캡처를 확인했으며 별도 Unity 실행이나 파일 수정은 하지 않았다.
- Final: Ruling: 시작 보드 구성/아틀라스 실패는 기존 세션의 실패 HUD에서 확인하고 사용자가 Stop하는 동작을 유지한다. 전송 구조 오류는 실행기가 종료한다. 기존 실패 진단 흐름을 보존하며, 자동 복귀가 필요하면 후속 UX 변경이 필요하다.
- Final: Ruling: 플레이어 빌드·Android/iOS 실기기, Editor 비정상 종료 복구, 4단계 HUD/아이템/일시정지/다시하기/상세 애니메이션은 명시된 제외 범위를 유지한다. 해당 환경·기능을 검증했다고 주장하지 않는다.
- Final: Ruling: 리뷰 시 남아 있던 문서 상태·MemoryPack/복귀 캡처·임시 도구 제거는 최종 전달 감사에서 확인한다. 이를 별도 미완료 기능으로 넘기지 않는다.

### 완료 조건별 증거

| 완료 조건 | 확인 근거 |
| --- | --- |
| 컴파일·기존 플레이 테스트 유지 | Unity 컴파일, UI 검사, 기존 버튼 구현 보존 |
| 두 레벨 × 두 소스·선택 번호·시드 | 왕복 case 0~7, 기준 시작 보드 비교 |
| 미저장 에셋/마지막 생성 팩 구분 | 에셋 37회 / 팩 23회 이동, 실제 생성 팩 읽기 검사 |
| 사본 분리·원본 보존 | 참조 분리, 실제 원본 JSON/dirty/파일 해시 비교 |
| 리로드 켜짐/꺼짐·단일 초기화 | case 0~3 / 4~7, 선택 사본 결과·단일 세션·요청 소비 |
| 잘못된 입력·취소·override 보존 | 오류 입력 3, 입력 검사, case 10 및 전체 override 비교 |
| 중복·로드 중 종료·사본 정리·일반 Play | DoubleLaunchRejected, case 8~11, SnapshotDestroyedOnExit |
| 미저장 씬·레벨·이미지 복구 | 모든 왕복 Scene 목록/활성/dirty 비교, 이미지 수명 5 PASS |
| 교환·파워 이미지·발동 | 실제 씬 16 PASS, 수동 교환 및 로켓 발동 캡처 |
| 레벨 직접 참조 없음·분할 유지 | 기존 실제 씬 의존성 검사, 원본 팩 해시, 패키지/그룹 변경 없음 |
| 화면 증거·사용법·미검증 범위 | 아래 캡처, 사용 가이드, 제외 환경 기록 |

추가 캡처:
- [MemoryPack 선택 안내](../../../../Logs/PuzzleEditorLaunchVerification/editor-memorypack.jpg)
- [MemoryPack 게임 실행](../../../../Logs/PuzzleEditorLaunchVerification/game-memorypack.jpg)
- [정상 복귀와 편집 이미지](../../../../Logs/PuzzleEditorLaunchVerification/editor-returned.jpg)

## 전달 감사

- 작업 1~4 완료. 최종 결과 파일 7개에서 202 PASS / 0 FAIL을 확인했다.
- 임시 StageThreeLiveRunner.cs와 meta를 제거하고 Unity 최종 재컴파일을 확인했다. 검사 전용 팩도 남아 있지 않다.
- 기존 플레이 테스트 버튼을 직접 클릭하여 시작 조건 통과·이미지 보드 준비를 확인하고 편집 탭으로 돌아왔다. [기존 플레이 테스트 캡처](../../../../Logs/PuzzleEditorLaunchVerification/existing-play-test.jpg)
- 씬/프리팹/패키지/Addressables/EditorSettings에 최종 변경이 없고 신규 스크립트 meta가 존재한다. 변경은 실행기·세션 진입점·편집창·검사·문서에 한정된다.
- 자동 커밋/푸시 없이 현재 체크아웃에 남긴다. 문서 링크·git diff --check는 전달 직전 최종 검사한다.

최종 전달 감사 완료: final-audit.json에서 202 PASS / 0 FAIL, 캡처 8개, 신규 meta, 임시 파일 제거, 최종 Unity 리로드를 확인했다. 문서 로컬 링크 902개 오류 0, git diff --check 통과. 씬/프리팹/데이터/Addressables/패키지/프로젝트 설정의 최종 diff는 없다.
