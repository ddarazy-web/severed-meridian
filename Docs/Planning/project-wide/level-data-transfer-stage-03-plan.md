# 제작 데이터 전달 3단계 — MemoryPack 변환·등록·게임 확인

> 구현자는 `superpowers:executing-plans`를 사용해 아래 작업 묶음별로 구현·검증한다.

2026-10-17 · 구현 및 회귀 검증 완료

**Goal:** 채택한 JSON을 50레벨 단위 MemoryPack과 요소 팩으로 변환하여 기존 게임 로딩 경로에 반영한다.

**Architecture:** JsonContentPackBuild → JsonPackPublication → JsonPackRegistration의 기존 책임을 유지한다. 가져오기 화면은 작업을 호출하고 결과·복구 상태를 보여주며 별도 변환기를 만들지 않는다.

**Tech Stack:** 기존 Unity C#·JSON·ZIP·MemoryPack·Addressables.
**Spec:** [통합 계획](level-data-transfer-integration-plan.md) · [목표](../../Goals/project-wide/level-data-transfer-stage-03-goal.md)

## 공통 제약

통합 계획의 제약을 모두 따른다. 제품 빌드·커밋·푸시·HTML 매뉴얼 갱신은 금지한다. 기존 데이터 보존 및 제한시간 없는 질문 방식을 유지한다.

## 대상 파일과 책임

2단계에서 추가한 `ExternalProjectImport`는 후보 검사·비교·JSON 선택 설정 교체까지만 담당한다. `ContentImportWindow`의 반영 완료 안내를 실제 원본/생성 세대 상태 표시와 변환 버튼으로 확장한다. 외부 표현 리소스는 `AuthoringSourceSelection.ValidateResources(snapshot, true)`의 런타임 아틀라스 주소·스프라이트 검사를 유지한다. 원본 선택의 고유 `.previous-*` 복구 파일은 삭제하지 않는다.

- 기존: Assets/Scripts/Features/LevelAuthoring/Editor/JsonContentPackBuild.cs — 필요 시 검증 보완.
- 기존: 같은 Editor/JsonPackPublication.cs, JsonPackRegistration.cs — 등록/복구 계약 재사용.
- 기존: Assets/Scripts/Features/Levels/Editor/Persistence/LevelPackBuild.cs — 빌드 사전 갱신 경로 유지.
- 2단계 ContentImportWindow.cs — 변환·상태·결과 연결.
- 검사: Tests/Editor/Features/LevelAuthoring/ImportedContentPackVerification.cs (신규), 기존 GenerationLoaderVerification 회귀 검사.

## 작업 묶음

- [x] 1. 임시 출력 대상으로 1~15레벨 및 별도 49/50/51/100/101 경계 fixture를 만든다. 예상 팩 구간·레벨 수·내용이 정확하고 생성 팩에서 레벨/튜토리얼/요소 참조를 역직렬화할 수 있어야 한다.
- [x] 2. 화면에 ‘리소스 변환’ 동작을 연결한다. 현재 선택된 원본을 고정하고 기존 생성·등록 경로를 호출한다. 변환 중 중복 실행을 막고 원본 변경을 감지한다. 프로젝트 제작 JSON과 SO 파일은 Addressables에 추가하지 않는다.
- [x] 3. 원본 해시·생성 해시·생성 시간·레벨 수·출력 경로 및 실패 원인을 표시한다. 실패 시 기존 복구 결과를 구분해서 보여주며 복구 실패를 성공 처리하지 않는다.
- [x] 4. 쓰기 및 등록 실패를 주입해 기존 팩과 등록 상태 복원을 확인한다. 레벨 구간이 줄었을 때 기존 코드의 오래된 등록 정리를 검증하고 참조 없는 이전 파일은 무단 삭제하지 않는다.
- [x] 5. Unity에서 동일한 후보의 JSON 시험과 MemoryPack 시험을 비교한다. 시작 보드·미션·튜토리얼 완료 및 일반 플레이 전환이 같아야 한다. 기존 Android/iOS/Steam 게임과 레벨툴의 빌드 전처리 분리 정책은 코드·격리 검사로 확인하고 실제 제품 빌드는 하지 않는다.

## 검사 실행과 결과

테스트 소스는 Assets 밖의 Tests에 둔다. 기존 Tools/Testing/ProjectTests.ps1의 -Action Run -Method 연결 방식을 사용한다. 추가 검사 클래스에는 배치 진입점 Run을 제공하고 실패는 비정상 종료, 성공은 PASS 건수/FAIL 0과 근거 파일을 남긴다. 실제 네임스페이스 및 실행 명령은 구현 시 확정해 완료 기록에 적는다. 앞 단계에서 통과한 ZIP/참조 검사를 재사용하며 관련 회귀만 실행한다.

## 검토 초점

통합 계획의 5개 실패 조건 중 이 단계에 해당하는 취소·오류·동시 변경·복구를 작업별 검사에 포함한다. UI 표시만 확인하고 데이터 무결성 검사를 생략하지 않는다.

## 제외 및 인계

제외: APK/AAB/Windows/iOS 제품 빌드, 원격 콘텐츠 배포, 실제 클라우드 업로드.

전달·검사·반영·플레이 전체 결과와 남은 운영 결정을 보고하고 종료한다.

## 완료 기록

[최종 검증 기록](../../Verification/project-wide/level-data-transfer-stage-03-progress.md)에서 각 작업의 근거를 확인한다. 원본 고정 변환, 실패 복구 및 메모리 조회 캐시 복원, 원본 오류와 독립적인 재복구를 구현했다. 6개 관련 검사 모두 통과했으며 실제 UI도 펼쳐 확인했다. 제품 빌드/실기기/배포 번들 검증은 제외 범위로 남긴다. 기존 팩과 Addressables 설정은 검사 후 복원했으므로 운영 원본 반영은 창의 변환 버튼으로 별도 실행한다.
