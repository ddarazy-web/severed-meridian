# EF-13 — 영구 정의 ID·기존 장애물 매핑 검증

상태: 완료. 2026-10-04, ServeredMeridian / Unity6000.3.10f1. 기준 `b353077`, 작업 브랜치 `work` 유지.

## 변경과 계약

새 Elements 기능 아래 런타임2개와 Editor 검사1개, 폴더5개/스크립트3개 meta8개를 추가했다. 기존 생산 소비자는 수정하지 않았다.

```text
Assets/Scripts/Features/Elements/
  Data/ElementId.cs                 불변 정의 ID 값
  Runtime/LegacyElementMap.cs       기존 장애물 enum→정의 ID 연결
  Editor/Tests/ElementIdVerification.cs  메모리 계약·매핑 검증
```

의존 방향은 `LegacyElementMap → ElementId + Levels.ObstacleKind`다. ElementId는 System만 사용한다. 기존 게임/편집/저장/봇/피해/발전기는 새 기능을 호출하지 않는다. 새 asmdef/패키지/DI/인터페이스/자동 등록/카탈로그/제작 에셋은 추가하지 않았다. 기존 네임스페이스를 바꾸지 않고 새 기능에 Elements를 사용했다.

- ElementId는 readonly struct와 readonly string 원문 필드다. 공개 값은 IsValid/Value이고 setter가 없다. `IEquatable<ElementId>`, object Equals, ==/!=와 해시는 StringComparer.Ordinal을 사용한다.
- null/빈 문자열/공백뿐인 입력은 ArgumentException으로 거절한다. 자동 trim/대소문자 변환/유니코드 정규화는 없다. `" obstacle.scrap "`은 공백을 포함한 별개 값으로 보존한다. 전체 ID 문법이나 제작 UI 규칙을 새로 강제하지 않았다.
- default(ElementId)는 IsValid=false, Hash=0이며 default끼리 비교는 true다. 유효 ID와 다르고 Value/ToString은 InvalidOperationException으로 거절한다. 이는 유효 ID를 생성하는 대체 경로가 아니다.
- LegacyElementMap.Get(ObstacleKind)는 캐시한 불변 값6개를 명시적 switch로 반환한다. 미지원 숫자는 ArgumentOutOfRangeException의 ActualValue/Message에 실제 입력을 담아 거절한다. 기본 상자 대체·역매핑은 없다.
- 배치 ObstaclePlacementDefinition.Id는 판 내부 연결용 인스턴스 문자열이다. 정의 ID로 바꾸지 않았다. 현재 Safe/Appliance의 아트 경로가 RecoveryCapsule/MetalRodBox임을 확인하고 계획의 콘텐츠 의미를 적용했다. 기존 구형 enum/표시 문자열은 그대로다.

## 실제 매핑·값·오류

| 기존 enum/숫자 | 실제 ElementId.Value |
| --- | --- |
| Crate=0 | obstacle.crate.wood |
| Scrap=1 | obstacle.scrap |
| Safe=2 | obstacle.recovery-capsule |
| ColorLock=3 | obstacle.color-lock |
| Appliance=4 | obstacle.metal-rod-box |
| Generator=5 | obstacle.generator |

정확한6개 문자열/enum숫자 유지·중복0을 검사했다. `obstacle.scrap`과 별도 문자열 사본은 Equals/형식 Equals/==가 true, !=가 false이며 실제 두 해시 모두 **-1063448500**이었다. 해시 집합에 동등 사본과 상자를 넣은 결과는2개다. 이 수치는 해당 실행 값이며 프로세스/플랫폼 간 영구 해시 보장은 아니다. 저장에는 원문 ID를 쓰는 원칙을 유지한다.

대문자 `OBSTACLE.SCRAP`, 앞뒤 공백 값, `.other`는 원문을 보존하며 원래 ID와 다르다. 조합된/분해된 Unicode `element.café`도 Ordinal 기준으로 서로 다름을 검사했다.

null, `""`, `" "`, tab/CR/LF는 ArgumentException: `요소 정의 ID는 빈 값일 수 없습니다.` / parameter value. default의 Value/ToString은 `초기화되지 않은 요소 정의 ID는 조회할 수 없습니다.`로 거절한다. 미지원 ObstacleKind **-1, 6, 999, -2147483648, 2147483647**은 ArgumentOutOfRangeException, parameter kind, 각 입력 숫자의 ActualValue와 메시지를 확인했다. 실제 전체 오류 문자열은 JSONL에 있다.

## 실제 메모리 입력과 소비자 보존

기존 GeneratorVerification.Make(Crate,3)를 재사용했다. 원본 메모리 이름 original-label과 복제본 different-label을 다르게 하고, 복제본 본체 Id 전체를 renamed-instance-*로 변경한 뒤 장애물 배열0→1을 이동했다. 이름·첫 Id·첫 Kind의 실제 차이를 먼저 검사했다. 두 입력 모두 종류별 정렬 매핑 결과는 `Crate=obstacle.crate.wood;Generator=obstacle.generator`다.

seed12345로 구성한 runtime의 현재 값/난수와 레벨 JSON 및 Unity 전역 난수는 매핑 조회 전후 동일하다. 기존 발전기 활성 연결은 각각1개이고 배치 인스턴스 Id가 정의 ID와 다르다. 기존 LevelPackCodec의 FormatVersion1/LevelsPerPack50 및 LevelDefinition 스키마4는 보존했다. 각 입력의 메모리 Encode 전후 바이트도 동일하다. 원본 메모리 팩은 **1921bytes**, 이름/인스턴스 이력을 바꾼 입력은 **1865bytes**다. 이 두 서로 다른 입력의 팩이 같다고 주장하지 않는다. 디스크 팩 재생성·저장 포맷 전환은 하지 않았다.

보호 파일 **1,795개 변경0**. 기존 enum/직렬화 필드 순서/인스턴스 연결/원본/GUID/아틀라스/Addressables/프리팹/씬/패키지/기존 소비자와 EF-05 예외 원복 보존을 해시와 최종 git diff로 확인했다. 새 meta GUID는 기존 및 새 meta와 중복 없다. 시작 작업 트리는 깨끗했다. 커밋·푸시·빌드·재패킹·이미지·사용자 Unity 종료·씬 저장은 하지 않았다.

## 검사 실행과 증거

구현 전에 생산 타입이 없는 상태에서 reflection 기반 전체 계약 검사를 먼저 만들었다. 별도 Editor의 RED는 종료1, `ElementId 계약 존재` 실패였다. 컴파일 오류가 아니라 실제 타입 부재를 확인한 기대 실패다. 그 뒤 두 런타임 파일을 구현해 통과했다. 조회/오류 기록을 보완한 최종 실행은 다음과 같다.

| 안전한 별도 Editor 진입점 | PASS | FAIL | 종료 |
| --- | ---: | ---: | ---: |
| Elements.Editor.ElementIdVerification.Run | 49 | 0 | 0 |
| Levels.Editor.BotObservationVerification.Run | 32 | 0 | 0 |
| Levels.Editor.FixedObstacleVerification.Data | 142 | 0 | 0 |
| Levels.Editor.GeneratorVerification.Data | 246 | 0 | 0 |
| 합계 | 469 | 0 | 각0 |

Data/Run의 호출부를 읽어 원본 저장/빌드/UI 실행이 없는 메모리 경로만 사용했다. 초기 단순 GREEN 실행도 종료0이었다. 마지막 수정 후 네 검사를 모두 실행했고 최종 필수 실패는 없다.

[Stage13 증거 폴더](../../../../Logs/ElementFramework/Stage13)의 `red.log/red-execution.json/red-results.txt`, `green.log/green-execution.json`, `id.log/id-execution.json/id-results.txt/id-values.jsonl`, `bot.*`, `obstacle.*`, `generator.*`, 각 results.txt, `protected-before.json/preservation-audit.json`, `source-evidence.json`, `progress.md`, `completion-audit.json`을 확인한다. 새 검사 실제 기록은 **24건**이며 ID/매핑/오류/두 입력 JSON·runtime·메모리 팩 크기를 담는다. 로그는 Git 제외 대상이다.

## 한계·남은 단계

순수 ID/매핑은 구현했지만 기존 소비자가 아직 사용하지 않는다. 수백 종류의 행동·표현·배포 전환, 제작 ScriptableObject/카탈로그·MemoryPack ID 직렬화, ID 변경 마이그레이션은 아직 없다. default를 거절하는 다음 조회 경계도 별도로 검사해야 한다. 전체 플레이·승률·렌더링/실기기/IL2CPP/성능 검증은 하지 않았다. 이번 변경은 엔진 API/플랫폼 설정을 추가하지 않으며 실제 플랫폼 동작 보장을 확대하지 않는다.

EF-09 원본1254px3개/Editor·월드 바닥 차이와 EF-11 Draw/Reset 초기화 책임은 그대로 남긴다. 승인된 드론 상승·호버·돌진 변경도 이번 단계에 포함하지 않았다.

다음은 **EF-14 읽기 전용 정의 메타데이터·카탈로그 조회**다. 현재 ID로 메모리 정의를 색인하고 누락/중복/무효 값과500개 조회를 검증한다. 게임/저장 전환과 제작 에셋은 분리한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-14-catalog-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-14-catalog-goal.md) · [전체 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-14-command.md). 다음 단계 구현은 시작하지 않았다.
