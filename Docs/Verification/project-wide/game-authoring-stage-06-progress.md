# 게임·제작 도구 분리 6단계 — 진행 기록

2026-10-09 · 구조·데이터·프로필 검증 완료. 아래 기록은 시간순 진행 이력이며 최종 상태는 「최종 실행 결과」와 완료 판정을 따른다.

[계획](../../Planning/project-wide/game-authoring-stage-06-plan.md) · [목표](../../Goals/project-wide/game-authoring-stage-06-goal.md)

## 착수 확인

- 5단계 완료·독립 리뷰와 검사 연결 해제 확인. work 브랜치의 모든 미커밋 변경을 보존한다.
- Unity 6000.3.10f1, Addressables 4.1.0, Newtonsoft.Json 3.2.1, MemoryPack 1.21.4를 현재 파일에서 확인했다.
- 현재 Generate는 SO 검색과 순차 파일 쓰기이므로 JSON 스냅샷 고정·전체 검증·실패 복구 경계가 필요하다.
- 커밋·푸시·Player/Addressables 빌드·HTML 변경 금지. 데이터 직렬화·Unity 검사만 수행한다.

## A — 후보 이관

- `CandidateMigrationVerification` RED091002-991: 후보 전용 API 없음.
- `LegacyContentExporter.ExportCandidate`를 별도 경로로 추가했다. 기존 시험 폴더 제한은 그대로 둔다.
- 후보 경로는 ContentData/Candidates의 명시적 하위 폴더이며 원본/메타 SHA256, GUID→문서 ID, 요소 기획 출처, 리소스 매핑과 공개 스냅샷 해시를 보고한다.
- GREEN091122-541: 전체 원본 목록·해시·출처·ID 연결, 반복 이관의 안정 ID, 이전 후보 복구본과 원본 파일 불변 통과.
- 확대 GREEN091238-800: 중단·중복 원본·이관 도중 미저장 원본 변경 시 기존 후보와 보고서 보존 통과. 검사 연결 해제.
- 기본 원본 채택은 아직 미구현이며 현재 선택과 출시 팩을 바꾸지 않았다.

## C — 제품 사전 조사(읽기 전용)

- 네 프로필에 기존 PRODUCT_ANDROID_GAME / PRODUCT_IOS_GAME / PRODUCT_STEAM_GAME / PRODUCT_LEVEL_EDITOR 심볼 및 PuzzleGame/LevelTool 씬 지정 존재. Windows subtarget 2는 설치된 Unity enum에서 Player임을 확인했다.
- 제품 심볼을 사용하는 코드 경계/검사기는 아직 없다. LevelAuthoring/LevelTool은 기본 어셈블리에서 무조건 컴파일된다. 외부 게임 Runtime 호출은 발견되지 않았다.
- Resources 비어 있음, StreamingAssets 없음, preloadedAssets 비어 있음. Addressables는 표현 28개·요소 팩 1개·레벨 팩 1개. 상세 포함 경계는 AssetDatabase 재귀 의존 검사로 확인해야 한다.
- Android/iOS/Windows 모듈 디렉터리와 Android SDK/NDK/JDK 설치 흔적은 있다. Steam SDK는 발견되지 않았다. 설치 흔적은 빌드·서명·실기 성공을 뜻하지 않는다.
- 출력 위치/제품별 PlayerSettings 구분과 실제 플랫폼 검사·코드 격리는 아직 남아 있다.
- 후보 채택은 원본 전체 필드를 정규 JSON 계약으로 비교하며 기획 문서·리소스 존재를 검증한다. 표현 이름은 실제 아틀라스 등록 스프라이트로 해석한다. 메모리 float/파일 double의 직접 비교 대신 기존 정규 직렬화 계약을 대조한다.
- adoption-canonical-green / 091914-580: 검증 후 선택 저장·재열기, 오래된 보고서 거절 시 기존 선택 불변 통과.
- candidate-relocation-green / 092044-998: 상대 경로 설정과 후보 폴더 이동, 안정 ID, 미지원 스키마 거절까지 통과.
- legacy-export-regression / 092126-092: 기존 시험용 내보내기의 27문서·재이관 ID·실패/원본 보존 회귀 통과. 모든 검사 연결 해제.
- 기본 원본 채택 API는 격리된 시험 설정 파일로만 검증했다. 정식 선택 설정/출시 팩은 아직 변경하지 않았다. 다음은 같은 스냅샷의 레벨·요소 팩 변환과 원자적 교체다.

## B — 같은 스냅샷의 팩 변환

- JsonPackConversionVerification RED092238-552에서 변환기 부재 확인 후 JsonContentPackBuild.CreateBytes를 추가했다.
- GREEN092345-629: 50/51·100/101의 기존 구간 주소, 결번 거절, 기존 코덱으로 요소/레벨을 읽은 결과의 정확한 스냅샷 동등성, 제작 세션 불변 통과.
- 변환기는 파일을 쓰지 않으며 같은 고정 문서 그래프에서 모든 바이트를 만들고 디코드 검증한다. 세대 확인·원자적 공개·Addressables 등록·기본 입력 연결은 아직 남아 있다.
- 세대 정보(MemoryPack)에 원본 해시와 주소별 파일 해시를 기록했다. generation-green / 092630-257에서 변조된 팩 거절 확인. 게임 로더 연결은 아직 전이다.
- publication 초기 성공 결과만으로 실패 복구를 완료 판정하지 않았다. 확대 검사에서 File.Replace의 null 백업 경로 오류가 의도한 실패보다 먼저 발생함을 확인했다. 주입한 오류만 인정하도록 검사를 보강하고 명시적 교체 백업·동일 파일 유지·복구 오류 보존으로 수정했다.
- publication-recovery-green-2 / 093421-725: 실제 중간 교체 실패·원본 바이트/메타 복원·중단 저널 재복구·변환 전후 원본 변경 거절·이전 완전한 세트 유지가 모두 통과했다. 출력은 Logs의 격리 폴더이며 출시 팩은 변경하지 않았다.
- generation-mixed-green / 093533-338: 실제 다른 정상 JSON 세대의 레벨 팩을 이전 세대 정보와 혼합하면 거절됨을 확인했다. 테스트 연결 해제. 파일/Addressables 통합 공개와 실제 로더·도구 시험 모드·제품 경계·정식 채택은 계속 진행할 미완료 범위다.

## 게임 로더 세대 검증 연결

- `LevelPackLoader`는 세대 정보를 한 번 읽고 동일한 정보로 요소·레벨 팩을 검증한다. 독립 요소 로더에도 세대 검증을 적용했다.
- `GenerationLoaderVerification`: 미구현 실패 확인 후 정상 경로, 요소/레벨 혼합 거절, 취소된 요청의 로딩 방지를 통과했다. 로그: `Logs/TestHarness/20261009-094227-287-Run.log`, exit 0.
- 검사는 바이트 공급을 주입한 실제 로더 경로다. 실제 Addressables 등록/로딩 검증은 남아 있다. 현재 세대 파일 등록 전이므로 정식 게임 연결 완료로 보지 않는다.
- 테스트 연결 해제 및 diff 검사 완료. 커밋·푸시·빌드·HTML 변경 없음.

## 팩·Addressables 등록의 통합 복구

- 팩 교체와 명시한 Addressables 설정/그룹/메타를 같은 복구 기록에 포함했다. 등록 이후에도 JSON 원본 변경 여부를 확인한다.
- 파일 단위 실패·재시작 복구 검사 통과: `Logs/TestHarness/20261009-094756-859-Run.log`.
- 실제 Addressables 그룹 등록 직후 실패 주입 검사 통과: `Logs/TestHarness/20261009-095005-889-Run.log`. 등록이 실제 일어난 뒤 기존 팩·설정·GUID·로드된 그룹 주소가 복원됐고 새 세대 파일/메타는 남지 않았다.
- 프로젝트에 이미 구성된 두 팩 그룹/스키마를 사용한다. 그룹이 없거나 저장되지 않은 설정이 있으면 명확히 중단하며, 변환 중 새 그룹을 임의 생성하지 않는다.
- 성공한 등록의 정식 채택, 기본 Generate 연결, 실제 게임 로딩 확인과 제품 경계 검증은 남아 있다. 기존 배포 데이터에 검사 변경이 남지 않았으며 빌드는 수행하지 않았다.

## 기본 변환 경로 및 정식 JSON 채택

- `LevelPackBuild.Generate()`를 선택된 JSON → 검증 → 팩 생성/등록으로 전환했다. SO 자동 대체를 제거했다.
- 선택된 JSON의 이동 횟수를 변경한 후 모든 생성 바이트와 주소가 일치하는 검사 통과: `Logs/TestHarness/20261009-095317-061-Run.log`.
- 정식 후보 `ContentData/Candidates/main`의 27개 문서를 검증하여 `ProjectSettings/AuthoringSource.json`으로 채택했다. 레벨·요소·세대 정보 3개 산출물 일치 검사 통과: `Logs/TestHarness/20261009-095440-749-Run.log`.
- 최초 채택 전 팩/설정 복구본과 절차는 `ContentData/Backups/game-authoring-stage-06/`에 있다. 원본 SO는 그대로 보존한다.
- 채택 후 등록 실패 복구 회귀도 통과했다: `Logs/TestHarness/20261009-095616-290-Run.log`. 기존 생성 팩 바이트는 원본과 동일하며 세대 파일/주소가 추가됐다.
- 실제 공개 Addressables 로더의 Play Mode 검증, 도구 기본 폴더/팩 시험, 제품 경계 검증과 최종 감사는 남아 있다. 6단계 완료로 표시하지 않는다.

## 레벨툴 편집본 / 생성 팩 시험 구분

- 게임 시험 입력을 `현재 편집본 JSON` 또는 `생성된 MemoryPack`으로 선택한다. 팩 모드는 미저장 편집과 공유 튜토리얼 사본을 반영하지 않으며 SO로 대체하지 않는다.
- `팩 상태 확인`과 상태 문구로 현재 편집본과 팩의 원본 해시 일치 여부를 표시한다. 게임 시작 시 실제 로더가 사용한 세대로 다시 확인한다.
- 동일 JSON/팩의 실행 요청과 시드 일치, 변경 후 해시 불일치 검사 통과: `Logs/TestHarness/20261009-100137-885-Run.log`.
- 실제 레벨툴 Play Mode에서 팩 선택, 일치/불일치 문구, 공개 Addressables 로더를 통한 게임 시험 준비, CreateTest 문맥, 복귀 후 미저장 상태 보존 확인: `Logs/TestHarness/20261009-100641-346-Run.log`, exit 0.
- Addressables Fast Play만 사용했다. 기존 모드 및 EditorSettings를 복원했고 번들/Player 빌드는 하지 않았다. 실제 배포 번들·실기 검증을 대신하지 않는다.
- 기본 JSON 폴더의 안전한 시작 연결, 네 제품의 설정/코드/리소스 경계 및 최종 검증은 아직 남아 있다.

## 최종 검토와 보완

- 독립 읽기 전용 검토에서 채택된 JSON 재이관 덮어쓰기와 저장 경로에 따른 잘못된 팩 불일치를 발견했다.
- `ExportCandidate`는 기본 원본 폴더와 이관 보고서 이후 편집된 후보를 거절한다. 새 후보에 이관하고 검증하는 경로는 유지한다. `103550-014`에서 재현, `103743-532`에서 보호·반복 이관·원본 불변 검사가 통과했다.
- 콘텐츠 해시는 프로젝트의 문서 저장 경로를 제외한다. `103837-039`에서 무변경 사본 저장 시 불일치를 재현했고, `103929-744`에서 사본 저장·무변경 저장은 같은 해시, 실제 필드 변경은 다른 해시임을 검증했다. 저장 충돌 감지는 별도의 파일 해시를 계속 사용한다.
- 구형 SO Inspector는 읽기 전용이며 명시적인 가져오기와 기본 JSON 도구 진입을 제공한다. `102919-032`에서 저장된 원본 검사 통과. 제작 SO 7종의 팩 포함 거절 및 공유 튜토리얼 변환 검사는 `102951-438`에서 통과했다.
- 제품별 설정·심볼·시작 씬·의존성·테스트 포함 거절 검사는 `103022-695`에서 통과했다. 코드 격리 뒤 독립 .NET 검사 280개가 통과했다(`portable-isolation.log`). 실제 네 플랫폼 Player 컴파일 성공을 뜻하지 않는다.

## 완료 조건별 근거 연결

| 조건 | 구현과 확인 근거 |
|---|---|
| G1 | 5단계 기능 대응표와 검증 기록, 기본 레벨툴 진입 유지 |
| G2 | main의 migration-report / migration-references, 26개 원본과 27개 문서, 최초 전환 복구본 |
| G3·G4 | CandidateMigrationVerification: 원본 전체 필드·ID·메타·리소스·기획 출처, 실패 채택·이관 중단·외부 변경·폴더 이동·미지원 버전·재이관 보호 |
| G5 | JsonPackPublicationVerification / RegisteredPackVerification: 고정 스냅샷, 파일·등록·메타 롤백 및 중단 저널 복구 |
| G6 | JsonPackConversionVerification / GenerationLoaderVerification: 50번호 구간·결번·중복·공유 튜토리얼·세대 혼합 거절 |
| G7 | PackedTrialVerification / PackedToolPlayVerification: 입력 선택·원본 해시·오래된 팩 표시·실제 Addressables Fast Play |
| G8 | ProductBoundaryVerification / ProductSelectionVerification 및 products 문서: 네 구성 감사와 현재 타깃 선택 검사 |
| G9 | ProductionJsonAdoption / LegacyInspectorVerification: 기본 JSON, 구형 원본 읽기 전용, 게임 MemoryPack 로더 |
| G10 | 이 기록과 migration/products 문서, 최종 보고의 실행 범위 및 미검증 배포 게이트 |

이 표는 완료 조건과 아래 최종 실행 결과의 근거를 연결한다.

### 제품 출력 경로 재현

`103047-862`와 `104011-157`에서 Android 출력 경로가 빈 값이었다. 진단 `104231-797`은 부모가 없는 상대 경로가 빈 값으로 기록되고, 같은 절대 경로는 기록되며, 부모가 있는 상대 경로도 기록됨을 확인했다. 출력 파일을 만들거나 빌드하지 않았다. 제품 선택 뒤 절대 경로를 설정하도록 수정했다. `104358-299`에서 출력·콘텐츠 라우팅·빌드 사전 검사와 원래 선택 복원은 통과했다. 마지막 검사에서 content-state 원시 직렬화 값과 해석된 getter를 혼동한 비교를 별도로 보완한다.
- 제품 최종 선택/복원 검사 `104503-410` exit 0: Android의 실제 선택·출력·콘텐츠 연결, 네 제품 카탈로그 루트 분리, 이전 선택과 raw/resolved content-state 복원 및 전역 Player Settings 불변을 확인했다. 다른 세 제품의 실제 타깃 전환/도메인 재로드 실행은 수행하지 않았다.
- 최종 파일 대조: 이관 보고서의 제작 SO 26개와 각 메타 SHA-256가 현재 파일과 일치한다. 최초 전환 복구본의 기존 레벨 팩·요소 팩 바이트도 현재와 동일하다. 추가된 세대 정보가 새 콘텐츠 해시를 기록하며 원본 콘텐츠는 바꾸지 않았다.

## 최종 실행 결과

- `104558-353`: 채택한 JSON을 덮어쓰지 않고 최종 콘텐츠 해시로 데이터 팩을 갱신했다. 27개 문서에서 3개 생성 파일과 주소가 일치한다.
- `104627-067`: 실제 기본 Launcher → 레벨툴 → Addressables Fast Play 팩 시험 → 편집 복귀 통과. 기본 JSON 자동 열기, 미저장 작업 보호, 팩 일치/불일치 표시를 확인했다.
- `104701-535`: 정식 4개 레벨에 같은 시드와 튜토리얼 행동을 적용하여 JSON과 등록된 MemoryPack의 초기 보드·공급·난수·미션 및 행동 후 결과가 같음을 확인했다. 튜토리얼 완료·재시작·정식 학습 기록 불변·취소/잘못된 요청·객체 해제도 통과했다. 상세 `Logs/GameAuthoringStage06/packed-action-results.txt`.
- 위 세 Unity 프로세스 모두 exit 0, 테스트 연결 해제. Player/Addressables 번들 빌드 없음.

## 별도 승인 후 남은 배포 검증

- 네 제품 각각의 플랫폼 전환·도메인 재로드 후 Player/Addressables 실제 빌드와 번들 로딩.
- Windows 게임/레벨툴을 Unity 밖에서 실행하고 저장 위치·제작 JSON 편집·게임 시험 확인.
- Android/iOS 실기 입력·리소스·저장·재시작, iOS Xcode 및 서명 구성 확인.
- Steam SDK/스토어 등록·인증은 구현된 것으로 간주하지 않는다.

이는 현재 승인한 구조·데이터·프로필 검증 이후의 별도 작업이다. 자동 빌드나 다음 단계 실행은 하지 않는다.

## 완료 판정

G1~G10의 현재 승인 범위인 구조·데이터·프로필 검증을 완료했다. `104802-403`에서 미저장 Addressables 설정이 갱신 전 검사에서 보존되고, 실제 등록 직후 실패한 경우 파일·GUID·주소·로드된 설정이 원래대로 돌아오는 최종 회귀도 통과했다. 검사 연결은 해제됐다.

독립 최종 검토의 두 지적은 재현 검사 후 수정했다. 원본 SO 및 메타를 보존하고 채택된 JSON/편집된 후보 재이관을 차단했다. 네 제품의 실제 배포 성공은 주장하지 않는다. 커밋·푸시·Player/번들 빌드·HTML 매뉴얼 변경은 수행하지 않았다.

복사용 커밋 메시지:

```text
feat: JSON 제작 원본 전환과 게임·레벨툴 제품 구성 완료

- 제작 원본 이관·검증·복구 및 채택된 JSON 덮어쓰기 보호
- 동일 JSON 스냅샷의 레벨·요소 MemoryPack 생성과 세대 검증
- 레벨툴 기본 진입 및 편집본·팩 플레이 시험 연결
- Android·iOS·Steam·레벨툴 프로필과 콘텐츠·코드 경계 분리
- 데이터 보존·실패 복구·동일 플레이 검증 및 완료 문서 정리
```
