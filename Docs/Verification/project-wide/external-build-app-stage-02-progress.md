# Windows 외부 빌드 앱 2단계 — 구현·검증 기록

2026-10-16

[단계 계획](../../Planning/project-wide/external-build-app-stage-02-plan.md) · [목표](../../Goals/project-wide/external-build-app-stage-02-goal.md) · [3단계 계획](../../Planning/project-wide/external-build-app-stage-03-plan.md)

## 구현 결과

- `Tools/BuildApp`에 .NET 8 WPF 독립 앱을 추가했다. 얇은 MVVM으로 화면 상태, 호스트 전송, 작업 위치, 로그 정리를 나눈다. 게임 Assets 밖에 두며 신규 NuGet UI 패키지를 사용하지 않는다.
- 5개 제품, 제품 안의 버전 입력, 등록된 옵션과 설명, Android 서명 우선 표시, 비동기 저장·조회·검사·실행·취소를 연결했다. UI에 플랫폼 switch를 복제하지 않고 응답의 지원 필드를 사용한다.
- 미저장 입력, 설정 충돌, 긴 작업, 중복 실행, 결과 누락을 구분한다. 창 위치·크기·최대화·마지막 프로젝트/Unity/제품만 별도 사용자 설정에 저장한다.
- `BuildToolHost`/`BuildToolBatch` v2에 check/build/signing-aliases/save-signing을 추가했다. 기존 Runner, 플랫폼 처리기, 저장 옵션, v1 BAT를 재사용한다. 요청 ID에 맞는 진행·취소 파일만 처리한다.
- DPAPI 구현 원본을 WPF에 링크해 기존 형식과 저장 컨텍스트를 공유한다. 일회성 암호화 전달을 사용하고 정상·실패 시 제거한다. 실제 자격 증명은 검사하지 않았다.
- 로그는 기본 7일, 미리보기·후보/삭제 용량·개수·보호/실패·실행 확인을 제공한다. 프로젝트/기간 변경은 미리보기를 무효화하며 Busy/Partial/Error를 완료로 표시하지 않는다.
- [Markdown 사용 안내](../../../Tools/BuildApp/README.md)와 3단계 계획·목표·실행문을 작성했다. HTML은 변경하지 않았다.

## 검증 중 발견하여 수정한 사항

1. 최소 창에서 왼쪽 프로필 목록이 압축되는 현상을 실제 WPF 렌더링으로 발견했다. 왼쪽 영역을 스크롤 가능하게 변경하고 목록 높이·프로필 선택·버튼 접근 검사를 추가했다.
2. Unity 시작이 `Temp`를 정리해 실제 조회 성공 후 앱이 결과를 받지 못했다. 통신 위치를 **Library/MatchBuildApp**으로 변경했다. 실제 앱 클라이언트→PowerShell→Unity 경로로 재검증했다. 단순 모의 호스트만으로는 찾지 못한 문제였다.
3. 버전 저장이 미저장 서명 입력을 지우지 않도록 보존했다. 늦은 진행 이벤트가 완료 상태를 덮어쓰지 않도록 작업 세대와 Busy 상태를 검사한다.
4. 암호화 파일 삭제 실패가 클라이언트 실행 잠금을 영구 유지하지 않도록 finally 안에서 실행 상태를 별도로 복구한다.

## 검증 증거

| 검사 | 결과와 범위 |
|---|---|
| dotnet build BuildApp.Tests.csproj | 외부 앱·검사 실행기 컴파일, 경고0/오류0. Unity 제품 빌드 아님 |
| BuildApp.Tests | 47개 통과. 설정 기억·5개 제품·지원 필드·dirty/충돌·저장 취소·비동기 Busy·취소 전달·결과 누락·로그 상태·DPAPI·전송 성공/실패 암호화 파일 제거·프로필 선택 바인딩·키보드·저장 명령·로그 탭 |
| 실제 BuildToolClient read-only | settings로 5개 제품 조회, Windows v2 check 성공. 조회의 effective 옵션과 사전 검사의 enabled 값 일치 |
| BuildToolExternalAppVerification.Main | .NET8→Unity DPAPI 호환, 복제 프로필 서명 저장/재읽기, 일회성 파일 제거, 손상된 암호문 실패 후 제거, 범위 밖 파일 보존, 취소 ID 및 기존 Runner Canceled 도달·빌드 호출0, v2 검사/revision, 활성 프로필 보존 |
| BuildToolExternalSettingsVerification.Main | 5개 복제 프로필 조회·저장·재로드·잘못된 입력·생략 필드·충돌·IO 복원·원본 격리 회귀 통과 |
| ExternalBuildAppHost.Tests.ps1 | v2 check/build 모의 라우팅, 진행 ID, GUID 프로필 선택, 부적절한 ID 거절 통과. 실제 빌드 없음 |
| ExternalBuildContract.Tests.ps1 | 기존 v1 호환·프로젝트 Busy·동시 실행·결과 누락/불일치·프로세스 실패·경로/입력 검증 등 27개 PASS 기록 |
| LogCleanup.Tests.ps1 | 임시 프로젝트에서 기존 16개 안전 검사 통과. 실제 사용자 로그 삭제 없음 |
| 렌더링 | 1240×840, 1000×700 창의 WPF 배치와 150% 렌더링 및 로그 탭 PNG 확인. 작은 창에서도 스크롤로 접근하고 하단 실행 버튼이 유지됨 |
| 원본 보존 | 실제 조회·검사 전후 등록 프로필, UserSettings/MatchBuildTool.json, ProjectSettings.asset 해시 비교. 검사 에셋은 복제본만 사용 |

로컬 근거: `Logs/ExternalBuildStage02/tests-build.txt`, `wpf-tests.txt`, `live-readonly.txt`, `unity-verification.json`, `settings-regression.json`, `host-tests.txt`, `host-regression.txt`, `cleanup-tests.txt`, `original-preservation.txt`, `ui-1240.png`, `ui-1000.png`, `ui-logs.png`. Logs는 정리 대상이므로 재현 가능한 검사 소스와 이 문서를 장기 기록으로 사용한다.

## 재현 방법

```powershell
dotnet build Tools/Testing/BuildApp.Tests/BuildApp.Tests.csproj
& Tools/Testing/BuildApp.Tests/bin/Debug/net8.0-windows/BuildApp.Tests.exe
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Testing/ExternalBuildAppHost.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Testing/ExternalBuildContract.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Testing/LogCleanup.Tests.ps1
```

실제 읽기 전용 검사는 프로젝트 Unity를 닫은 뒤 위 실행기에 `--live-readonly`를 붙인다. 제품 빌드·서명을 호출하지 않는다.

Unity 상호운용 검사는 검사용 에디터를 먼저 연 뒤 WPF 검사를 실행해 임시 DPAPI fixture를 생성하고 다음을 호출한다. Unity 시작 시 Temp의 fixture 인덱스가 정리될 수 있어 이 순서를 지킨다.

```powershell
unity command run_script --file Tests/Editor/Features/Products/BuildToolExternalAppVerification.cs --project-path C:/Projects/Git/ServeredMeridian --caller plugin --skill unity-cli --format json
```

## 이번 단계의 한계

- 실제 Player/Addressables 빌드, 실제 Keystore 인증, 제품 설치·실행·배포는 하지 않았다. 빌드 버튼 연결은 모의 경로와 기존 Runner 취소 경계까지 검증했다.
- 강제 프로세스 종료 뒤 재접속·잔여 암호화 파일 정리, 실제 OS 배율/다중 모니터, 배포 런타임 정책·패키징은 3단계다. 확대 렌더링을 실제 모니터 배율 검증으로 대신 보고하지 않는다.
- 실행 중에는 창 닫기를 막고 취소 처리 후 종료하도록 한다. 동기 빌드 즉시 취소는 보장하지 않는다.
- 3단계는 문서만 작성하며 자동 시작하지 않는다. 커밋·푸시·HTML 갱신·실제 사용자 로그 삭제는 하지 않았다.
