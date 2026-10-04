# 팝업 프레임워크 3단계 최종 검증

작성/완료일: 2026-10-03. 상태: 관리·템플릿·게임 세 팝업 전환과 최종 리뷰 처리 완료. 12개 완료 조건을 아래 증거로 확인했다. 1~2단계를 보존했으며 다른 단계를 시작하지 않았다.

[계획](../../Planning/project-wide/popup-framework-stage-03-plan.md) · [12개 완료 조건](../../Goals/project-wide/popup-framework-stage-03-goal.md) · [사용 안내](../../Guides/project-wide/popup-framework-usage.md) · [명령문 이력](../../Commands/project-wide/popup-framework-stage-03-command.md)

## 최종 실행과 원본

기준/최종 HEAD는 `89431f4e1cadc33347b037a894be19c729de1ac9`로 같다. 소유한 숨김 Unity 6000.3.10f1에서 최종 검사 22회 모두 실제 exit0, 합계 2,781 PASS·FAIL0이다. PASS 합계는 실행별 assertion 기록의 합이며 중복 검사를 포함한다. 고유 요구사항 수가 아니다.

- `Logs/PopupFramework/Stage03/final-gate-manifest.json`: 실행별 원본 결과 SHA256·작성 UTC·PASS/FAIL/exit.
- `final-gate-results.txt`: 22/22 통과 집계. 요약과 원본 PASS/FAIL을 직접 대조했다.
- `review-fix-source-hashes.json`: 검사 동안 수정 소스 37개 해시 불변.
- `baseline-files.json`, `final-original-changes.txt`, `final-resource-audit.txt`: 원본 1,825개 중 허용된 20개 변경. 기존 메타·씬·레벨·텍스처·Packages·ProjectSettings·Addressables는 기준 해시와 같다. GUID 1,015개 모두 유효·고유, 신규 에셋/메타 짝 45개 항목과 시험 경로 잔여0.
- `final-process-audit.txt`: 모든 소유 검사 핸들 정상 종료 뒤 ServeredMeridian Unity 프로세스0. 다른 프로젝트의 사용자 Unity는 건드리지 않았다.

위 파일명은 `Logs/PopupFramework/Stage03/` 기준이다. 로그 폴더는 버전 관리 제외이므로 검사 진입점과 요약은 이 문서에도 남긴다. 빌드·Addressables 콘텐츠 빌드·커밋·푸시·사용자 Unity 종료·사용자 씬 자동 저장은 수행하지 않았다.

## 마지막 소스의 검사 22회

아래 결과 파일은 `Logs/PopupFramework/<label>-results.txt`, `<label>-summary.txt`다. 모든 행은 FAIL0·실제 exit0이다.

| label | PASS | 확인 범위 |
| --- | ---: | --- |
| stage03-task1-tools | 33 | 등록 오류·읽기 전용 조회·경로/예약 이름 |
| stage03-task1-window | 15 | 실제 UITK 편집 Undo/Redo·저장·Host별 시험·오류 진단 유지 |
| stage03-task1-template | 8 | 실제 생성/컴파일/도메인 재로딩/등록/표시/캡처/충돌 |
| stage03-task1-data | 16 | 1단계 관리/중복/상태 계약 |
| stage03-task1-restoration-data | 59 | 2단계 복사·문맥·ticket·실패/호출 예제 |
| stage03-task1-input | 49 | 실제 EventSystem/입력/포커스/외부 요청 |
| stage03-task1-restoration-scene | 26 | 실제 씬 왕복·캡처 중 종료·뷰/구독 정리 |
| stage03-task1-navigation | 3 | 같은 프레임 Move/Submit/Cancel 경계 |
| stage03-task1-focus | 2 | 비활성 선택 복구 |
| stage03-task1-dynamic | 1 | 동적 버튼 Cancel |
| stage03-task1-requests | 1 | 재진입 요청 잔류0 |
| stage03-gate-game-input | 23 | 게임 중첩·HUD/보드/키 차단·외부 소유권·종료 |
| stage03-gate-restart | 13 | 실제 취소/데이터 실패 보존·성공 문맥·외부 정지 해제 |
| stage03-gate-game-restore | 22 | 결과·실제 게임 씬 왕복·미준비 연결 거부·현재 버튼 |
| stage03-gate-design | 11 | 기준 Rect/Image/Text/Button·설명 분리 시각 보존 |
| stage03-gate-stage13-scene | 140 | 실제 Next 성공/실패/취소/중복·화면·게임 상태 |
| stage03-gate-stage13-asset | 38 | Asset 실행/Retry·MemoryPack 안내·화면 |
| stage03-gate-stage13-boundaries | 31 | 50→51 구간·준비 중 실제 씬 종료·늦은 갱신0 |
| stage03-gate-stage13-inset | 43 | 네 크기 비영점 안전 영역·버튼 경계/raycast·보드 침투0 |
| stage03-gate-stage13-regression | 2132 | 기존 행동·파워·풀·진행·중단·오디오/수명 |
| stage03-final-legacy-ui | 97 | 기존 미션/아이템/팝업·안전 영역·5회 반복 재시작 |
| stage03-final-generators | 18 | 세 연결/생성 경로·추가 등록/정책·Next/GUID·원본 보존 |

도구/선행 단계 11회는 213 PASS, 게임 묶음 9회는 2,453 PASS, 마지막 UI/생성 2회는 115 PASS다. 별도 실제 컴파일 실패 검사 `stage03-template-compilation-failure`는 4 PASS·exit0이며 의도한 owned #error/Failed/프리팹 미생성/기존 파일 불변을 확인했다. 이 4개는 최종 22회 합계에 포함하지 않는다. 마지막 수정은 이름 preflight/상태 표시이며 기존 컴파일 실패 처리 경로는 보존했다.

## 최종 리뷰와 재현 → 수정

독립 리뷰는 한 번만 수행했다. `final-review.md`는 수정 전 판정이며 `review-resolution.md`가 최종 처리 기록이다. 재리뷰 없이 직접 수정하고 같은 실패 검사와 전체 관련 회귀로 확인했다.

| 항목 | 실제 RED | 최소 수정 | GREEN |
| --- | --- | --- | --- |
| I1 추가 등록 삭제 | stage03-review-catalog-red에서 owned.extra 프리팹/정책 보존 실패 | 세 내장 ID만 병합 갱신, 다른 등록 보존, 병합 후 검사/저장 | catalog-green 18 PASS 및 final-generators 18 |
| I2 미준비 Binding 복원 | stage03-review-binding-red에서 미구성 Binding이 보관을 소비 | 활성 현재 세션/입력·Host/service/context 소유권 검사 후 연결. 세 뷰에 동일 준비 검사 | binding-green 22 PASS 및 gate-game-restore 22 |
| I3 예약 팝업 이름 | stage03-review-name-red에서 CON preflight 허용 | Name에도 대소문자 무관 Windows 장치 이름 거부 | name-green 33 PASS 및 task1-tools 33 |
| M1 진단 덮임 | stage03-review-diagnostic-red에서 이전 성공 뒤 잘못된 요청 진단이 실제 폴링 후 사라짐 | 실제 요청 Details 변경 때만 상태 갱신 | diagnostic-green 15 PASS 및 task1-window 15 |

M1은 이전 성공 표시가 새 거부의 원인 진단을 막으므로 Important로 재판정했다. 네 RED 모두 컴파일/설정 실패가 아니라 명시한 행동 assertion에서 실패했다. 검토자가 판단에서 제외한 21개 행동도 원장의 Ruling으로 각각 판단과 비용을 기록했다. 미해결 Critical/Important 및 보류 Minor는 없다.

원장: `.superpowers/sdd/popup-framework-stage-03-plan/progress.md`. 판정 전체 사본: `Logs/PopupFramework/Stage03/rulings-exhaustive.txt`. 커밋 금지에 따라 원장/증거를 삭제하지 않는다.

## 완료 조건 12개별 증거

| 조건 | 확인 결과와 직접 증거 |
| --- | --- |
| 1 선행/원본 | 2단계 최종 기록 및 현재 1~2단계 8회/157 PASS. baseline HEAD/status/1,825개 해시·최종 허용 변경 감사 |
| 2 메뉴/검사 | tools33와 window15에서 세 메뉴, 중복/빈 ID·프리팹/Rect/직렬화 CanvasGroup 오류 진단. 예약 Name 8종 추가 거부 |
| 3 관리/Undo/조회 | window15의 실제 UI ID 수정 Undo/Redo/명시 저장·Play 저장 거부·선택 Host만 시험·pending 표시, tools33의 immutable 복사 조회 |
| 4 템플릿 | template8 실제 컴파일/도메인 재로딩/프리팹/등록/열기/Copy/닫기/충돌. 별도 컴파일 실패4, 이름/경로 거부33·지속 진단15 |
| 5 게임 정책 | game-input23/result를 포함한 restore22: 세 단일/Restorable ID, pause만 정지·pause/description Cancel 허용·result 거부, Refresh 인스턴스/순서 불변 |
| 6 중첩/최상위 | game-input23 실제 raycast/가상 포인터·키와 하위 Submit0, 보드 교환/Shuffle/HUD0, Escape 닫기1·중간 제거 유지 |
| 7 요청 소유권 | game-input23 외부 정지/입력 유지·TimeScale 불변, restart13 외부 해제 가능, regression2132 배경/오디오/중단/수명·결과음 중복0 |
| 8 기존 Retry/Next | restart13 실패/취소는 기존 state/art/key, 성공만 새 문맥. scene140/asset38/boundaries31 실제 버튼·중복/늦은 요청·Asset 안내, legacy-ui97 |
| 9 선택적 복원 | restore22 실제 A 언로드→B→새 A, 이전Retry0·현재 실제 반복 클릭1, 복원 명령/결과음0, 누락/미구성/비활성 연결 후보0·보관 유지. 선행 data59/scene26의 capture/Prepare/Copy/종료 실패 |
| 10 디자인/GUID/생성 | design11 기준 설정·원본 쓰기0, generators18 세 경로 두 번 재실행 추가 등록/정책·Next/Host/Binding/GUID 보존, 최종 원본 메타 해시 불변 |
| 11 마지막 검증/화면 | 위 22/22·2781 PASS·FAIL0·exit0, 수정 소스37 SHA256 불변. 네 크기·inset43·실제 캡처 확인. Android 물리 입력 미검증 구분 |
| 12 리뷰/문서/감사 | 독립 리뷰1회·네 RED→GREEN·전체 GREEN, 현재 사용 안내/계획/목표/명령/색인·조건표, 최종 HEAD/diff/GUID/원본/소유자원 감사 |

## 화면과 보존 한계

`Logs/Stage13/`의 victory/loading/error/inset-victory를 1280×720·450×800·450×975·600×800에서 확인했다. panel/text/Retry/Next가 영역 안에 있고 로딩은 잠금, 오류는 복귀 표시다. 결과/비영점 안전 영역의 경계·최상위 raycast·배경 침투0은 실제 검사다. 마지막 캡처 16개 중 15개는 직접 확인한 이미지와 SHA256이 같고 변경된 error-1280x720은 다시 열어 확인했다. 세부 기록은 `final-capture-audit.txt`다.

초기 game-gate의 기존 회귀 실패는 inactive 명령/새 Binding 소유자/승인된 취소 보존 계약을 반영한 기존 verifier 수정으로 처리했다. assertion을 제거하거나 런타임 규칙/오디오/풀을 바꿔 통과시키지 않았다. 현재 같은 game-gate 전체 9회가 통과한다. 마지막 UI 검사 실행기의 결과 경로 오기는 검사 자체 exit0 뒤 발생한 harness 오류이며 올바른 `Logs/PuzzleUIVerification/interaction-results.txt`로 재실행해 97 PASS를 확인했다.

물리 Android 뒤로가기/터치·기기 오디오·기기 성능 및 player/content build는 미검증이다. Editor 결과를 기기/배포 보장으로 확대하지 않는다. 메모리 보관만 구현했으며 게임 진행 영속화·새 씬 왕복 기능·디스크 저장·구매/보상·새 패키지/asmdef/DI/풀링·디자인 변경은 추가하지 않았다.
