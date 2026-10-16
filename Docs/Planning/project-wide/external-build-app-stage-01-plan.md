# Windows 외부 빌드 앱 1단계 — 공통 연결 계약 계획

> 구현자는 `superpowers:executing-plans`를 적용해 작업 묶음별로 검증한다. 이 문서 작성은 구현 착수나 커밋 승인이 아니다.

2026-10-16 · 구현·검증 완료. 상세 근거는 1단계 진행 기록 참조.

**Goal:** 기존 UI/BAT와 같은 설정을 조회·저장하고 로그 정리 결과를 전달하는 외부 연결부를 준비한다.

**Architecture:** Unity 설정의 해석·검증·저장 책임을 기존 Editor 영역에 유지한다. PowerShell은 프로젝트 잠금·실행·JSON 전달을 담당한다. WPF와 연결될 계약은 Unity 객체를 노출하지 않는 JSON으로 정의한다.

**Tech Stack:** 기존 Unity C#, Newtonsoft.Json, Windows PowerShell, 기존 검증 스크립트 구조.

**Spec:** [통합 계획](external-build-app-integration-plan.md) · [목표](../../Goals/project-wide/external-build-app-stage-01-goal.md)

## 범위와 제약

- 재진입 시 기존 1단계 완료 기록과 구현을 먼저 대조한다. 충족한 항목은 유지하고 누락·회귀가 확인된 항목만 수정한다. 계획 작성 요청만으로 검사를 재실행하거나 다음 단계 구현을 시작하지 않는다.
- WPF 화면, 실제 빌드, 실제 서명 비밀번호 입력·복호화, SDK 설치는 제외한다.
- 프로필/옵션/버전의 조회·저장과 로그 정리 기계 응답까지 구현한다. 로그 삭제 검증은 임시 fixture만 사용하고 실제 프로젝트 로그 삭제는 실행하지 않는다.
- 기존 `Build.bat`, 제품별 BAT, list/check/build v1 입력을 보존한다. 기존 옵션 처리기를 별도 엔진으로 복제하지 않는다.
- 커밋·푸시·HTML 매뉴얼 갱신 금지. 질문은 제한시간 없는 일반 채팅으로 한다.

## 파일과 책임

| 파일 | 예정 변경 |
|---|---|
| `Assets/Scripts/Features/Products/Editor/BuildTool/BuildToolExternalSettings.cs` (신규) | 현재 설정 DTO 작성, 입력 검증, 기존 버전/옵션 저장 호출 |
| `Assets/Scripts/Features/Products/Editor/BuildTool/BuildToolBatch.cs` | v1 유지, v2 settings/save-settings 라우팅·결과·오류 |
| 같은 폴더 `BuildToolOptionState.cs`, `BuildToolVersionSettings.cs` | 기존 저장 API를 재사용하기 위한 필요한 최소 변경만 |
| `Tools/Build/Invoke-BuildTool.ps1` (신규) | UI용 구조화 요청 파일 진입점, 공통 잠금·Unity 실행 호스트 |
| `Tools/Build/Build.ps1` | 기존 인자를 공통 호스트로 전달. BAT 인자/종료 코드 유지 |
| `Tools/Build/Clean-Logs.ps1` | UI가 필요한 후보 용량 등 구조화 정보 보완, 기존 안전 정책 유지 |
| `Tests/Editor/Features/Products/BuildToolExternalSettingsVerification.cs` (신규) | 복제 프로필로 조회/저장/오류/기존 설정 보존 검사 |
| `Tools/Testing/ExternalBuildContract.Tests.ps1` (신규), `LogCleanup.Tests.ps1` | 요청 호환·호스트 오류·정리 회귀 검사 |
| `Tools/Build/README.md` | JSON 계약과 개발자용 실행 예시. HTML 변경 없음 |

신규 Editor 파일에는 meta를 함께 관리한다. 현재 구현과 다른 파일 분리가 더 단순한 경우 책임은 유지하고 경로 변경을 완료 보고에 기록한다.

## 계약 초안 — 1단계에서 고정할 외부 표면

- 공통 요청: `version:2`, `requestId`, `operation`, `profileGuid`. 기존 v1의 이름 기반 입력은 어댑터로 유지한다.
- `settings`: 등록 프로필 목록, 플랫폼/출력, 버전 필드 label/type/value, 옵션 id/설명/default/saved/effective/지원 여부, 비밀이 아닌 서명 경로·alias·저장 여부, 설정 revision을 반환한다.
- `save-settings`: profileGuid, expectedRevision, appVersion(name/code), options를 받는다. 미지정 항목은 보존하며 빈 값과 미지정을 구별한다. 지원하지 않는 필드·미등록 옵션·버전·충돌을 저장 전에 거절한다. revision이 바뀌었으면 재조회 요청을 반환하고 덮어쓰지 않는다.
- 결과: `version`, `requestId`, `exitCode`, `data`, `errorCode`, `message`. 비밀번호·암호문은 설정 응답에 포함하지 않는다. 성공 시 저장한 현재 설정을 다시 반환한다.
- PowerShell 진입점: `Invoke-BuildTool.ps1 -ProjectPath <path> -RequestPath <json> [-UnityPath <exe>]`. Unity 작업은 프로젝트별 단일 실행 잠금을 사용한다. 로그 출력과 JSON 결과 파일을 분리한다.
- 정리 operation은 호스트가 처리한다: `cleanup-preview`, `cleanup`, `retentionDays`(기본7, 기존 범위1~3650). Unity를 새로 실행하지 않는다. 기존 정리기가 잠금을 획득하므로 호스트가 동일 잠금을 중첩 획득하지 않게 한다.
- 정리 결과: Status, Candidates(상대 경로·바이트), DeletedEntries/Bytes, Skipped, Errors. 후보 용량과 실제 삭제 용량을 구분한다. `Busy`를 정리 완료로 표시하지 않는다.

## 작업 1 — 조회 계약과 호환 진입점

- [x] 기존 v1 list/check 결과와 저장 위치를 기록한다. 다섯 등록 제품 및 옵션의 예상 필드를 fixture로 고정한다.
- [x] 조회 검사를 먼저 작성한다: 현재값/기본값 구분, 지원되지 않는 버전 코드 숨김, 비밀번호 미노출, 원본 파일 무변경.
- [x] `BuildToolExternalSettings.Read`와 v2 settings 경로를 구현한다. Guid 기준으로 등록 제품만 선택하고 기존 발견/옵션 해석기를 사용한다.
- [x] PowerShell 공통 호스트를 추출하여 기존 Build.ps1과 신규 JSON 진입점이 같은 잠금·Unity 실행·결과 판정을 사용하게 한다. 콘솔 문자열을 파싱해 설정을 얻지 않는다.
- [x] v1 list/check, 미지원 버전, 미등록 프로필, 공백·한글 경로, 프로젝트 사용 중, Unity 경로 오류를 검증한다. 조회가 빌드를 호출하지 않았음을 확인한다.

## 작업 2 — 명시적 저장과 설정 일치

- [x] 복제 프로필에서 잘못된 코드·옵션·revision 불일치가 저장 전 거절되는 검사를 작성한다.
- [x] 버전과 옵션을 전부 검증한 뒤 기존 저장 함수를 호출한다. 실패 시 이전 값 보존을 확인하고 복구 실패를 별도 오류로 반환한다. 파일 IO 실패를 성공 처리하지 않는다.
- [x] 기존 UI와 외부 조회의 effective 값이 같고, 저장 후 재조회 및 프로필 재로드에도 유지되는지 확인한다.
- [x] 원본 프로필/옵션/활성 프로필을 건드리지 않는 fixture 정리를 보장한다. v1 빌드 요청의 옵션 결정 순서와 자동 저장 여부를 바꾸지 않는다.

## 작업 3 — 로그 정리 연결과 회귀

- [x] 기존 `LogCleanup.Tests.ps1`을 먼저 실행하여 기준 결과를 기록한다.
- [x] 정리 스크립트의 반환 객체를 JSON 결과로 감싸고 후보별 용량을 제공한다. 프로젝트 경로 확장은 같은 스크립트를 임의 프로젝트에서 실행할 필요가 있을 때만 추가하며 경로 검증을 유지한다.
- [x] 임시 fixture로 오래된 로그 삭제, 최근 파일 보호, 미완료 요청, 사용 중 파일, 링크 경로, 잠금 Busy, 미리보기 후 상태 변경을 검사한다. 실제 프로젝트 로그를 지우지 않는다.
- [x] Unity 버전 설정 검사와 외부 계약·로그 정리 회귀를 실행한다. Player/Addressables는 빌드하지 않는다.
- [x] 진행 기록을 `Docs/Verification/project-wide/external-build-app-stage-01-progress.md`에 작성하고 완료 조건을 증거에 따라 갱신한다. 2단계 계획·목표·복사용 실행문을 작성하고 구현은 멈춘다.

## 우선 검토할 실패 조건

| 조건 | 예상 처리 / 담당 작업 |
|---|---|
| UI가 오래된 설정으로 저장 | revision 충돌, 재조회 안내 / 작업2 |
| 앱·BAT·Unity가 같은 프로젝트에 접근 | 기존 잠금에 따라 Busy, 중복 실행 없음 / 작업1·3 |
| 버전 저장 후 옵션 저장 실패 | 복원 또는 복구 필요, 성공 응답 금지 / 작업2 |
| Unity 결과 파일 없이 종료 | 실패, 기존 산출물 존재로 성공 판단 금지 / 작업1 |
| 정리 미리보기 후 새 빌드 시작 | 실행 시 다시 잠금 검사, 정리 생략 / 작업3 |
