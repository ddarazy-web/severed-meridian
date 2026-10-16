# Windows 외부 빌드 앱 2단계 — WPF 화면과 실행 연결 계획

> 구현자는 `superpowers:executing-plans`로 작업 묶음별 검증을 수행한다. 다음 단계 실행은 별도 요청으로 시작한다.

2026-10-16 · 계획 작성, 구현 미착수

**Goal:** Unity 빌드 도구의 프로필·설정·실행·로그 관리를 독립 Windows UI에서 조작한다.

**Architecture:** 얇은 MVVM으로 화면 상태와 비동기 호스트 호출을 분리한다. Unity 설정은 1단계의 JSON 계약으로 조회·저장하며 `.asset` 직접 편집과 빌드 엔진 복제를 금지한다.

**Tech Stack:** C# WPF, 우선 설치된 .NET SDK 8.0.400의 `net8.0-windows`, 기존 PowerShell 호스트. 새 MVVM/테마 패키지 없이 기본 WPF부터 사용한다. 배포 대상 런타임과 지원 기간은 3단계 패키징 전에 재확인한다.

**Spec:** [통합 계획](external-build-app-integration-plan.md) · [1단계 기록](../../Verification/project-wide/external-build-app-stage-01-progress.md) · [목표](../../Goals/project-wide/external-build-app-stage-02-goal.md)

## 범위와 제약

- `Tools/BuildApp` 아래 독립 프로젝트로 생성한다. 게임 Assets와 빌드에 포함하지 않는다.
- 기존 Unity UI/BAT의 프로필·플랫폼·옵션 등록이 기준이다. 향후 옵션을 UI switch나 하드코딩 목록에 추가하지 않는다.
- 외부 앱 컴파일 및 모의 실행은 허용한다. 실제 게임/레벨툴 Player·Addressables 빌드, 배포, 커밋·푸시, HTML 갱신은 하지 않는다.
- 서명 구현 검사는 임시 자격 증명 fixture만 사용한다. 실제 Keystore 비밀번호 입력·사용은 사용자 명시 승인 전 실행하지 않는다.
- 로그 삭제 검사도 임시 프로젝트만 사용한다. 화면 확인을 위해 실제 프로젝트 로그를 삭제하지 않는다.

## 작업 1 — 독립 UI와 현재 설정

예정 파일: `Tools/BuildApp/BuildApp.csproj`, `App.xaml`, `MainWindow.xaml`, `BuildWorkspaceViewModel.cs`, `BuildToolClient.cs`, `BuildContracts.cs`, `WorkspacePreferences.cs` 및 해당 코드비하인드.

- [ ] 프로젝트 선택, Unity 경로 자동 탐색/변경, 프로필 목록, 조회 중/오류/마지막 조회 시각을 구현한다.
- [ ] 입력 UI는 Android 서명을 먼저 표시하고, 제품 정보에 버전을 통합한다. 옵션은 응답의 label/description/on/off로 표시한다.
- [ ] 설정 조회·저장을 비동기로 호출한다. dirty 상태, 저장 필요, RevisionConflict 시 재조회 동선을 제공하고 임의 덮어쓰기를 막는다.
- [ ] 마지막 프로젝트·프로필·창 위치/크기는 사용자 앱 설정에 저장하되 버전·옵션 원본은 복제 저장하지 않는다. 화면 밖 위치는 현재 모니터 영역으로 보정한다.
- [ ] 키보드 탭 순서, 명확한 상태 색상/문구, 툴팁, 스크롤·창 축소·고배율에서 입력 잘림을 확인한다. 원시 JSON을 일반 사용자에게 편집시키지 않는다.

검증: 모의 응답과 실제 read-only settings 응답으로 5개 프로필 전환·지원 필드 표시·지연 응답·저장 충돌을 검사한다. 외부 앱 컴파일이 Unity 제품 빌드와 다름을 보고한다.

## 작업 2 — 검사·빌드·서명·취소 연결

예정 변경: `BuildToolClient.cs`, `BuildContracts.cs`, `Tools/Build/BuildToolHost.ps1`, `BuildToolBatch.cs`, 기존 Android 서명 처리기. 필요한 보안 공통 코드는 `Tools/BuildApp`와 Unity에서 같은 원본/명세를 참조한다.

- [ ] 1단계 v2는 settings/save-settings/cleanup만 지원한다. v1 check/build 호출을 안전하게 감싸거나 v2로 확장해 기존 Runner를 그대로 사용한다. 선택값 저장과 실행값 확정을 분리한다.
- [ ] 빌드 전 제품·출력·버전·옵션을 확인한다. 저장되지 않은 변경이 있으면 저장 후 실행/저장 취소 동선을 명시한다. 취소했다고 빌드로 진행하지 않는다.
- [ ] 실행 요청 ID를 기준으로 상태·결과를 추적하고, 긴 Unity 시작/컴파일 동안 UI가 멈추지 않게 한다. 로그는 부분 읽기와 표시량 제한을 사용한다.
- [ ] 취소 요청을 파일/기존 Runner.Cancel 경계로 전달한다. 동기 Player 빌드 중 즉시 취소를 보장하지 않으며 안전한 지점까지 대기 문구를 표시한다. 앱 종료가 Unity 강제 종료로 이어지지 않게 한다.
- [ ] Keystore 경로·alias 조회/선택·비밀번호 마스킹·암호화 저장을 구현한다. 기존 DPAPI의 프로젝트/프로필/keystore/alias 컨텍스트와 저장 위치를 유지한다. 평문 JSON·명령행·로그·일반 앱 설정 저장을 금지한다.
- [ ] 암호화 입력 전달은 사용자 계정에 묶인 일회성 암호화 파일과 요청 참조를 사용한다. 실제 비밀번호 없이 fixture로 저장 호환·실패·전달 후 정리를 검증한다. 별도 저장소나 별도 플랫폼 빌드 엔진은 만들지 않는다.

검증: 모의 Unity 실행으로 성공/실패/취소/결과 없음/다른 요청 결과/프로젝트 사용 중을 확인한다. 실제 설정 조회·검사는 가능하지만 실제 빌드는 3단계 승인 후 진행한다.

## 작업 3 — 로그 관리와 사용 흐름 검증

예정 파일: `LogCleanupViewModel.cs`, 로그 관리 화면, `Tools/Testing/BuildApp.Tests` 또는 독립 테스트 실행기(기존 설치 의존성으로 가능한 최소 구성).

- [ ] 프로젝트 Logs 범위를 화면에 명시하고 기본7일, 기간 입력, 후보별 용량, 총 후보 용량, 정리 버튼과 결과를 제공한다.
- [ ] 미리보기 후 삭제 범위/용량을 확인할 수 있게 하고 실제 실행 때 잠금과 보관 조건을 재검사한다. Busy/Partial/Error를 성공 배지로 표시하지 않는다.
- [ ] 실행 중에는 정리를 보류하고, 보호/실패 경로와 사유를 표시한다. 같은 이름의 Build/ContentData 폴더를 삭제 범위에 포함하지 않는다.
- [ ] 설정 저장→검사→모의 빌드→결과 열기, 로그 미리보기→임시 로그 정리 흐름을 검증한다.
- [ ] Markdown 사용 안내와 진행 기록을 작성하고 3단계 계획·목표·실행문을 제시한다. 자동으로 3단계를 구현하지 않는다.

## 검토 중점

느린/실패한 조회가 다른 프로필의 최신 화면을 덮어쓰지 않는지, 프로필 변경 시 미저장 입력 유실이 없는지, 비밀번호가 예외 메시지에 섞이지 않는지, 앱 종료 후 실행 프로세스를 중복 시작하지 않는지, 높은 배율에서도 핵심 버튼이 접근 가능한지를 확인한다.
