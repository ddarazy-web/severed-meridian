# 제작 데이터 전달 1단계 — 레벨툴 ZIP 백업·전달

> 구현자는 `superpowers:executing-plans`를 사용해 아래 작업 묶음별로 구현·검증한다.

2026-10-17 · 완료

**Goal:** 현재 저장본을 식별 가능한 ZIP으로 내보내고 다른 작업 폴더에서 같은 제작 데이터로 복원한다.

**Architecture:** 기존 ProjectPackageStore와 플랫폼별 ILevelToolFileTransfer를 재사용한다. ZIP 구조를 바꾸지 않고 파일명·저장 안내·왕복 검증을 보완한다.

**Tech Stack:** 기존 Unity C#·JSON·ZIP·MemoryPack·Addressables.
**Spec:** [통합 계획](level-data-transfer-integration-plan.md) · [목표](../../Goals/project-wide/level-data-transfer-stage-01-goal.md)

## 공통 제약

통합 계획의 제약을 모두 따른다. 제품 빌드·커밋·푸시·HTML 매뉴얼 갱신은 금지한다. 기존 데이터 보존 및 제한시간 없는 질문 방식을 유지한다.

## 대상 파일과 책임

- 기존: Assets/Scripts/Features/LevelTool/Runtime/LevelToolScreen.FileTransfer.cs — 저장/선택/내보내기 결과 안내.
- 기존: Assets/Scripts/Features/LevelTool/Runtime/LevelToolFileTransfer.cs 및 플랫폼 구현 — 기존 스트림 계약 유지.
- 기존: Assets/Scripts/Features/LevelAuthoring/Storage/ProjectPackageStore.cs — 필요할 때만 최소 보완.
- 검사: Tests/Editor/Features/LevelAuthoring/ProjectTransferWorkflowVerification.cs (신규, 기존 검사 패턴 사용).

## 작업 묶음

- [x] 1. 기존 ZIP 및 현재 1~15레벨의 복사본으로 왕복 검사를 먼저 확보한다. 프로젝트 ID·문서 ID·참조·정규화 콘텐츠 해시가 같아야 한다.
- [x] 2. ZIP 기본 파일명을 프로젝트명_날짜시간.zip으로 제안하고 저장 전 최신 입력 반영과 저장 성공 여부를 확인한다. 이름 충돌은 기존 파일 선택기 정책을 따르며 조용한 덮어쓰기를 추가하지 않는다.
- [x] 3. 내보내기 화면에서 프로젝트 식별 정보·레벨 수·콘텐츠 버전과 저장 결과를 안내한다. Windows는 확인 가능한 저장 위치, Android는 제공자가 반환한 표시 정보만 사용한다. 클라우드 업로드 완료를 추정하지 않는다.
- [x] 4. 저장 취소, 닫기/쓰기 실패, 한글 경로, 예전 ZIP, 다른 폴더로 복원을 검사한다. 실패 시 원본 JSON이 동일하고 불완전 파일 가능성이 표시되어야 한다.

## 검사 실행과 결과

테스트 소스는 Assets 밖의 Tests에 둔다. 기존 Tools/Testing/ProjectTests.ps1의 -Action Run -Method 연결 방식을 사용한다. 추가 검사 클래스에는 배치 진입점 Run을 제공하고 실패는 비정상 종료, 성공은 PASS 건수/FAIL 0과 근거 파일을 남긴다. 실제 네임스페이스 및 실행 명령은 구현 시 확정해 완료 기록에 적는다. 앞 단계에서 통과한 ZIP/참조 검사를 재사용하며 관련 회귀만 실행한다.

## 검토 초점

통합 계획의 5개 실패 조건 중 이 단계에 해당하는 취소·오류·동시 변경·복구를 작업별 검사에 포함한다. UI 표시만 확인하고 데이터 무결성 검사를 생략하지 않는다.

## 제외 및 인계

제외: Unity 반영 UI, 직접 클라우드 업로드, 자동 백업 스케줄, 새 ZIP 포맷.

2단계의 후보 검사·비교 화면에 전달할 ZIP을 준비한다.

