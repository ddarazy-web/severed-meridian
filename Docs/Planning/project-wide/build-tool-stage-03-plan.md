# 제품 빌드 도구 3단계 — 옵션 등록·적용·복원 계획

> 2026-10-16 · 공통 구조 구현·모의/재로드/조회 회귀 검사 완료. 실제 SDK/제품 빌드 미수행.
> 구현 담당: `superpowers:executing-plans`로 아래 세 묶음을 순차 진행한다. 이 문서 작성은 실행 승인이 아니다.

**Goal:** 제품·플랫폼별 옵션을 등록하고 ON/OFF 값에 따라 컴파일 준비·빌드 전후처리·정리를 수행한다.

**Architecture:** 기존 공통 실행기에 옵션 등록표와 단계 실행을 연결한다. 플랫폼 처리기는 플랫폼 차이를, 옵션 처리기는 해당 기능의 설정 소유권과 적용·복원을 담당한다. 실제 SDK마다 전용 어댑터를 연결하며 범용 플러그인 제거기는 만들지 않는다.

**Tech Stack:** 기존 Editor UI Toolkit·BuildToolRunner·로컬 요청 저장·외부 Tests. 새 SDK/패키지/DI/asmdef는 추가하지 않는다.

**Spec:** [통합 계획](2026-10-15-build-tool-plan.md) · [2단계 검증 기록](../../Verification/project-wide/build-tool-stage-02-progress.md) · [3단계 목표](../../Goals/project-wide/build-tool-stage-03-goal.md) · [실행문](../../Commands/project-wide/build-tool-stage-03-command.md)

## 전역 제약

- 메뉴는 `Match → Build → 빌드 도구`. 기존 제품 정의와 고정 출력 계약을 재사용한다.
- Player/APK/AAB/iOS·Addressables 실제 빌드는 별도 승인 전까지 금지한다. 모의 백엔드와 검사 전용 옵션으로 검증한다.
- 커밋·푸시·배포·HTML 매뉴얼 갱신·SDK 설치는 하지 않는다. 질문은 제한시간 없는 일반 채팅으로 한다.
- GPG와 특정 SDK의 실제 ON/OFF 어댑터는 대상·의존성·제외 범위가 확정되지 않았다. 이번 단계는 등록·실행 구조를 제공하고 실제 SDK 연동 완료를 주장하지 않는다.

## 파일과 계약

위치: `Assets/Scripts/Features/Products/Editor/BuildTool/`. 기존 기능은 재사용하고 필요한 책임만 추가한다.

| 파일 | 책임 |
|---|---|
| `BuildToolOptions.cs` 신규 | 옵션 정의·좁은 처리 계약·명시적 등록표·대상/의존/충돌 검증 |
| `BuildToolOptionState.cs` 신규 | 프로필 GUID별 개인 선택 저장, 확정 실행값·단계별 적용 기록 |
| `BuildToolRunner.cs`, `BuildToolRequest.cs` 수정 | 옵션 확정, 재로드 이후 단계 재개, 적용 순서·역순 정리 |
| `UnityProfileBuildBackend.cs` 최소 수정 | 컴파일 준비 및 실제 콜백에 맞는 전후처리 경계 연결 |
| `MatchBuildToolWindow` 관련 파일 수정 | 적용 가능 옵션, 설명·기본값·차단 이유·실행 요약 |
| `Tests/Editor/Features/Products/BuildToolOptionsVerification.cs` 신규 | 모의 옵션·백엔드의 조합·순서·복원 검사 |
| `Tests/Editor/Features/Products/BuildToolOptionsReloadVerification.cs` 신규 | 실제 스크립트 재로드와 미완료 단계 재구성 검사 |

정의에는 안정 ID·처리 버전·이름·툴팁·기본값·적용 제품/플랫폼·의존/충돌 ID를 둔다. 체크박스만 제공하며 요구되지 않은 숫자·열거형 옵션 편집기는 만들지 않는다.

연결 계약은 다음으로 시작한다. 기존 동일 책임 타입이 있다면 중복 생성하지 않는다.

- `BuildToolOptionDefinition`: `Id`, `Version`, `Label`, `Tooltip`, `DefaultEnabled`, `Requires`, `Conflicts` 및 `bool AppliesTo(ProductDefinition product)`.
- `BuildToolOptionStage`: `PrepareCompilation`, `BeforeBuild`, `AfterBuild`, `Cleanup`.
- 옵션 처리 계약: `Definition`, `bool Handles(BuildToolOptionStage stage)`, `void Execute(BuildToolOptionStage stage, BuildToolOptionContext context)`.
- `BuildToolOptionContext`: `Request`, `Enabled`, `Result`와 해당 옵션의 원래 값·예상값·단계 완료 기록. 처리기는 등록되지 않은 다른 옵션의 기록을 변경하지 않는다.
- `BuildToolOptions.Resolve(ProductDefinition product, IReadOnlyDictionary<string, bool> choices)`는 의존·충돌 검증 후 확정 순서의 옵션 목록을 반환한다. 지원하지 않는 선택은 무시하고 실행하지 않으며 UI가 이유를 표시한다.

하나의 처리기가 여러 단계에 참여할 수 있다. `Cleanup`은 성공적으로 적용한 부분만 되돌리며 재호출해도 외부 변경을 덮어쓰지 않는 비교 후 복원을 사용한다. 위 이름은 후속 구현 계획의 계약이며 이번 단계에 소스를 미리 추가하지 않는다.

## 검토 초점

1. 이전 ON 실행의 설정이 다음 OFF 실행에 남는가 → 작업 A/B의 ON→OFF 검사.
2. 옵션 변경과 사용자 수정을 구분하지 못해 원본 변경을 숨기는가 → 작업 B의 설정 소유권·지문 검사.
3. 컴파일 재로드 때 이미 적용한 단계를 다시 실행하는가 → 작업 B/C의 재로드 검사.
4. 후처리·로그 오류가 임시 상태 정리를 막는가 → 작업 C의 실패 주입 검사.
5. 공유 SDK 의존성·제품 필수 심볼을 일반 OFF 옵션이 제거하는가 → 작업 A의 예약 설정·대상 검증.

## 작업 A — 옵션 정의·검증·화면

- [x] 검사 전용 옵션으로 대상 제품 필터링, 기본값, 의존 누락·순환·충돌을 실패 검사로 만든다.
- [x] 등록표에서 해당 제품·플랫폼에 적용 가능한 항목만 수집한다. 중앙 플랫폼 switch나 제품 정의 복제를 추가하지 않는다.
- [x] 개인 선택은 기존 `UserSettings/MatchBuildTool.json`의 GUID별 영역에 저장한다. 미등록·지원하지 않는 기록은 적용하지 않고 이유를 표시한다.
- [x] 옵션 이름·설명·ON/OFF 효과·차단 이유를 표시한다. 등록된 실제 옵션이 없으면 빈 상태를 유지하고 검사 옵션은 운영 UI에 노출하지 않는다.
- [x] 제품 필수 심볼·시작 씬·공유 의존성의 소유권을 검증한다. 설치된 SDK를 토글한다는 이유로 패키지 제거/재설치를 수행하지 않는다.

결과: 옵션 정의를 추가하면 지원 제품에만 표시되며, 잘못된 조합은 변경 전에 거절된다.

## 작업 B — 실행값 확정·단계 적용·재로드

- [x] 실행 요청에 최종 ON/OFF 값과 처리 버전을 확정 저장한다. OFF 처리도 실행 대상에 포함한다. 실행 중 UI 선택이 이미 확정한 요청을 바꾸지 않는다.
- [x] 컴파일 준비 → 플랫폼 전환/재컴파일 대기 → 빌드 전처리 → 빌드 → 후처리 → 종료 정리로 연결한다. 실제 Unity 콜백 시점에 맞춰 중복 없는 호출 위치를 기록한다.
- [x] 단계 시작 전에 소유 설정의 원래 값·예상 적용값과 진행 기록을 저장한다. 재로드 후에는 실제 상태를 대조하며 이미 완료한 단계를 무조건 재실행하지 않는다.
- [x] 원본 프로필 지문과 자신의 옵션이 적용된 예상 지문을 구별한다. 외부 변경을 새 기준으로 받아들이기 위해 지문을 무조건 재계산하지 않는다.
- [x] 의존 순서대로 적용하고 역순으로 복원한다. 합칠 수 있는 심볼은 하나의 소유자가 적용하고 동일 설정의 상충 변경은 실행 전에 거절한다.

결과: 같은 세션의 재로드를 이어가며 같은 요청의 빌드 호출은 최대 1회다. 에디터 재시작은 자동 실행하지 않는다.

## 작업 C — 실패 정리·회귀·보고

- [x] 성공·실패·취소·부분 적용·후처리 실패·기록 실패에서도 소유 임시 상태 정리를 시도한다. 외부 변경은 덮어쓰지 않고 정리 오류를 따로 남긴다.
- [x] Player 결과와 전체 옵션 작업 결과를 구별한다. 최종 산출물의 후처리는 유지하고 원본 프로젝트 임시 변경만 복원한다. 부분 산출물을 삭제하지 않는다.
- [x] ON→OFF, 반복 ON, 제품 전환, 적용 중 재로드, 실행 중 값 변경, 처리 버전 변경, 순환·충돌, 후처리 오류, 정리 오류, 외부 수정, 재시작을 모의 검사한다.
- [x] 기존 사전 검사·창·실행·재로드 검사를 순차 실행해 회귀를 확인한다. Unity 검사는 실제 빌드 API를 호출하지 않는다.
- [x] 변경·검증·미검증·SDK별 후속 범위를 `Docs/Verification/project-wide/build-tool-stage-03-progress.md`에 기록한다. 실제로 필요한 후속 어댑터를 제안하되 자동 구현하지 않는다.

신규 검사 메서드 구현 후 실행할 명령:

```powershell
& ./Tools/Testing/ProjectTests.ps1 -Action Run -Method Products.Editor.BuildToolOptionsVerification.Run
& ./Tools/Testing/ProjectTests.ps1 -Action Run -Method Products.Editor.BuildToolOptionsReloadVerification.Run
```

이 단계의 완료는 옵션 확장 구조와 모의 검증 완료다. GPG SDK 제외·Gradle·Manifest·스토어 로그인·실제 산출물의 검증은 실제 어댑터별 후속 범위로 명시한다.

## 완료 기록

[진행·검증 기록](../../Verification/project-wide/build-tool-stage-03-progress.md)에 실제 연결 계약, 독립 검토와 수정, 최종 여섯 검사 로그, 요구사항별 감사와 미검증을 정리했다. 설정 접근자는 변경 전에 예상 지문을 계산하고 Context.Apply로 변경하며, 공유 심볼은 공통 합산·보호 목록을 사용한다. 운영 SDK 등록표는 비어 있고 검사 옵션은 외부 Tests에만 있다. 다음 실제 SDK 연동은 자동 진행하지 않았다.
