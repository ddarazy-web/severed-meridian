# EF-13 — 영구 정의 ID·기존 장애물 매핑 계획

상태: 완료. 새49+봇32+장애물142+발전기246 PASS/0 FAIL, 각각 종료0. [검증 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-13-progress.md). 큰 구간 B의 첫 작은 구현 단계.

연결: [통합 가이드라인](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-12 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-12-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-13-element-id-goal.md).

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 목적·범위

EF-01~12에서 보존 기준을 확보했다. 한 번에 카탈로그·규칙·저장을 전환하지 않고, 기존 장애물6종과 영구 콘텐츠 ID를 연결하는 최소 경계를 먼저 만든다. 아직 어떤 기존 소비자도 새 경계를 사용하지 않으므로 게임 동작과 저장 결과는 동일해야 한다.

추가 후보는 `Assets/Scripts/Features/Elements/Data/ElementId.cs`, `Elements/Runtime/LegacyElementMap.cs`, `Elements/Editor/Tests/ElementIdVerification.cs`와 필요한 meta다. 실제 구현 시 기존 네임스페이스/asmdef를 확인하고 파일 책임을 조정할 수 있다. 새 패키지·asmdef·DI·자동 등록·전체 종류 목록 인터페이스는 만들지 않는다.

## 최소 계약

ElementId는 Unity Object/표시명/GUID/배치 Id와 독립된 불변 값이다. 문자열 값을 그대로 보존하고 Ordinal 동등성/해시를 사용한다. null/빈 문자열/공백뿐인 값은 명시적으로 거절한다. trim/대소문자 변환으로 다른 ID를 몰래 합치지 않는다. 값 형식의 default를 택하면 무효 상태임을 명시하고 유효 ID로 취급하지 않는다. MemoryPack/Unity 직렬화 연결은 이번 단계에 넣지 않는다.

LegacyElementMap은 기존 ObstacleKind6종만 영구 ID로 연결한다. enum 숫자와 배치의 인스턴스 Id는 보존한다. 미지원 enum(-1/999 등)은 입력 값을 포함한 오류로 거절하며 기본 상자 대체를 하지 않는다. 역변환·모든 카테고리 통합·동적 플러그인 등록은 요구하지 않는다.

확인·구현한 매핑은 다음과 같다. 기존 아트의 RecoveryCapsule/MetalRodBox 의미와 일치한다. 아직 저장/배포 소비자를 전환하지 않았으며 사용된 ID는 다른 종류에 재사용하지 않는다.

| 기존 종류 | 영구 정의 ID |
| --- | --- |
| Crate | obstacle.crate.wood |
| Scrap | obstacle.scrap |
| Safe | obstacle.recovery-capsule |
| ColorLock | obstacle.color-lock |
| Appliance | obstacle.metal-rod-box |
| Generator | obstacle.generator |

Safe/Appliance라는 구형 enum 이름을 현재 회수캡슐/금속기둥 상자의 콘텐츠 의미와 구분한다. 기존 이름이나 UI 문자열은 이 단계에서 정리하지 않는다.

## 작은 작업 단위와 검증

1. 실제 enum/배치 Id/발전기 연결/저장 호출부와 기존 변경을 확인하고 보호 해시를 확보한다. ID 의미·허용 값·default 처리·오류 형식을 검사에서 고정한다.
2. ElementId 값과 장애물6종의 명시적 매핑만 구현한다. 기존 게임/편집/관찰/저장 경로에는 연결하지 않는다.
3. 안전한 메모리 Editor 검사로 ID 값 보존·동등성·해시·잘못된 값 거절, 6종의 정확한 문자열·중복0·미지원 enum 거절을 실제 확인한다. 표시명/배치 Id/배열 순서가 바뀌어도 같은 종류의 매핑은 같아야 한다.
4. 별도 Editor에서 새 검사와 기존 BotObservationVerification을 실행한다. 관련 기존 메모리 장애물 검사는 Run의 저장/빌드 부작용을 먼저 확인하고 안전한 경로만 재사용한다. 보호 파일·enum 숫자·원본/GUID·기존 저장 및 생산 소비자 diff를 확인한다.
5. 실제 입력/출력/오류/종료0·필수 FAIL0·원본 보존·미검증을 stage-13-progress.md에 기록한다. 결과에 따라 다음 한 단계 계획·목표·전체 복사용 명령문만 작성하고 구현하지 않는다.

## 제외·중단 기준

정의 ScriptableObject/카탈로그/행동 등록/피해·미션/봇 DTO 전환/드론 수정/UI/MVVM/표현·풀/원본 변환·팩 재생성은 제외한다. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지. 기존 예외 원복과 EF-09/11 차이는 보존한다. 필수 실패를 기록하고 실패를 숨기는 production 수정이나 목표 축소를 하지 않는다. 저장 ID 계약 전체를 확정하는 단계는 이후 별도로 다룬다.

실행 결정: readonly struct/IsValid/Value, default.Value·ToString 거절 및 안전한 default 동등성/해시0. Get(ObstacleKind)만 추가했고 기존 소비자는 연결하지 않았다. Reflection 검사로 타입 부재 RED를 먼저 실행했다.
