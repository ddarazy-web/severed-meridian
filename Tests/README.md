# 프로젝트 테스트

게임 소스는 `Assets/Scripts`, 자동 검증 소스는 `Tests/Editor`에서 관리한다. 별도 Unity 프로젝트가 아니라 **같은 저장소의 독립 테스트 소스 영역**이다. Unity 버전·게임 에셋·패키지를 복제하지 않는다.

## 구조

- `Editor/Features/<기능>/`: 기능별 검증 소스와 기존 메타 파일
- `Editor/Systems/Popup/`: 팝업 시스템 검증
- `Editor/Features/Levels/Fixtures/`: 이전 저장 형식의 실제 원본 데이터
- `../Tools/Testing/ProjectTests.ps1`: 연결·해제·검사 실행
- `../Tools/Testing/Test-ProjectTests.ps1`: 연결 도구의 실파일 보호 검사

기존 `Assets/Scripts/Features/<기능>/Editor/Tests/`는 `Tests/Editor/Features/<기능>/`로, `Assets/Scripts/Systems/Popup/Editor/Tests/`는 `Tests/Editor/Systems/Popup/`로 이동했다. 이전 계획서와 검증 기록의 경로는 당시 이력으로 보존한다.

## 사용

저장소 루트에서 PowerShell 7로 실행한다. 연결 상태 변경 및 별도 배치 검사를 할 때는 **이 프로젝트의 Unity Editor를 저장 후 닫는다.** 도구는 실행 중인 프로젝트를 임의로 종료하지 않는다.

```powershell
# 연결 없이 상태 확인
./Tools/Testing/ProjectTests.ps1 -Action Status

# 연결 → Editor 컴파일 → 자동 해제 (게임 빌드 아님)
./Tools/Testing/ProjectTests.ps1 -Action Compile

# 연결 → 기존 검사 메서드 실행 → 자동 해제
./Tools/Testing/ProjectTests.ps1 -Action Run -Method Elements.Editor.ElementIdVerification.Run

# 이전 레벨 저장 형식 호환성 검사
./Tools/Testing/ProjectTests.ps1 -Action Run -Method Levels.Editor.LevelStorageBaselineVerification.Run

# 실행 도구 자체 검사
./Tools/Testing/Test-ProjectTests.ps1
```

기본 실행 파일은 ProjectVersion.txt에 기록된 버전의 Windows Unity Hub 설치 경로에서 찾는다. 다른 위치라면 `-UnityPath 'C:/경로/Unity.exe'`를 지정한다. 실제 UI 검사가 있어 `-nographics`를 사용하지 않는다. `Run`은 종료 시 `EditorApplication.Exit`를 호출하는 기존 배치 검사 메서드에 사용한다. 일반 메뉴용 메서드는 사용하지 않는다.

검사별 기존 결과 경로는 바뀌지 않는다. 과거 결과를 보존하려면 실행 전 해당 검사 출력 폴더를 별도 백업한다. 실행 도구 로그와 연결 소유 기록은 `Logs/TestHarness`에 남는다.

## 검사 종료 후 임시 사본 정리

`ProjectTests.ps1 -Action Run/Compile` 및 `JsonAuthoringChecks.ps1`의 독립 검사는 프로세스 종료 후 자동 정리한다. 실패 종료도 정리하지만, 프로세스 중단·에디터 사용 중·연결 해제 실패 시에는 보존한다.

- 실행 전 존재했던 폴더는 삭제하지 않는다. `TestArtifactCleanup.ps1`에 등록된 GUID 형식의 테스트 전용 폴더 중 이번 실행에서 생성된 폴더만 검사한다.
- 해당 폴더의 `project.json`, `draft.json`, `part-draft.json`과 그 임시·이전 사본, `snapshots/<GUID>/<번호>.json`만 삭제한다.
- 실제 편집의 `draft-*`, Candidates/Backups, 현재 선택된 정식 원본, 고정 입력, 로그·JSONL·스크린샷·보고서·ZIP은 보존한다. 이름이나 소유권이 불명확한 파일도 보존한다.
- 잠긴 파일 또는 링크가 있으면 해당 사본을 보존한다. `.cleanup.json`에 삭제 수·용량·보존 경로를 기록한다. 일반 에디터에서 직접 실행한 검사는 자동 정리 대상이 아니다.
- 새 테스트가 다른 경로를 사용한다면 전용 출력 경로와 사본 파일 형식을 확인한 뒤 정리 규칙 및 보호 검사를 함께 추가한다. 전체 Logs/Trials 삭제나 오래된 폴더 일괄 삭제는 하지 않는다.

정리 보호 검사: `./Tools/Testing/Test-TestArtifactCleanup.ps1`

## 수동으로 검사 연결

```powershell
./Tools/Testing/ProjectTests.ps1 -Action Attach
# Unity를 열고 기존 검사를 실행한다. 테스트 수정은 Tests 원본에서 한다.
# Unity를 저장 후 닫는다.
./Tools/Testing/ProjectTests.ps1 -Action Detach
```

테스트는 `Assets/__ProjectTests/Editor`에 복사되어 기존 `Assembly-CSharp-Editor`에서 컴파일된다. 기존 namespace·검사 진입점·테스트 간 참조를 유지한다. 임시 연결 경로는 Git에서 제외한다. 일반 개발 시에는 연결 해제 상태를 유지한다. **연결된 상태에서 빌드하지 않는다.** 기존 검사는 Editor 전용이지만 fixture까지 Assets에 일시 복사되므로 배포 작업 전에 Detach한다.

복사본을 수정하면 자동 해제가 거절된다. 변경을 `Tests/Editor`의 대응 원본에 반영한 뒤 복사본도 연결 당시 내용으로 복구하고 Detach한다. 추가 파일·폴더도 자동 삭제하지 않으므로 필요한 내용을 원본으로 옮긴 뒤 추가본을 직접 정리한다.

실행 중단 후에는 Status를 확인한다. Unity가 계속 실행 중이면 종료를 기다린 뒤 Detach한다. 연결 기록이 없는 기존 폴더는 자동 삭제하거나 덮어쓰지 않는다. `Logs/TestHarness/connection.json`은 해제 완료까지 유지해야 한다.

## 새 검사 추가

새 파일은 Assets가 아닌 `Tests/Editor/Features/<기능>` 또는 `Tests/Editor/Systems/<시스템>`에 추가한다. 기존 namespace와 검사 패턴을 따른다. `.meta`도 함께 관리하고 영구 게임 에셋을 검사 데이터로 수정하지 않는다. 일반 코드/Editor 도구에서 테스트 타입을 참조하지 않는다.

이번 분리에서는 NUnit 전환, 새 asmdef, 새 패키지, 독립 Unity 프로젝트를 추가하지 않았다.
