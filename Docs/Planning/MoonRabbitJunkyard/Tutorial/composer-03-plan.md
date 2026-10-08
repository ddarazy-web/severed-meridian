# 조합형 튜토리얼 3단계 — 공유 흐름과 제작 도구 완성

> 실행: `superpowers:executing-plans`로 같은 세션에서 작업 묶음별로 진행한다. 하위 에이전트·빌드·커밋·푸시는 실행하지 않는다.

**목표:** 공통 흐름과 레벨별 연결, 사용자 샘플, 튜토리얼 ID 완료 기록을 제공하고 기존 콘텐츠를 검증 가능한 사본으로 전환해 구형 진행 경로를 정리한다.

**구조:** 현재 TutorialProgress/조건 등록/대상 조회/보드 연결을 재사용한다. 공유 흐름과 레벨별 값은 실행 전에 하나의 독립 LevelTutorialDefinition으로 해석한다. 런타임이 SO나 에디터 자산을 조회하지 않도록 팩에는 해석한 값만 넣는다.

**기술:** 현재 C#/Unity, ScriptableObject 제작, MemoryPack/Addressables 실행, UI Toolkit 레벨 에디터, 외부 Tests/Editor 하네스. 신규 패키지·DI·asmdef 없음.

**기획:** [조합형 튜토리얼](../../../Contents/MoonRabbitJunkyard/15_조합형튜토리얼.md) · [2단계 결과](../../../Verification/MoonRabbitJunkyard/Tutorial/composer-02-progress.md)

상태: 계획만 작성. 이 문서 작성으로 3단계 구현이나 데이터 전환을 실행하지 않는다.

## 공통 제약

- 실제 레벨의 새 튜토리얼을 대신 만들지 않는다. 기존 레벨1~4는 읽기와 전환 사본 검증 자료로만 사용한다.
- WIP·GUID·출시 팩·플레이어 완료 기록을 보존한다. 기존 에셋 자동 덮어쓰기나 PlayerPrefs 삭제 없음.
- 샘플은 적용 시 복사, 템플릿은 공유 참조다. 사용자 샘플 수정이 이미 적용한 단계로 전파되면 안 된다.
- 튜토리얼 ID는 전체 완료 기록 단위다. 템플릿 ID, 단계 식별자, 생성 개체 연결 이름과 분리한다.
- 공유 단계 순서가 다르면 흐름을 복제한다. 복제만으로 완료 ID를 변경하지 않는다.
- 조건 숫자/체크리스트는 에디터에만 표시한다. 게임은 강조·손가락·문구를 사용한다.
- 테스트는 Tests/Editor에 유지한다. 50레벨 묶음과 Addressables에는 팩만 포함하는 정책을 유지한다.
- 기존 팩3/4/5 읽기를 유지한다. 호환 읽기 변환과 구형 진행 엔진을 병행하는 것은 구분한다.
- 상세 매뉴얼 개편은 이번 단계에 자동 포함하지 않는다. 기능 인계와 사용 절차만 기록한다.

## A. 연결 계약과 완료 기록 설계 확정

파일: `Tutorial/Data/LevelTutorialDefinition.cs`, `Tutorial/Runtime/TutorialExecutionContext.cs`, `Levels/Data/LevelPackCodec.*.cs`, `Docs/Decisions/MoonRabbitJunkyard/2026-10-08-tutorial-composer-storage.md`.

- [ ] 현행 계약·실제1~4레벨·팩fixture·완료 키를 읽고 변경 전 기준을 확보한다. 플레이어 기록의 값은 바꾸지 않는다.
- [ ] 공유 흐름에서 바꿀 수치/대상/조작 영역을 명시적인 이름과 타입으로 선언하는 방식을 구체화한다. 배열 인덱스만으로 연결하지 않고, 기존 firstBinding/secondBinding은 생성 개체 추적 전용으로 유지한다.
- [ ] 필요한 미확정 항목만 하나씩 논의한다: 연결 필드 UX, 완료 ID의 기존 레벨별 명시 매핑, 기존 이동0 무료 체험 의미를 새 실행 계약에 보존하는 방식. 답변을 기다리고 임의 확정하지 않는다.
- [ ] SO 참조 없이 팩으로 해석하는 계약과 새 버전 정책을 ADR에 기록한다. 현재 팩5를 조용히 변경하지 않는다.

완료 증거: 작성/공유/해석/실행/기록의 데이터 흐름과 이름·타입·호환 규칙이 확정되어 B~D에서 같은 계약을 사용한다. 미확정 연결 계약을 먼저 구현하지 않는다.

## B. 공유 흐름과 레벨별 연결

신규 역할: `Tutorial/Data/TutorialFlowDefinition.cs`(SO), `Tutorial/Data/TutorialFlowBinding.cs`(레벨 값), `Tutorial/Validation/TutorialFlowResolver.cs`(해석/진단). 수정: `LevelTutorialDefinition.cs`, `LevelTutorialValidator.cs`, `LevelPackCodec`의 새 확장 부분.

예정 공개 경계: `TutorialFlowResolver.Resolve(LevelDefinition level)`은 원본을 수정하지 않는 실행용 `LevelTutorialDefinition`을 반환한다. 잘못된 연결은 레벨/단계/필드 경로를 포함해 거절한다. 세부 필드 계약은 A의 확정본을 따른다.

- [ ] `Tests/Editor/Features/Tutorial/TutorialSharedFlowVerification.cs`에 공유 흐름1개를 서로 다른 두 레벨에 연결하는 실패 검사를 먼저 만든다. 한 레벨 값 수정이 다른 레벨/흐름 원본을 바꾸면 실패한다.
- [ ] 동일 흐름의 단계 순서·동작·조건과 각 레벨의 대상·수치·보드/공급을 결합한다. 기존 초기 배치/공급 소유권을 템플릿으로 옮기지 않는다.
- [ ] 누락/중복 이름, 타입 불일치, 삭제된 단계/대상, 생성 연결 소실, 공유 흐름 순환 참조를 거절한다. 중첩 템플릿은 도입하지 않는다.
- [ ] 해석된 SO 입력과 팩 입력을 같은 조건 엔진으로 실행하고 결과·시드·공급 소비·생성 개체 연결이 같은지 검증한다.

검사 명령(구현 시 진입점): `& Tools/Testing/ProjectTests.ps1 -Action Run -Method Tutorial.Editor.TutorialSharedFlowVerification.Run`.

## C. 기존 에디터 안의 공유 편집과 내 샘플

신규 역할: `Tutorial/Editor/TutorialFlowUsageQuery.cs`(참조 레벨 찾기), `Tutorial/Editor/TutorialUserSampleStore.cs`(독립 사본 저장/읽기). 데이터: `Tutorial/Data/TutorialUserSampleDefinition.cs`. 수정: `LevelTutorialEditorPanel`의 해당 partial 파일과 기존 샘플 선택 UI.

- [ ] 공유 흐름 선택·레벨별 연결 필드·원본 편집·독립 복제를 구분한다. 별도의 다른 보드 에디터를 만들지 않는다.
- [ ] 흐름 편집 전에 사용하는 레벨 목록을 표시하고, 저장 후 영향 레벨을 사본으로 일괄 검증한다. 오류에서 해당 레벨/단계/필드/셀로 이동한다. 사용 목록 조회로 레벨을 수정하지 않는다.
- [ ] 현재 단계/선택 흐름을 내 샘플 SO로 저장하고 미리보기 후 복사 적용한다. 저장 위치는 기존 데이터 폴더 아래 Tutorial/Samples로 분리한다. 취소·Undo/Redo·중복 이름을 검사한다.
- [ ] 공유 수정과 복제, 샘플 재적용을 실제 UI에서 실행해 저장·재로드 후에도 소유권이 맞는지 검증한다.

예정 검사: `TutorialSharedFlowEditorVerification.Run`. 자동 UI와 실제 가로/세로 게임 캡처를 구분해 보고한다.

## D. 튜토리얼 ID 기록과 기존 데이터 전환

수정: `TutorialExecutionContext.cs`, 에디터 실행 기록 연결부, 팩 직렬화 경계. 신규 역할: `Tutorial/Editor/TutorialLegacyConversion.cs`(사본 변환/차이 보고), 필요한 버전 읽기 변환기.

- [ ] 튜토리얼 ID로 Automatic/Always/Never를 판정한다. 같은 ID의 설명/수치 변경은 기록을 유지하고, 새 ID만 새 학습으로 처리한다. 에디터 시험 기록과 플레이어 기록을 분리한다.
- [ ] A에서 확정한 명시 매핑만 기존 레벨 키에서 읽는다. 완료 키를 삭제하지 않고 반복 실행해도 결과가 같은 전환을 검사한다. 템플릿 복제로 완료 ID를 암묵적으로 바꾸지 않는다.
- [ ] 실제1~4레벨과 팩3/4/5의 사본을 변환하여 행동·생성/제거·공급·이동·연출 대기·완료 결과를 전후 비교한다. 비교 보고 전 실제 에셋이나 배포 팩에 적용하지 않는다.
- [ ] 변환 검사가 통과한 뒤 `results` 전용 판정과 중복 진행 코드를 제거한다. 이전 팩 읽기는 단일 새 실행 계약으로 변환한다. 구형 형식 읽기까지 삭제하지 않는다.
- [ ] 실제 에셋 반영을 요청받지 않았다면 전환 사본과 보고서만 제공한다. 기존 에셋을 읽는 경우에도 호환 해석으로 실행할 수 있어야 한다.

예정 검사: `TutorialCompletionIdentityVerification.Run`, `TutorialLegacyConversionVerification.Run`. 지연 사건·재시작·팝업·이동0 경계와 일반 플레이 회귀 포함.

## E. 최종 통합과 인계

- [ ] 공유 흐름 편집→영향 레벨 검사→팩 변환→게임 실행과 사용자 샘플 저장→복사 적용을 실제로 확인한다.
- [ ] 기존 조합형/세션 검사를 다시 실행한다. 새 스크립트 이름과 실제 명령을 보고서에 남긴다. 테스트 분리 후 에디터 컴파일도 확인한다.
- [ ] 테스트가 수정한 임시 자료만 정리하고 사용자 데이터/GUID/팩/완료 기록 보존을 확인한다.
- [ ] 2단계에서 측정한 넓은 영역 후보 검색 지연을 재확인한다. 개선 시 실제 결과 예측을 생략하거나 후보 의미를 좁히지 않는다. 모바일 성능은 직접 측정 전 보장하지 않는다.
- [ ] 결과·확인하지 못한 실제 기기 범위·남은 선택사항을 보고하고, 다음 작업이 있다면 해당 계획/목표/실행문을 작성한다. 전체 매뉴얼 정리는 별도 요청 범위로 둔다.

## 검토 중점

1. 공유 원본 수정이 레벨 고유 값이나 독립 샘플을 덮어쓰지 않는가(B/C).
2. 단계 순서 변경 시 이름 연결이 다른 조건을 조용히 가리키지 않는가(B/C).
3. 같은 템플릿과 같은 완료 ID를 혼동하지 않는가(A/D).
4. 기존 팩 읽기·이동0 무료 체험·지연 사건 의미를 전환 중 바꾸지 않는가(D).
5. 오류/취소된 저장·팩 생성·레벨 준비가 기존 작업과 플레이어 기록을 보존하는가(C~E).

각 묶음은 해당 검사가 통과한 후 다음으로 진행한다. 계획 변경이 필요하면 근거와 영향 범위를 기록하고, 전체 목표를 편한 구현으로 축소하지 않는다.
