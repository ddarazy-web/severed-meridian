# 게임·제작 도구 분리 2단계 — 검증 기록

> 2026-10-08 · 2단계 구현·검증 완료. 후속 편집 UI/제품 빌드는 별도 단계다.

기준 커밋은 `4f55e70019c213e74e117fd14ec2353b3a391dd1`, 작업 브랜치는 `work`다. 기존 SO·팩·Addressables·프로필·기본 입력을 유지한다. 커밋/푸시/Player·Addressables 빌드/HTML 매뉴얼 변경은 하지 않는다.

## 구현 구조와 계약

- `Assets/Scripts/Features/LevelAuthoring/Documents`: 고정 v1 스키마, 엄격한 JSON 파싱, 사본을 격리하는 ContentSnapshot.
- `Validation`: 문서 필드·버전·ID·참조·공유 흐름 바인딩·카탈로그 자체 공급·배치 연결 검사. 모든 게임 규칙이나 클리어 가능성을 대신하는 검사는 아니다.
- `Storage`: UTF-8 원자 파일 교체와 이전 파일 보존, 세대별 문서 기록 후 project 공개. 스냅샷 revision은 project와 모든 하위 파일 해시를 포함한다.
- `Editor/Import`: SO 직렬화 필드를 직접 읽고 GUID→문서 ID를 재사용한다. 기존 실행 팩을 역변환하지 않는다.
- `Runtime`: 명시 리소스 ID와 제작 참조를 임시 SO에 복원한 뒤 기존 PuzzlePlayRequest/CreateTest로 실행한다. 임시 객체는 성공/실패 모두 정리한다.

[제작 필드 대응표](game-authoring-stage-02-field-map.md)에 원본 26개 SO, 전체 필드와 enum, 제외 항목, null/빈 목록, 저장·복구 경계를 기록했다. flow/sample은 임시 시험 자료를 함께 사용한다. Newtonsoft JSON 3.2.1을 Unity 패키지 API로 추가했고 독립 .NET 8.0.400 검사에서도 같은 DLL을 사용한다.

## 검증 명령과 증거

```powershell
& Tools/Testing/JsonAuthoringChecks.ps1 -Action All
& Tools/Testing/ProjectTests.ps1 -Action Status
```

`All`은 제작 왕복 → 내보내기 → 공유 fixture → 실행 입력 → 실제 Play Mode → 독립 검사 순서다. 테스트 원본은 Tests에 있고 Unity 실행 때만 임시 연결한다. 제품/콘텐츠 빌드를 실행하지 않는다. 최초 Portable 실행 전에는 Unity fixture 생성이 필요하다.

| 완료 조건 | 대응 검사·근거 | 현재 판정 |
|---|---|---|
| G1 필드/참조/제외 목록 | field-map 문서, 고정 AuthoringSchema와 실제 SerializedField 왕복 | 확인 |
| G2 8종류 보존 | authoring-roundtrip.txt 29건, shared-fixture-results.txt 8종류, 독립 문서 왕복 | 확인 |
| G3 안정 ID/출처/순서 | export-results.txt GUID 재사용·실패 시 대응표 보존, raw 직렬화 트리 비교 | 확인 |
| G4 공유/독립 의미 | shared-fixture-results.txt 7건: 두 레벨 공유·개별 값·완료 ID·이전 번호·독립 복사 | 확인 |
| G5 버전/키/참조/경로 | Portable 엄격 파서·누락·중복·미지원·리소스/공급/연결·경로 검사 | 확인 |
| G6 Unity 독립 실행 | 동일 Documents/Validation/Storage 소스와 Newtonsoft만 참조한 .NET 검사 | 122건 통과 |
| G7 원본/배포 불변 | export 원본 byte/meta/dirty 검사, git diff HEAD -- Assets ProjectSettings | 확인; 종료 뒤 재확인 완료 |
| G8 저장·실패·복구 | 임시 쓰기/재읽기/교체 실패, 실제 OS 잠금, project+하위 파일 외부 변경, 이전 세대 복구 | 확인 |
| G9 실행 동등성 | request-results.txt 실제 4레벨/표현 DTO 비교, play-results.txt 실제 보드·행동 | 66개 Play 비교/정리 항목 통과 |
| G10 정리·저장 격리 | request 변환 도중 실패 정리, Play 취소/실패/반복·정식 저장 불변 | 66개 Play 비교/정리 항목 통과 |
| G11 기록/해제/인계 | 본 문서·3단계 계획/목표/명령 작성 | 연결 해제 확인·인계 문서 작성 완료 |

최종 재검사 로그: `Logs/GameAuthoringStage02/all-final-audit.log`. 세부 결과는 같은 폴더의 authoring-roundtrip.txt, export-results.txt, shared-fixture-results.txt, request-results.txt, play-results.txt에 기록한다. 로그는 로컬 생성 자료이므로 커밋 대상 문서에는 결과와 실행 경로를 남긴다.

## 검토에서 보완한 내용

1. project만 같고 하위 JSON이 외부 수정된 상황을 실패 검사로 재현했다. 전체 스냅샷 revision을 시작/공개 직전에 비교하여 외부 내용을 보존한다.
2. 공유 흐름의 RequiredCount/Target 기본값을 바인딩보다 먼저 검사하던 문제를 재현하고 값 적용 후 검사하도록 수정했다. 단계/조건/파라미터 구조 검사는 공유 원본에서도 유지한다.
3. 레벨에서 사용하지 않는 카탈로그도 자체 정의 목록의 중복과 공급 참조를 검사한다. 실행기의 기본 ID 보충보다 앞선 카탈로그 검증 순서와 맞췄다.
4. envelope와 별도로 level payload schemaVersion 1~5를 허용하고 미래 버전을 차단한다. 시각 효과 경로 배열도 effectResourceIds로 보존한다.

## 남은 경계와 다음 단계

- 현재 JSON은 Assets 밖 `ContentData/Trials/game-authoring-stage-02/`의 시험 자료다. 기존 SO 편집 화면과 기본 게임 로더는 그대로다.
- 외부 프로세스를 모두 잠그는 다중 파일 트랜잭션은 아니다. 마지막 해시 검사 뒤 경쟁, 외부에서 삭제한 과거 세대 복구까지 보장하지 않는다.
- IL2CPP/실제 Android·iOS·Steam·Windows 앱은 빌드·실행하지 않았다. 리플렉션 기반 제작 어댑터의 AOT 보존 및 제품별 포함 경계는 후속 제품 검증 대상이다.
- [3단계 계획](../../Planning/project-wide/game-authoring-stage-03-plan.md), [목표](../../Goals/project-wide/game-authoring-stage-03-goal.md), [실행문](../../Commands/project-wide/game-authoring-stage-03-command.md)을 작성했다. 자동 구현하지 않는다.
- 공통 레벨툴 씬은 4단계 시범, 5단계 기존 기능과 미저장 보호 검증 뒤 Unity 기본 편집 화면으로도 전환한다.
## 최종 감사 결과

`All` 종료 코드 0, Unity 실행 5회 모두 종료 코드 0, 독립 검사 122개 통과. Play Mode 결과 66개 통과. `ProjectTests.ps1 -Action Status`는 테스트 연결 해제 상태다. `git diff --exit-code HEAD -- Assets ProjectSettings` 종료 코드 0으로 기존 추적 코드·SO·메타·팩·Addressables·프로필 불변을 확인했다. 신규 LevelAuthoring 파일과 메타는 별도 추가 파일이다. 패키지 변경은 공식 Newtonsoft JSON 3.2.1 직접 의존 추가뿐이다. HTML 변경 없음, work 브랜치 유지, 제품 빌드·커밋·푸시 없음. 문서 링크와 diff 공백 검사도 통과했다.

잘못된 MemoryPack 바이트를 넣는 실패 정리 검사에서 '레벨 1 시작 실패: Sequence reached end' 오류 로그가 의도적으로 발생한다. 해당 실행은 실패를 포착하고 객체 정리를 확인한 뒤 정상 종료했다. 이 로그를 정상 입력에서 발생한 미해결 오류나 컴파일 실패로 분류하지 않는다.

추가 null 감사에서는 카탈로그 null 목록/항목을 일반 예외 대신 ContentFormatException으로 거절하도록 보완했다. 중복 레벨 번호와 미지원 enum 키도 독립 검사에 포함했다. G1~G11의 범위는 위 표와 명시한 Editor/.NET 검증 경계에 해당한다.