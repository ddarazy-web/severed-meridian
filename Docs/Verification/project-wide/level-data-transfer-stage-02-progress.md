# 제작 데이터 전달 2단계 진행 기록

2026-10-17 · 완료

계획: Docs/Planning/project-wide/level-data-transfer-stage-02-plan.md

- Ruling: 기존 작업 폴더에서 단계 1 변경을 이어 사용한다. 커밋·푸시·제품 빌드·HTML 갱신 금지에 따라 자동 커밋 및 작업 폴더 삭제를 하지 않는다.
- Ruling: SO 이관 채택과 외부 ZIP 채택을 분리하고 표현 리소스 검사만 공유한다. 기존 SO 대조 계약은 변경하지 않는다.
- Pre-flight: ZIP Import → 고정 스냅샷 검사 → 비교 보고서 → 해시 재확인 후 선택 설정 교체. 팩 바이트는 메모리에서 검사하고 파일은 교체하지 않는다.
- 작업 묶음 1~5 완료. 사용자 데이터는 보존하고 격리 원본 선택으로 검증.

## 구현 및 검사 이력

- `ExternalProjectImport`: 후보 Import/Inspect, 문서 ID·레벨 번호 비교, 명시적 교체 확인, 후보·기존 원본·설정 해시 재확인, 원자적 선택 설정 교체와 고유 복구 사본.
- `ContentImportWindow`: UI Toolkit 검사/비교/전체 교체 확인 화면. 팩 파일 변경 없이 갱신 필요 안내.
- `AuthoringSourceSelection.ValidateResources`만 공통 추출. SO 이관 보고서·원본 비교는 유지.
- RED `20261017-011520-723-Run.log`: 외부 ZIP 서비스 부재 확인. 새 검사 스크립트의 자동 생성 meta는 Tests 원본으로 보존 후 연결 영역 정리.
- `20261017-011809-127-Run.log`: 테스트 fixture 상대경로 오류. 절대경로로 수정.
- `20261017-011902-621-Run.log`: 최초 10개 조건 통과. 중복 레벨은 ContentSnapshot 생성 시 이미 거절되므로 손상 후보 파일로 검사를 수정. 기존 검증을 중복하는 구현 제거.
- 확장 검사 실행 중: `20261017-012213-801-Run.log`. 이후 실제 EditorWindow 패널/반영 버튼 검사를 진행한다.
- Ruling: 사용자 원본 선택은 실제로 바꾸지 않고 격리 설정 경로로 채택을 증명한다. 실제 원본 반영은 사용자가 비교 결과를 확인한 후 도구에서 수행한다.
- `20261017-012213-801-Run.log`: 21 PASS/FAIL 0, 정상 종료. 실제 제작 원본·선택·MemoryPack 파일 보존 포함.
- UI 검사 `20261017-012611-664-Run.log`: NavigationSubmitEvent가 버튼 콜백을 실행하지 못했다. 기존 LevelToolTransferRecoveryVerification과 같은 Clickable.Invoke 방식으로 연결된 콜백을 검증하도록 수정했다. 네이티브 마우스 입력 검사로 확대 해석하지 않는다.
- UI 재검사 `20261017-012741-843-Run.log` 진행 중. 검사 중 진행률·취소를 제공하도록 단계별 콜백을 연결했다.
- 독립 최종 검토 요청: transfer_stage02_review. 다른 단계의 변경은 검토 범위에서 제외.
- UI `20261017-012741-843-Run.log`: PASS, exit 0. 실제 EditorWindow 패널·체크 확인·연결된 반영 콜백·리소스 갱신 안내 통과. 네이티브 마우스 조작/픽셀 배치 검수는 별도 미검증.
- Final review Important 2건: 기존 에셋 존재만으로 게임 아틀라스 해석을 우회할 수 있음 / 정적 오류의 문서·레벨 문맥 누락. `20261017-013123-159-Run.log`에서 두 문제 모두 RED 확인. 엄격 아틀라스·Addressables 주소 검사와 공통 검증 문맥 보완 후 재검사 중.
- Final: minor (deferred): 후속 가져오기 실패·취소 시 이전 비교 행이 남는다. 반영 버튼과 보고서는 무효화되므로 잘못된 채택은 차단된다. 표시 정리는 후속 UX 보완 항목으로 남긴다.
- Final: Ruling: 팩 발행·등록·복구는 3단계, Android 실기기는 미검증 유지. UI 콜백과 JSON 시작 보드 검사는 실행 증거로 보완한다. 협조하지 않는 외부 작성자의 검사/교체 사이 경쟁은 기존 저장소의 낙관적 재확인 경계를 유지한다 — 완전한 파일시스템 트랜잭션은 보장하지 않는다.

## 최종 검사

- 검토 회귀 GREEN: `Logs/LevelDataTransferStage02/review-20261017-013306-785/result.txt`, 두 조건 PASS. `20261017-013241-960-Run.log`, exit 0.
- 전체 후보 처리 GREEN: `Logs/LevelDataTransferStage02/20261017-013407-094/result.txt`, **24 PASS / FAIL 0**. `20261017-013344-208-Run.log`, exit 0.
- 정상 15레벨·튜토리얼 논리 재생·메모리 변환, 원본/설정/팩 보존, 후보·원본·설정 변경 감지, 손상/미지원 ZIP, 취소, 중복 레벨, 누락 리소스, 다른 프로젝트·추가·변경·제외, 복구 설정, 채택한 JSON의 Unity 시작 보드 생성 포함.
- 최종 UI: `Logs/LevelDataTransferStage02/ui-20261017-013625-615/result.txt`, PASS / FAIL 0. `20261017-013612-053-Run.log`, exit 0.
- 기존 SO 이관: `20261017-013714-533-Run.log`, 138개 PASS 출력 및 최종 candidate migration PASS, exit 0. 기존 SO/메타 바이트 보존 및 이관 채택 거절·복구·재개 계약 유지.
- 누락 참조 진단을 추가한 최종 검토 회귀: `20261017-013836-596-Run.log`, 3 PASS, exit 0. 중복 번호·누락 참조의 ID/레벨/필드와 기존 비시각 에셋 거절 확인.
- 최종 테스트 연결 해제, git diff --check 통과. 제품 빌드·커밋·푸시·HTML 매뉴얼 변경 없음. ProjectSettings/Assets/Data/Addressables 변경 없음.

## 사용 및 후속 단계

Unity 메뉴 `MATCH → 제작 데이터 → 가져오기·리소스 변환`에서 ZIP 선택 → 검사 결과 및 변경 목록 확인 → 전체 교체 확인 → 정식 JSON 원본 반영을 진행한다. 이전 JSON 폴더와 선택 설정 복구 사본을 보존한다.

3단계에서 이 화면에 실제 MemoryPack 변환·Addressables 등록과 원본/생성 세대 비교를 연결한다. 지금 화면의 갱신 필요 안내는 아직 팩을 생성하지 않았다는 작업 상태이며 바이트별 최신성 판정은 3단계 범위다.

미검증: 네이티브 마우스 조작과 픽셀 배치 검수, Android 실기기, 제품 빌드. 이번 UI 증거는 실제 EditorWindow 패널과 연결된 버튼 콜백 검사다. 후속 가져오기 실패·취소 시 이전 비교 행이 남는 경미한 표시 문제는 위 검토 기록대로 보류한다.
