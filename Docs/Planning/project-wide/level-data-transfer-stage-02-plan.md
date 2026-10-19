# 제작 데이터 전달 2단계 — Unity ZIP 가져오기·정식 원본 반영

> 구현자는 `superpowers:executing-plans`를 사용해 아래 작업 묶음별로 구현·검증한다.

2026-10-17 · 완료

**Goal:** 외부 ZIP을 별도 후보로 검사하고 변경 내용을 확인한 뒤 정식 JSON 제작 원본으로 채택한다.

**Architecture:** SO 최초 이관과 외부 제작 데이터 수입의 검증 경계를 분리한다. 후보는 ContentData의 새 폴더에 두고 검증 완료 후 원본 선택 설정을 교체한다.

**Tech Stack:** 기존 Unity C#·JSON·ZIP·MemoryPack·Addressables.
**Spec:** [통합 계획](level-data-transfer-integration-plan.md) · [목표](../../Goals/project-wide/level-data-transfer-stage-02-goal.md)

## 공통 제약

착수 전 1단계 검증 기록을 확인한다. ZIP 형식은 변경되지 않았고 Export의 expectedHash는 선택 인자다. 1단계의 Windows 실제 저장·열기와 15레벨 콘텐츠 일치 확인은 완료됐다. 기존 ZIP 및 가져오기 검증을 재사용하며, 이번 단계는 Unity 후보 검사·비교·채택에 집중한다. Android 실기기 검증은 별도 미검증 항목으로 유지한다.

통합 계획의 제약을 모두 따른다. 제품 빌드·커밋·푸시·HTML 매뉴얼 갱신은 금지한다. 기존 데이터 보존 및 제한시간 없는 질문 방식을 유지한다.

## 대상 파일과 책임

- 신규 예정: Assets/Scripts/Features/LevelAuthoring/Editor/Import/ExternalProjectImport.cs — 후보 생성·검사·비교·채택.
- 신규 예정: 같은 Editor/Import/ContentImportWindow.cs — 가져오기 및 반영 UI.
- 기존: AuthoringSourceSelection.cs — 설정 읽기 계약 보존, SO 이관 검증 유지.
- 재사용: ProjectPackageStore, ContentSnapshotStore, JsonContentPackBuild, LevelTutorialReplayValidator.
- 검사: Tests/Editor/Features/LevelAuthoring/ExternalProjectImportVerification.cs (신규).

## 작업 묶음

- [x] 1. 격리 폴더의 ZIP을 ProjectPackageStore.Import로 후보 등록한다. 압축 경로·크기·구조 검사를 중복 구현하지 않는다. 취소와 오류는 현재 선택/팩에 영향을 주지 않아야 한다.
- [x] 2. 후보 스키마·문서 참조·레벨 번호 중복·요소 정의·표현 리소스 해석·레벨 규칙·튜토리얼 재생을 검사한다. JsonContentPackBuild.CreateBytes를 이용한 변환 가능성 검사는 메모리 안에서 수행하고 Assets에는 쓰지 않는다. 오류 위치와 레벨 번호를 표시한다.
- [x] 3. 문서 ID와 레벨 번호를 함께 사용하여 추가·변경·유지·제외 예정 목록을 제공한다. 다른 프로젝트 ID와 제외 예정 항목은 눈에 띄게 표시하고 전체 프로젝트 대체임을 명시한다. 세부 문서 병합은 하지 않는다.
- [x] 4. 사용자 반영 동작 시 후보와 기존 선택/원본의 해시를 재확인한다. 소스 폴더·projectId·adoptedHash의 기존 읽기 계약에 맞춰 선택 설정을 교체하고 이전 설정을 복구용으로 보존한다. 기존 SO 대조 검증을 느슨하게 만드는 방식은 사용하지 않는다.
- [x] 5. 손상/미지원 ZIP, 누락 리소스, 중복 레벨, 다른 프로젝트, 제외 항목, 검사 후 변경, 채택 실패를 검사한다. 정상 후보는 기존 Unity 편집/JSON 게임 시험에서 읽히고 팩은 ‘갱신 필요’로 남아야 한다.

## 검사 실행과 결과

테스트 소스는 Assets 밖의 Tests에 둔다. 기존 Tools/Testing/ProjectTests.ps1의 -Action Run -Method 연결 방식을 사용한다. 추가 검사 클래스에는 배치 진입점 Run을 제공하고 실패는 비정상 종료, 성공은 PASS 건수/FAIL 0과 근거 파일을 남긴다. 실제 네임스페이스 및 실행 명령은 구현 시 확정해 완료 기록에 적는다. 앞 단계에서 통과한 ZIP/참조 검사를 재사용하며 관련 회귀만 실행한다.

## 검토 초점

통합 계획의 5개 실패 조건 중 이 단계에 해당하는 취소·오류·동시 변경·복구를 작업별 검사에 포함한다. UI 표시만 확인하고 데이터 무결성 검사를 생략하지 않는다.

## 제외 및 인계

제외: MemoryPack 파일 교체, Addressables 등록 변경, 부분 레벨 병합, 제품 빌드.

3단계에서 채택한 JSON과 생성 팩의 차이를 해소한다.
