# 디버그 빌드 옵션·공통 로그 — 구현·검증 기록

2026-10-16 · 구현·컴파일/모의/재로드/조회 회귀 검사 완료. 실제 Player 빌드 미수행.

## 동작 계약

- `Match → Build → 빌드 도구`에 **디버그 모드**를 등록한다. 기존 다섯 제품 공통이며 기본값은 OFF다. 마지막 선택은 기존 GUID별 개인 설정에 저장한다.
- ON은 Unity Development Build를 사용한다. `GameDiagnostics.GameLog.Debug(message, context)`는 `DEVELOPMENT_BUILD` 또는 `UNITY_EDITOR`가 정의된 호출부에서만 컴파일된다.
- OFF는 이번 실행의 Development·디버거/프로파일러 관련 플래그를 제거한다. 압축 등 관계없는 프로필 옵션은 보존한다. 원본 Build Profile이나 전역 Development 설정을 수정하지 않는다.
- 에디터에서는 작업 로그를 계속 볼 수 있다. 기존 Warning/Error/Exception은 억제하지 않는다. 외부 SDK의 직접 Unity 로그를 전역 차단하는 기능은 아니다.
- Release에서는 로그 호출뿐 아니라 문자열 생성·함수 호출 등 해당 로그의 인수 계산도 제외한다. 일반 `DEBUG` 심볼만으로 켜지지 않는다.

## 변경 위치와 사용

| 위치 | 책임 |
|---|---|
| `Assets/Scripts/Systems/Diagnostics/Runtime/GameLog.cs` | Conditional 기반 공통 로그 함수 |
| `BuildToolDebugMode.cs` | 기존 옵션 등록 방식의 디버그 모드, 요청별 결정, 읽기 전용 프로필 옵션 생성과 플래그 조정 |
| `BuildToolOptions.Default` | 실제 동작하는 첫 기본 옵션 등록. GPG 등 SDK 토글은 아직 없음 |
| `BuildToolRequest.debugMode` | 이번 실행 값 저장. 이전 요청·나중 UI 변경과 분리 |
| `UnityProfileBuildBackend` | 확인된 활성 프로필의 실제 실행 옵션에만 적용 |
| 기존 프로젝트 Editor 로그 9개 파일 | 기존 일반 로그 10개 호출을 공통 함수로 변경 |
| `PuzzleGameSession` | 준비에 성공한 세션의 레벨·시드 디버그 로그 |

```csharp
GameDiagnostics.GameLog.Debug("진행 상태: " + state, this);
```

Unity 오브젝트가 없는 곳에서는 두 번째 인수를 생략한다. 조건부 메서드를 델리게이트로 전달하지 않고 직접 호출한다.

## 빌드 연결 판단

Unity의 [6000.3 공식 BuildProfileModuleUtil 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/BuildProfile/BuildProfileModuleUtil.cs)에서 기존 Development 상태가 최종 옵션에 OR되는 것을 확인했다. `BuildPlayerWithProfileOptions.options = None`만으로는 이미 켜진 Development를 OFF로 만들 수 없다.

Ruling: 원본 설정을 임시 수정하는 대신, [공식 BuildPipeline 프로필 wrapper](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/BuildPipeline.bindings.cs)가 사용하는 동일 프로필 옵션 계산 API를 재사용하고 최종 플래그만 조정한다. 임의 씬 목록으로 제품 계약을 우회하지 않는다.

- 활성 프로필 GUID·플랫폼이 확정 요청과 같은지 다시 확인한다.
- 프로필의 씬·타깃·서브타깃·출력·기존 옵션을 Unity가 계산한 결과에서 가져온다.
- 내부 API의 이름·파라미터·반환 형식을 검사한다. 버전 변경으로 API가 사라지면 추측하지 않고 실행 전에 차단한다.
- 조정한 옵션은 기존 BuildGuard·MemoryPack·Addressables 등 동일 Player 빌드 파이프라인에 전달한다.
- 이전 형식의 미완료 요청처럼 `debugMode`가 없는 경우에는 기존 프로필 기반 호출을 유지한다.
- ON은 Development만 추가하며 디버거 접속·프로파일러 연결을 임의로 활성화하지 않는다. 기존 프로필이 제공한 옵션은 보존한다.

## 검증

### 조건부 로그의 실제 컴파일 검사

`& ./Tools/Testing/DebugLogVerification.ps1`

설치된 Windows C# 컴파일러로 실제 GameLog 소스와 Unity 로그 스텁을 컴파일·실행한다. 게임 Player 빌드는 아니다. 임시 자료는 Git 제외 `Logs/DebugLogging` 아래에 둔다. SDK를 설치하지 않는다.

| 호출부 심볼 | 출력 횟수 | 메시지 인수 함수 실행 횟수 |
|---|---|---|
| 없음 | 0 | 0 |
| DEVELOPMENT_BUILD | 2 | 2 |
| UNITY_EDITOR | 2 | 2 |
| DEBUG만 있음 | 0 | 0 |

최초 공통 함수 부재를 실패로 확인한 뒤 위 네 구성을 통과했다. 근거 디렉터리: `Logs/DebugLogging/53814f8f301645a0a275b156bee60950`.

### Unity 검사

`BuildToolDebugModeVerification.Run` / `Logs/TestHarness/20261016-082411-496-Run.log`: exit 0.

- 모든 제품의 등록·기본 OFF, 요청값 고정·저장, ON→OFF 분리, 프로젝트 설정 변경 없음.
- 기존 개발/디버거/프로파일러 플래그 제거와 압축 옵션 보존.
- 설치된 Unity 내부 API의 형식과 네이티브 GetBuildOptions 결과의 ON/OFF 변환.
- 다른 활성 프로필로 요청을 우회하지 않는 검사, 실제 UI의 저장된 ON→OFF 변경.
- 기존 BuildToolStateSnapshot의 파일·메모리·활성 상태 보존.

최초 Unity 검사 `20261016-082156-342-Run.log`는 활성 제품 프로필이 있을 것으로 가정하여 실패했다. 현재 프로젝트는 기본 프로필 상태이므로 테스트 때문에 제품을 활성화하지 않도록 수정했다. 이 경우 제품 기반 씬·서브타깃 생성 실행은 미검증으로 명시하며, API 형식·네이티브 플래그 계산·바인딩 차단·순수 변환·UI만 검사한다.

실제 Player/APK/AAB/iOS·Addressables 빌드와 기기 로그는 미검증이다. 커밋·푸시·SDK 설치·HTML 매뉴얼 갱신은 하지 않는다. 기존 작업 변경을 보존한다.

## 독립 검토

새 컨텍스트의 읽기 전용 검토에서 중요한 결함이나 회귀는 발견되지 않았다. Conditional 호출/인수 제거, 기본 OFF와 요청 저장, 활성 프로필 바인딩, 같은 Unity 옵션 factory 재사용, OFF의 개발 관련 플래그 제거를 검토했다. 검토자는 Unity·검사·실제 빌드·소스 수정·커밋을 하지 않았다. 최종 검증 근거는 아래 실제 실행 로그이며, 활성 제품 프로필의 옵션 생성/빌드 미검증 제한은 유지한다.

## 최종 회귀 검사

모든 메서드는 `Tools/Testing/ProjectTests.ps1 -Action Run -Method Products.Editor.<메서드>.Run`으로 순차 실행했다. 실제 BuildPlayer는 호출하지 않았다.

| 메서드 | 최종 로그 (`Logs/TestHarness/`) | 결과 |
|---|---|---|
| BuildToolDebugModeVerification | `20261016-082411-496-Run.log` | exit 0 |
| BuildToolOptionsVerification | `20261016-082600-415-Run.log` | exit 0 |
| BuildToolOptionsReloadVerification | `20261016-082633-494-Run.log` | exit 0 |
| BuildToolPreflightVerification | `20261016-082706-843-Run.log` | exit 0 |
| BuildToolExecutionVerification | `20261016-082736-318-Run.log` | exit 0 |
| BuildToolExecutionReloadVerification | `20261016-082805-426-Run.log` | exit 0 |
| BuildToolWindowVerification | `20261016-082941-873-Run.log` | exit 0 |

창 검사의 `20261016-082837-762-Run.log` 실패는 손상된 개인 설정을 안내하는 초기 상태에서 옵션 토글이 이미 있을 것으로 가정한 검사 오류였다. 정상 프로필 선택으로 개인 기록을 복구한 뒤 토글을 확인하도록 순서를 수정했다. 제품 코드의 손상 기록 처리나 자동 빌드 동작을 바꾸지 않았다.

최종 신규 메타데이터·관련 문서 링크·Git 변경을 확인하고 임시 테스트 연결을 해제했다. 기본 실제 요청 파일 `Library/MatchBuildTool/current.json`은 생성하지 않았다. 기존 프로필·PlayerSettings·Addressables 원본과 활성 상태는 각 검사의 스냅샷 비교 범위에서 보존됐다. 경고·오류·예외 로깅과 외부 SDK 소스는 변경하지 않았다.
