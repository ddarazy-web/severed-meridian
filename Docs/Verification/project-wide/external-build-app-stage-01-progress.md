# Windows 외부 빌드 앱 1단계 — 진행·검증 기록

2026-10-16 · 1단계 완료. WPF 앱과 실제 제품 빌드는 다음 범위.

[통합 계획](../../Planning/project-wide/external-build-app-integration-plan.md) · [1단계 계획](../../Planning/project-wide/external-build-app-stage-01-plan.md) · [완료 조건](../../Goals/project-wide/external-build-app-stage-01-goal.md) · [2단계 계획](../../Planning/project-wide/external-build-app-stage-02-plan.md)

## 구현 결과

- `BuildToolExternalSettings`: 5개 제품의 현재 버전/지원 필드, 기본·저장·적용 옵션, 서명 메타데이터, revision 조회. 버전/옵션 저장은 기존 API를 재사용한다.
- `BuildToolBatch`: v1 list/check/build 유지, v2 settings/save-settings 추가. 등록 프로필 GUID만 사용하고 미등록 필드를 거절한다.
- `BuildToolHost.ps1`: 기존 Build.ps1에서 잠금·Unity 실행·결과 판정을 추출. `Invoke-BuildTool.ps1`과 BAT가 공통 사용한다. v2 requestId와 종료 코드/결과를 검사한다.
- `Clean-Logs.ps1`: 기존 정책과 Candidates 배열을 보존하고 CandidateDetails/CandidateBytes 및 명시적 ProjectPath를 추가했다. 호스트는 cleanup-preview/cleanup을 Unity 실행 없이 처리한다.
- 비밀번호 복호화 없이 DPAPI 저장 파일 존재 여부만 조회한다. `credentialsValidated:false`를 반환한다. 일반 JSON에서 서명 비밀번호 입력을 지원하지 않는다.
- 외부 저장은 입력 전체 검증, revision 충돌 거부, 저장 후 프로필 재로드 확인, 부분 실패 시 복원을 수행한다. 프로필 파일 잠금은 변경 전에 거절한다.

## 단계별 판단 기록

- 기존 work 브랜치의 미커밋 변경을 보존하며 사용자 지정 작업 디렉터리에서 진행했다. 다른 프로젝트 Unity는 조작하지 않았다.
- 작업1→2: 조회 revision을 저장 expectedRevision과 공유한다. 프로필 전체와 개인 설정 파일 변경을 보수적으로 감지한다.
- 작업1→3: 호스트가 정리용 잠금을 중복 획득하지 않게 정리 요청을 별도 분기로 처리한다. 실행 중 정리는 Busy로 보류한다.
- 계획의 공통 호스트 책임은 `Invoke-BuildTool.ps1` 단일 파일 대신 `BuildToolHost.ps1` 함수 + 얇은 진입점으로 분리했다. BAT가 함수 반환값/종료 코드를 재사용하기 위한 변경이다.
- `Candidates` 기존 문자열 배열은 유지하고 후보별 용량은 별도 `CandidateDetails`로 제공해 이전 호출자와 호환한다.
- 검사는 먼저 실패를 확인했다: 신규 설정 클래스 없음, 신규 호스트 없음. 이후 구현했다.
- 확대 검사에서 Windows 미지원 코드 거부와 Unity SaveAssetIfDirty의 로그-only 쓰기 실패를 발견했다. 각각 지원 여부 판정, 쓰기 사전 확인/디스크 재로드 검증으로 수정 후 재검사했다.
- 기존 로그 검사 최초 실행은 샌드박스 junction 생성 제한으로 중단되었으며, 임시 fixture에 한정한 승인 실행에서 전체 통과했다.

## 검증 근거

| 검사 | 결과 / 확인 범위 |
|---|---|
| BuildToolExternalSettingsVerification.Main | 5개 복제 프로필 통과. 읽기 무변경, 메타데이터, 저장/재로드, stale revision, 잘못된 이름/코드/옵션, 생략 필드 보존, 프로필 잠금, 옵션 IO 실패 복원, 등록 카탈로그/미등록 GUID, 활성 프로필 보존 |
| BuildToolVersionVerification.Main | 기존 5개 프로필 UI 자동 저장·재생성·잘못된 입력 복원·활성 프로필 보존 통과 |
| ExternalBuildContract.Tests.ps1 | PASS 출력 27개. 정리 JSON 상관관계, 후보 용량, 미리보기 이후 잠금 재검사, fixture 삭제, v1 list/check/build 인자 유지, 모의 숨김 프로세스, 결과 없음/ID 불일치/프로세스 실패, 동시 요청/열린 프로젝트, Unity 없음/미지원 버전, 부적절한 옵션 payload의 로그 생성 전 거부 |
| LogCleanup.Tests.ps1 | 기존 16개 안전 검사 통과. 오래된 로그·스크립트 삭제, 최근/사용 중/미완료 보호, 링크 밖 파일 보호, 보관 기간, 깨진 journal 시 보류 |
| 실제 v2 settings 요청 | 외부 호스트 → Unity batchmode → 응답 성공. requestId 일치, 5개 제품 조회, 종료 코드0 |
| 실제 Build.bat -ListProfiles | 기존 5개 제품 목록 조회, 종료 코드0 |
| 실제 Build-AndroidGame.bat -Check -Format apk -DebugMode off | APK/디버그OFF 해석, 버전 조회, 종료 코드0. 빌드·서명 사용 없음 |
| 원본 보존 | 실제 조회/검사 전후 등록 프로필과 UserSettings/MatchBuildTool.json SHA256 동일. 검사용 Assets 복제 폴더 제거 확인 |
| 컴파일 / 변경 범위 | Unity C# 컴파일 후 실행 검사 통과. 관련 git diff --check 통과. 작업 시작 git 상태와 비교하여 요청 관련 코드/문서/검사만 추가 변경 |

로컬 증거: `Logs/ExternalBuildStage01/settings-verification.json`, `host-tests.txt`, `settings-host.txt`, `v1-list.txt`, `v1-check.txt`, `original-hashes.json`. 실제 요청 로그는 `Logs/ExternalBuild/<실행별 폴더>`에 있다. Logs는 자동 정리 대상이므로 장기 인수인계 기준은 재실행 가능한 검사 소스와 이 기록이다.

재실행 방법:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Testing/ExternalBuildContract.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Testing/LogCleanup.Tests.ps1
unity command run_script --file Tests/Editor/Features/Products/BuildToolExternalSettingsVerification.cs --project-path C:/Projects/Git/ServeredMeridian --caller plugin --skill unity-cli --format json
```

마지막 명령은 해당 프로젝트 Unity가 준비된 상태에서 실행한다. 관련 검증용 에디터는 작업 완료 후 정상 종료했다. 테스트는 Temp fixture와 Assets 복제 프로필만 수정하며 실제 자격 증명을 사용하지 않는다.

## 범위 밖·남은 사항

- WPF 화면, 외부 서명 입력·암호화 전달, v2 실행·취소·진행 연결은 2단계다. 현재 v2는 settings/save-settings/cleanup-preview/cleanup을 지원하며 빌드 경로는 기존 v1을 유지한다.
- 실제 Player/Addressables 빌드·배포·실기 서명 성공은 검증하지 않았다. build 호환은 모의 프로세스 경계로 검증했다.
- 부분 저장의 예외 복원은 검증했다. 전원 종료/프로세스 강제 종료 중 다중 파일 저장의 완전한 원자성까지 보장하지 않는다. 실행 복구·운영 검증은 3단계에서 다룬다.
- 코드/옵션 원본을 별도로 복제 저장하지 않으며 새 SDK·패키지를 설치하지 않았다. 실제 사용자 로그 삭제, 비밀번호 복호화, 커밋·푸시, HTML 갱신은 하지 않았다.

## 다음 단계

[2단계 계획](../../Planning/project-wide/external-build-app-stage-02-plan.md), [목표](../../Goals/project-wide/external-build-app-stage-02-goal.md), [복사용 실행문](../../Commands/project-wide/external-build-app-stage-02-command.md)을 작성했다. 2단계 구현은 시작하지 않았다.
