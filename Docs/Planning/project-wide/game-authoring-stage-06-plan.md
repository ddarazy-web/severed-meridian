# 게임·제작 도구 분리 6단계 — 원본 이관·팩·제품 구성

> 2026-10-09 승인 범위의 구현·검증 완료. 실제 Player/번들 빌드·실기 검증은 별도 게이트로 남는다. 진행 기록은 Docs/Verification/project-wide/game-authoring-stage-06-progress.md를 따른다.
> 실행 담당: `superpowers:executing-plans`. 독립 조사·검토에 필요한 경우 하위 에이전트를 사용할 수 있다.

**Goal:** 검증한 JSON을 기본 제작 원본으로 채택하고 같은 스냅샷에서 게임용 팩을 생성하며 네 제품의 구성 경계를 확인한다.
**Architecture:** 문서 저장·공통 실행 어댑터를 재사용한다. 제작 JSON과 배포 MemoryPack을 구분하고, Unity 변환 어댑터만 Addressables 등록을 담당한다. SO와 JSON의 자동 양방향 동기화는 만들지 않는다.
**Tech Stack:** 현재 프로젝트 Unity, Newtonsoft.Json, MemoryPack, Addressables, Build Profiles. 착수 시 실제 버전과 프로필을 다시 확인한다.
**Spec:** [통합 계획](2026-10-08-game-and-authoring-products-plan.md), [JSON 계약](2026-10-08-json-authoring-transition-plan.md).
**완료 조건:** [목표](../../Goals/project-wide/game-authoring-stage-06-goal.md) · [실행문](../../Commands/project-wide/game-authoring-stage-06-command.md).

## 공통 제약

- 커밋·푸시·Player/Addressables 빌드·HTML 매뉴얼 수정 금지. 데이터 직렬화 검사는 제품/번들 빌드와 구분한다.
- 질문은 충분히 설명하는 제한시간 없는 일반 문답으로 한다.
- 기존 work 작업 환경과 미커밋 변경을 보존한다. 원본 전환을 기존 자료 삭제로 구현하지 않는다.
- 새 블록·장애물 데이터는 기획 출처를 확인한다. 테스트 편의를 위한 출시 콘텐츠 추가 금지.
- 레벨은 번호 구간별 50개 단위 팩, 기존 주소/로더 호환을 유지한다. 게임은 JSON 직접 로딩으로 바꾸지 않는다.
- 실제 배포 앱·실기 검증은 별도 승인 후 수행할 후속 게이트다. 빌드 금지 상태에서 출시 준비 완료로 보고하지 않는다.

## A. 전체 원본의 검증 가능한 JSON 이관

대상: `Assets/Scripts/Features/LevelAuthoring/Editor/Import/LegacyContentExporter.cs`, `LevelAuthoring/Storage/ContentSnapshotStore.cs`, 기존 JSON 문서·참조 검사기, 외부 `Tests/Editor/Features/LevelAuthoring`.

- [x] 5단계 완료 기록과 기본 메뉴/구형 가져오기 경로를 확인한다. 미완료는 먼저 해결한다.
- [x] 전체 SO 목록·파일 해시·GUID↔문서 ID·참조 관계·기획 출처를 기록한다. 엔진 설정용 SO와 제작 원본을 구분한다.
- [x] 현재 시험 폴더 제한과 원본 전환 경계를 명시적으로 분리한다. 별도 후보 폴더로만 변환하고 기존 원본을 덮어쓰지 않는다.
- [x] 누락/중복 ID, 미지원 버전, 공유 튜토리얼/완료 ID, 리소스 참조, 재이관 ID 안정성 검사를 먼저 추가하고 실패를 확인한다.
- [x] 수량·필드·참조·대표 실행 결과가 일치하는 스냅샷만 채택하도록 구현한다. 채택 실패 시 기존 기본 원본 설정을 유지한다.
- [x] 원본 파일 변경·중단·재실행·폴더 이동·외부 JSON 변경을 검증한다. SO 보관 위치와 복구 절차를 기록한다.

산출물: 전체 이관 보고서, 검증된 후보 JSON, 기본 원본 지정 및 되돌리기 경로. 런타임 임시 SO는 삭제 대상이 아니다.

## B. 한 스냅샷에서 레벨·요소 팩 생성

대상: `Levels/Editor/Persistence/LevelPackBuild.cs`, `LevelPackAddressablesBuilder.cs`, `Elements/Editor/ElementContentPackBuild.cs`, `LevelAuthoring/Runtime/AuthoringObjectGraph.cs`. 코드 경로는 `Assets/Scripts/Features/` 기준이다.

- [x] 기존 `CreatePackBytes`/`CreateBytes`를 재사용할 입력 경계를 확보한다. JSON 원본 모드에서는 SO 검색 경로로 자동 대체하지 않는다.
- [x] 50/51·100/101, 결번·중복 번호·정의 충돌·공유 튜토리얼 해석·오래된 팩 판별 검사를 먼저 만든다.
- [x] 고정한 JSON 스냅샷을 검증하고 임시 그래프로 변환하여 레벨/요소 팩을 함께 생성한다. 모든 바이트를 검증한 뒤에만 산출물을 교체한다.
- [x] 변환 도중 원본 변경과 교체 중 실패를 주입한다. 이전 정상 팩·주소·메타를 보존하고 혼합 세대 산출물을 거절한다.
- [x] Addressables에는 생성된 팩과 필요한 표현만 등록한다. 번들 빌드는 실행하지 않는다.
- [x] 도구의 현재 초안 시험과 배포 팩 시험을 명확히 구분한다. 같은 입력·시드·행동의 결과를 기존 로더와 대조한다.

산출물: JSON→검증→튜토리얼 해석→MemoryPack→등록 경로, 콘텐츠 세대/해시 증거와 실패 복구 검사.

## C. 네 제품의 구성 경계와 최종 채택

대상: 기존 `Assets/Settings/BuildProfiles`, 제품 설정/검사 코드, 씬·프리팹·Addressables 설정. 새 플랫폼 SDK는 추가하지 않는다.

- [x] Android/iOS/Steam/Windows 레벨툴의 기존 프로필·심볼을 읽고 플랫폼·시작 씬·출력 위치·설정 대응표를 작성한다. 기존 심볼을 임의로 중복 생성하지 않는다.
- [x] 상호 배타적인 제품 심볼과 플랫폼 조합을 검사한다. 설치되지 않은 플랫폼 모듈이나 SDK는 사용 가능하다고 가정하지 않는다.
- [x] 씬 직접 참조·Resources·StreamingAssets·사전 로드 객체·Addressables·네이티브 플러그인 의존성을 검사한다. 게임에 제작 원본/도구 화면/테스트가 유입되는 경로를 제거한다.
- [x] 검증된 JSON을 기본 제작·변환 입력으로 채택한다. 구형 SO 생성/저장 진입은 일반 제작 경로에서 제외하고 명시적 가져오기만 남긴다.
- [x] 깨끗한 후보 출력 위치에서 재생성, 에디터 재시작, 도구 시험과 정식 저장 격리를 확인한다. 기존 자료 및 관련 없는 설정의 변경 여부를 대조한다.
- [x] 실제 실행한 검사와 배포 빌드/실기 미검증 항목을 구분해 보고한다. 최종 검토 후 복사용 커밋 메시지만 제공한다.

## 검증 실행 원칙과 검토 초점

기존 `Tools/Testing/ProjectTests.ps1 -Action Run -Method <작성한 검증 메서드>`와 `JsonAuthoringChecks.ps1 -Action Portable`를 재사용한다. 새 메서드는 해당 묶음의 실패 검사를 작성한 뒤 정확한 이름과 결과를 기록한다. Unity 검사는 순차 실행하고 종료 후 테스트 연결을 해제한다.

| 위험 | 책임 묶음과 증거 |
|---|---|
| 원본 채택 중 기존 SO/JSON 손실 | A: 후보 폴더·해시·실패 시 기본 원본 불변 |
| ID 변경으로 학습 기록/공유 의미 변경 | A: 전체 매핑과 반복 이관·동일 행동 비교 |
| 레벨과 요소 팩이 다른 세대 | B: 한 스냅샷·전체 선검증·교체 실패 복구 |
| JSON 모드에서 오래된 SO/팩을 몰래 사용 | B: 잘못된 대체 경로 거절·입력 표시 |
| 프로필 존재만으로 제품 격리 완료 주장 | C: 참조/심볼 검사와 미실행 빌드의 명확한 구분 |


