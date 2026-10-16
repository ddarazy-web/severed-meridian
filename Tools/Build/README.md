# 외부 빌드 사용법

Unity 창에서 버튼을 누르는 대신 프로젝트 루트의 `Build.bat`로 실행합니다. 기존 **Match → Build → 빌드 도구**의 제품 정의, 옵션 등록표, 사전 검사, 빌드 실행기, 콘텐츠 변환, 후처리와 설정 복원을 그대로 사용합니다.

## 1. 실행 전 준비

1. 이 프로젝트를 열어 둔 Unity를 저장하고 닫습니다. 다른 프로젝트는 열려 있어도 됩니다.
2. `ProjectSettings/ProjectVersion.txt`에 지정된 Unity와 해당 플랫폼 빌드 모듈을 설치합니다. 정상적인 Unity 라이선스도 필요합니다.
3. Android 사용자 서명을 사용하는 경우 에디터 빌드 도구에서 **해당 프로필의 Keystore·Alias 비밀번호를 안전하게 저장**합니다.
4. 명령 프롬프트 또는 PowerShell에서 프로젝트 폴더로 이동합니다.

```bat
cd /d C:\Projects\Git\ServeredMeridian
Build.bat -Help
```

위 `cd /d`는 명령 프롬프트용입니다. PowerShell에서는 `Set-Location C:\Projects\Git\ServeredMeridian` 후 `./Build.bat`로 실행하세요. 다른 위치에서도 BAT의 전체 경로로 실행할 수 있습니다.

## 2. 프로필과 옵션 확인

```bat
Build.bat -ListProfiles
```

Unity를 숨김 배치 모드로 실행하여 **현재 등록된** 프로필과 옵션 ID·설명·기본값을 조회합니다. 목록 조회만 하므로 Player/Addressables 빌드나 플랫폼 전환은 하지 않습니다. Unity 시작·컴파일 시간은 걸릴 수 있습니다.

현재 프로필 이름:

| 이름 | 제품 |
|---|---|
| `Android Game` | Android 게임 |
| `Android Level Editor` | Android 레벨툴 |
| `Windows Steam Game` | Windows 게임 |
| `Windows Level Editor` | Windows 레벨툴 |
| `iOS Game` | iOS 게임 |

이름은 공백을 포함하므로 큰따옴표로 감싸고 정확히 입력합니다. 목록은 BAT에 복제하지 않으므로 제품·옵션 등록이 추가되면 자동으로 반영됩니다. 목록에 보여도 해당 운영체제에서 빌드할 수 있다는 뜻은 아닙니다. 이 실행기는 Windows용이며 iOS 지원 모듈·호스트 제약을 우회하지 않습니다.

## 3. 빌드 없이 먼저 확인하기

```bat
Build.bat -Profile "Android Game" -Format apk -DebugMode off -Check
Build.bat -Profile "Windows Level Editor" -Check
```

`-Check`는 프로필·모듈·기존 공통 설정과 옵션 조합을 검사하고 최종 옵션·출력 경로를 보여줍니다. **서명 비밀번호, 플랫폼 전환 후 컴파일, 전체 의존성, 실제 빌드 성공까지 보장하는 검사는 아닙니다.** 결과가 성공이어도 실제 빌드 중 오류가 발생할 수 있습니다.

## 4. 실제 빌드

프로젝트 루트의 앱별 배치파일을 실행하면 프로필 이름을 입력할 필요가 없습니다.

| 배치파일 | 선택하는 프로필 |
|---|---|
| `Build-AndroidGame.bat` | Android Game |
| `Build-AndroidLevelTool.bat` | Android Level Editor |
| `Build-WindowsGame.bat` | Windows Steam Game |
| `Build-WindowsLevelTool.bat` | Windows Level Editor |
| `Build-iOSGame.bat` | iOS Game |

추가 인자 없이 실행하면 해당 프로필에 저장된 옵션을 사용합니다. 옵션을 붙이면 이번 실행에만 적용합니다. `-Profile`은 이미 지정되어 있으므로 다시 넣지 마세요. 전체 프로필 조회는 공통 `Build.bat -ListProfiles`를 사용합니다.

```bat
Build-WindowsLevelTool.bat
Build-AndroidGame.bat -Format apk -DebugMode off
Build-AndroidGame.bat -Format aab -Check
Build-AndroidLevelTool.bat -Format apk
Build-WindowsGame.bat -UseDefaults -DebugMode off
```

PowerShell에서는 파일명 앞에 `./`를 붙입니다. iOS 배치파일도 같은 실행 경로를 사용하지만 Windows에서 iOS 최종 앱을 완성하는 기능은 아닙니다. 기존 호스트·모듈 제약을 따르며 최종 iOS 빌드와 서명에는 macOS/Xcode가 필요합니다.

공통 배치파일에 프로필을 직접 지정하는 방식도 계속 사용할 수 있습니다.

```bat
Build.bat -Profile "Android Game" -Format apk -DebugMode off
Build.bat -Profile "Android Game" -Format aab -DebugMode off
Build.bat -Profile "Android Level Editor" -Format apk -DebugMode off
Build.bat -Profile "Windows Steam Game" -DebugMode off
Build.bat -Profile "Windows Level Editor" -DebugMode off
```

- `-Format apk`: Android 기기에 직접 설치할 APK.
- `-Format aab`: Google Play 업로드용 AAB.
- `-DebugMode on`: 디버그 옵션 활성화. `off`: Release 옵션.
- Android 형식을 Windows 프로필에 지정하면 오류로 종료합니다.
- 출력 위치는 기존 제품 정의를 따릅니다. 예: `Builds/Android/Game.apk`, `Builds/Steam/MoonRabbit.exe`, `Builds/LevelTool/MoonRabbitLevelTool.exe`.
- 실제 빌드는 기존 Addressables·MemoryPack 등 콘텐츠 생성도 실행합니다. 결과를 스토어에 업로드하거나 커밋·푸시하지는 않습니다.
- 실제 빌드에서는 Unity 시작 인자로 선택한 Build Profile을 지정하여 대상 플랫폼으로 시작합니다. 이후 옵션 적용·실행·후처리는 기존 빌드 도구가 담당합니다. 프로필 에셋은 기존 제품 정의 규칙대로 `Assets/Settings/BuildProfiles/<프로필 이름>.asset`을 사용합니다.
- 동일 출력 경로에 재빌드하면 기존 산출물이 갱신됩니다. 보관할 빌드는 먼저 별도 폴더로 복사하세요.

## 5. 저장된 선택과 직접 지정한 옵션

빌드 도구의 **앱 버전**에서 `Version Name`과 Android의 `Version Code` 또는 iOS의 `Build Number`를 입력할 수 있습니다. Enter 또는 포커스 이동으로 입력을 확정하면 해당 Build Profile에 자동 저장하며, Unity 재실행과 외부 BAT 빌드 모두 같은 값을 사용합니다. Windows에는 Version Name만 적용합니다. 버전은 자동 증가하지 않습니다.

이 버전 값은 개인 옵션 캐시가 아니라 Build Profile 에셋에 저장됩니다. 따라서 다른 PC와 공유하려면 해당 프로필 변경도 함께 전달하세요. `-UseDefaults`는 옵션 선택에만 적용되며 저장된 앱 버전을 초기화하지 않습니다. `-Check` 결과의 `appVersion`에서 적용될 값을 확인할 수 있습니다.

기본적으로 빌드 도구의 `UserSettings/MatchBuildTool.json`에 저장된 **프로필별 옵션**을 읽습니다. 외부 명령의 값이 있으면 이번 실행에만 덮어쓰고, UI의 저장된 선택은 변경하지 않습니다.

우선순위: **명령에서 지정한 값 → 해당 프로필의 저장값 → 옵션 등록 기본값**.

개인 저장값을 무시하고 기본값부터 시작하려면:

```bat
Build.bat -Profile "Windows Level Editor" -UseDefaults -DebugMode off
```

옵션 ID를 직접 지정할 수도 있습니다. 새로운 옵션이 등록되면 BAT 수정 없이 이 방식을 사용합니다.

```bat
Build.bat -Profile "Android Game" -Option "debug-mode=off,android-app-bundle=on"
```

값은 `on/off` 또는 `true/false`입니다. 알 수 없는 ID, 해당 제품에 적용되지 않는 ID, 중복 지정은 오류로 처리합니다. 예를 들어 `-DebugMode off`와 `-Option debug-mode=on`을 함께 쓰지 마세요. GPG 등의 미구현 옵션은 사용할 수 없습니다.

## 6. Unity 설치 위치가 다른 경우

기본 위치는 `C:\Program Files\Unity\Hub\Editor\<프로젝트 버전>\Editor\Unity.exe`입니다. 다른 위치라면 프로젝트와 같은 버전의 실행 파일을 지정합니다.

```bat
Build.bat -Profile "Windows Level Editor" -UnityPath "D:\Unity\6000.3.10f1\Editor\Unity.exe"
```

PowerShell을 직접 사용할 수도 있습니다.

```powershell
& ./Tools/Build/Build.ps1 -Profile 'Windows Level Editor' -DebugMode off
$LASTEXITCODE
```

## 7. 결과와 오류 확인

### Logs 자동 정리

앱별 BAT 또는 공통 `Build.bat`로 **실제 빌드를 시작하기 전** 최근 7일 보관 정책을 적용합니다. `-Help`, `-ListProfiles`, `-Check`에서는 정리하지 않습니다. Unity 에디터의 빌드 버튼이나 PC가 유휴 상태일 때 별도로 실행되는 예약 작업은 아닙니다.

- 정리 범위는 프로젝트 루트의 `Logs`뿐입니다. `Assets`, `Tools`, `Tests`, `Builds`, `Library/MatchBuildTool`은 대상이 아닙니다.
- 일반 작업 폴더는 하위 파일·폴더가 모두 7일보다 오래된 경우에만 삭제합니다. 루트에 놓인 오래된 로그·임시 스크립트도 포함합니다.
- `ExternalBuild`는 실행별 폴더 단위로 정리합니다. `result.json`이 없는 미완료 실행 폴더와 잠금 파일은 계속 보존합니다. 중단된 빌드 기록은 확인 후 수동 정리하세요.
- 외부 빌드 또는 Unity가 실행 중이거나 빌드 도구에 미완료 요청이 있으면 전체 정리를 건너뜁니다. 빌드 상태를 읽지 못하는 경우에도 삭제하지 않습니다.
- 사용 중인 파일이나 링크·정션이 들어 있는 폴더는 삭제하지 않습니다. 정리 오류는 표시하며 자동 빌드 자체를 정리 오류 때문에 실패시키지는 않습니다.
- 빌드 시작 출력에 정리 상태와 삭제 항목 수·용량을 표시합니다.

수동으로 확인하거나 다른 보관 기간을 한 번 적용할 수 있습니다.

```bat
Clean-Logs.bat -Preview
Clean-Logs.bat
Clean-Logs.bat -RetentionDays 14 -Preview
Clean-Logs.bat -RetentionDays 14
```

`-Preview`는 삭제하지 않고 후보를 보여줍니다. `RetentionDays`는 1일 이상이며, 생략 시 7일입니다. 수동으로 지정한 기간은 그 실행에만 적용됩니다. 결과의 `Status`는 `Complete`(완료), `Busy`(사용 중이라 생략), `Partial`(일부 항목 실패), `Error`(안전 확인 실패)이며 `Skipped`, `Errors`도 함께 확인하세요.

실행마다 `Logs/ExternalBuild/<날짜-고유번호>/`가 생깁니다.

| 파일 | 내용 |
|---|---|
| `request.json` | 프로필 이름과 명시한 옵션. 비밀번호는 저장하지 않음 |
| `Unity.log` | Unity 시작·컴파일·빌드 전체 로그 |
| `result.json` | 성공/실패, 요청 ID, 출력 경로, 오류, 기존 빌드 도구 로그 위치 |

실제 빌드 이력은 기존과 동일하게 `Library/MatchBuildTool`에도 남습니다. 성공 여부는 파일이 존재한다는 사실만으로 판단하지 않고 **종료 코드와 result.json**으로 확인하세요. 이전 빌드 파일이 남아 있을 수 있습니다.

| 종료 코드 | 의미 |
|---|---|
| `0` | 요청한 목록 조회·사전 검사·실제 빌드 완료 |
| `1` | 실제 빌드 실패 또는 취소 |
| `2` | 인자·실행 환경·서명 준비 오류, 미완료 요청, 결과 파일 없음 등 확인 필요 |

명령 프롬프트에서는 `echo %ERRORLEVEL%`, PowerShell에서는 `$LASTEXITCODE`로 확인합니다. BAT/PowerShell 자체를 잘못 호출해 파라미터 바인딩이 실패한 경우도 0이 아닌 코드로 종료됩니다.

### 자주 만나는 상황

- **프로젝트가 열려 있음:** 해당 프로젝트 Unity를 닫고 다시 실행합니다. 기존 에디터를 자동 종료하지 않습니다.
- **외부 빌드가 이미 실행 중:** 같은 프로젝트의 외부 실행은 한 번에 하나만 허용됩니다.
- **미완료 요청:** Unity 빌드 도구에서 로그와 산출물을 확인하고 `중단 요청 정리` 기능을 사용한 뒤 다시 실행합니다. 외부 도구가 임의로 정리하거나 재빌드하지 않습니다.
- **서명 복원 실패:** 같은 Windows 계정에서 같은 프로젝트 경로·프로필·Keystore·Alias로 비밀번호를 다시 저장합니다. 암호화 파일을 다른 PC/계정에 복사하는 것만으로는 사용할 수 없습니다. BAT·명령행·요청 JSON에 비밀번호를 넣지 마세요.
- **Unity가 결과 없이 종료:** 표시된 `Unity.log`에서 컴파일·라이선스·모듈 오류를 확인합니다.
- **강제 종료/전원 중단:** 빌드가 끝났다고 간주하지 않습니다. 자식 Unity가 살아 있는지 확인하고, 다음 실행 전 기존 빌드 도구에서 미완료 요청을 점검합니다.

빌드 동안 플랫폼 전환과 재컴파일이 발생해도 동일 요청을 이어갑니다. 자동 재시도나 무제한 새 빌드를 수행하지 않으며, 종료 후 선택한 제품 프로필은 기존 빌드 도구와 마찬가지로 유지됩니다.
