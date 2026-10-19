# 레벨 제작 데이터 전달·Unity 리소스 반영 통합 계획

> 구현자는 `superpowers:executing-plans`를 적용한다. 문서 작성은 구현 착수 승인이 아니다.

2026-10-17 · 1·2·3단계 완료

**Goal:** 레벨툴에서 제작한 프로젝트를 ZIP으로 보관·전달하고, Unity에서 검증 후 정식 JSON 원본으로 채택하여 게임용 MemoryPack 리소스로 변환한다.

**Architecture:** 제작 원본은 JSON, 이동 단위는 프로젝트 전체 ZIP, 실행 리소스는 MemoryPack이다. 기존 저장·압축·검증·팩 생성·Addressables 등록을 재사용하고 Unity에 가져오기 및 반영 화면을 연결한다. SO는 기존 변환 과정의 임시 객체로만 사용하며 제작용 SO 파일을 재생성하지 않는다.

**Tech Stack:** Unity C#, Newtonsoft.Json, System.IO.Compression, 기존 MemoryPack·Addressables 파이프라인.
**Spec:** 아래 확정 범위와 단계별 목표 문서.

## 사용자 흐름

레벨툴 저장 → ZIP 내보내기 → 사용자 지정 폴더/USB/클라우드로 전달 → Unity ZIP 검사 → 변경 목록 확인 → 정식 JSON 원본 반영 → MemoryPack 생성·등록 → 게임 로더 검증.

- ZIP에는 현재 프로젝트가 참조하는 레벨·튜토리얼·블록/장애물 정의 등 공개 JSON을 함께 넣는다.
- 기존 ProjectPackageStore의 형식을 유지한다. 예전 ZIP도 계속 가져올 수 있어야 한다.
- 텍스처·프리팹·SDK·빌드 결과·비밀번호·로그·과거 스냅샷 전체를 ZIP에 넣지 않는다. 표현 리소스 참조는 Unity 프로젝트에서 해결하고 누락을 보고한다.
- 최초 전달은 수동 파일 저장/이동으로 제공한다. 클라우드 동기화 완료와 로컬 ZIP 생성 완료를 혼동하지 않는다. 서비스 직접 업로드·인증은 저장소 결정 후 별도 범위다.
- 프로젝트 전체를 후보로 가져오며 레벨별 병합은 제외한다. 정식 원본과 비교해 추가·변경·제외 예정 데이터를 표시한다. 제외 예정 항목은 사용자 확인 없이 제거하지 않는다.
- 프로젝트 ID가 다른 ZIP은 다른 프로젝트로 표시하고 명시적으로 선택해야 한다. 같은 ID라도 더 오래된 데이터일 수 있으므로 contentVersion만으로 최신성을 단정하지 않는다.
- JSON 채택 상태와 생성 팩 상태를 분리해 표시한다. 채택만 하고 변환하지 않았다면 ‘리소스 갱신 필요’다.

## 확인된 기반

| 영역 | 기존 구현 | 연결 시 주의점 |
|---|---|---|
| ZIP 전달 | ProjectPackageStore.Export/Import, LevelToolScreen.FileTransfer | 현재 참조 문서만 포함, 크기·경로 검사, 취소 및 스트림 종료 처리 재사용 |
| 원본 선택 | AuthoringSourceSelection | 기존 Adopt/ValidateCandidate는 SO 이관 원본 대조용이다. 외부 제작 ZIP에 그대로 적용하지 않는다. |
| 팩 변환 | JsonContentPackBuild.CreateBytes | 레벨·요소 팩 생성과 게임 로더 역직렬화 검사 재사용 |
| 등록·복구 | JsonPackPublication, JsonPackRegistration | 생성 파일과 Addressables 등록의 기존 복구 경계 유지 |
| 빌드 연결 | LevelPackBuild | 선택된 JSON을 사용하는 기존 갱신 경로 유지, 별도 변환 엔진 금지 |

## 3단계

| 단계 | 제공 결과 | 계획 / 목표 / 실행문 |
|---|---|---|
| 1 | 레벨툴 ZIP 백업·전달 UX 정리 및 왕복 검증 | [계획](level-data-transfer-stage-01-plan.md) · [목표](../../Goals/project-wide/level-data-transfer-stage-01-goal.md) · [실행문](../../Commands/project-wide/level-data-transfer-stage-01-command.md) |
| 2 | Unity ZIP 후보 검사·변경 비교·정식 원본 채택 | [계획](level-data-transfer-stage-02-plan.md) · [목표](../../Goals/project-wide/level-data-transfer-stage-02-goal.md) · [실행문](../../Commands/project-wide/level-data-transfer-stage-02-command.md) |
| 3 | Unity에서 MemoryPack 생성·등록·복구까지 연결 | [계획](level-data-transfer-stage-03-plan.md) · [목표](../../Goals/project-wide/level-data-transfer-stage-03-goal.md) · [실행문](../../Commands/project-wide/level-data-transfer-stage-03-command.md) |

앞 단계 완료 보고 후 다음 계획을 실제 결과에 맞게 갱신한다. 후속 단계 문서는 현재 기준 초안이며 세부 API를 미리 고정하지 않는다.

현재 세 단계 구현·검증은 완료했다. [3단계 완료 근거](../../Verification/project-wide/level-data-transfer-stage-03-progress.md)를 기준으로 개발 계획을 종료한다. 제품 빌드·실기기·원격 업로드는 수행하지 않았으며 자동으로 다음 개발 단계를 추가하지 않는다. 실제 운영 데이터 반영은 [후속 운영 실행문](../../Commands/project-wide/level-data-transfer-stage-03-command.md)으로 요청할 수 있다.

## 공통 제약

- ServeredMeridian만 대상으로 한다. 제품 빌드·커밋·푸시·HTML 매뉴얼 갱신은 하지 않는다.
- 질문은 제한시간 없는 일반 채팅으로 한다. 외부 서비스 설정/자격증명 사용은 현재 범위 밖이다.
- 기존 제작 데이터와 사용자 로그를 보존한다. 검증은 격리 후보와 임시 출력으로 먼저 수행한다.
- 원본 JSON, ZIP 후보, 변환 리소스의 역할을 나누고 작업 시작 시 고정한 해시로 중간 변경을 감지한다.
- UI 메뉴 제안: Unity `MATCH/제작 데이터/가져오기·리소스 변환`. 빌드 실행 메뉴와 혼합하지 않는다.
- 플랫폼 파일 선택은 기존 Windows/Android 구현을 재사용한다. 직접 에셋/프리팹 구조를 변경하는 작업은 포함하지 않는다.

## 주요 검토·검증 지점

1. 취소·저장 실패·쓰기 실패: 원본은 보존되고 불완전한 ZIP을 성공으로 안내하지 않음 — 1단계.
2. 손상 ZIP·경로 탈출·중복 문서·미지원 스키마: 정식 원본을 건드리기 전에 거절 — 2단계.
3. 다른 프로젝트/오래된 후보/레벨 제외: 차이를 명시하고 자동 대체하지 않음 — 2단계.
4. 검사 후 원본 또는 후보 변경: 기존 검사 결과로 반영하지 않고 재검사 요구 — 2·3단계.
5. 팩 생성·등록 중 실패 및 50레벨 경계: 이전 정상 리소스 복구 및 게임 로더 확인 — 3단계.

## 완료 보고

각 단계에서 변경 파일, 수행한 검사, 미검증 항목, 남은 작업을 보고한다. 다음 단계 계획·목표·복사용 실행문을 연결하고 실제 구현 완료 뒤에만 간략한 커밋 메시지를 제시한다. 커밋 자체는 하지 않는다.
