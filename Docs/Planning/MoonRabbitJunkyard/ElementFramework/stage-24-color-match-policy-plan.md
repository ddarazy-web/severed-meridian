# EF-24 — 색 자물쇠 색 일치 조회 정책 연결 계획

상태: 완료. 별도 Editor17종 종료0/필수FAIL0, 실제8447행 전후 동일. EF-23의 기존6종 원인 허용 연결 다음 작은 작업.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-23 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-23-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-goal.md).

## 목적과 계약

색 자물쇠는 Power/Hammer에서는 색을 검사하지 않고, 그 외 원인은 출발색과 본체 지정색을 비교한다. 미정의-1/4도 기존 색 비교 대상이다. 내부 null은 불일치이며 외부 Evaluate는 null 출발색을 출발칸 색으로 대체한다. 이 순서를 보존하며 색 비교 한 조건만 정의 조회로 연결한다.

Elements/Data에 불변 ElementColorMatchPolicy를 둔다. 읽기 전용 RequiresMatchingColor(bool)와 Allows(RabbitColor? sourceColor, RabbitColor targetColor)를 제공한다. true는 기존 nullable 색 비교와 같은 일치만 허용하고 false는 색 제한을 하지 않는다. 상태/문맥/난수/Unity 참조는 보유하지 않는다. 범용 조건 언어나 새로운 피해 실행기는 만들지 않는다.

ElementDefinition에 읽기 전용 ColorMatchPolicy와 RequireColorMatchPolicy를 추가한다. 기존2/3/4/5인자 생성 계약을 유지하고 끝에 새 프로필을 받는6인자 생성 경로만 추가한다. 누락은 ID 포함 오류이며 기본값을 대체하지 않는다. LegacyElementDefinitions의 obstacle.color-lock에 true 프로필을 한 번 등록한다. 기존 DamageSourcePolicy와 배치/충전 프로필은 그대로다.

ObstacleDamageRules.Query의 기존 색 자물쇠 비교 위치에서 Get→Require→Allows만 연결한다. 원인 허용과 발전기 위임 뒤, 내구도/턴 집계 전이라는 순서 및 기존 메시지를 유지한다. 다른 종류에 색 프로필을 필수로 요구하지 않는다.

## 작은 작업과 검증

1. work/현재 미커밋 변경과 원본/GUID/과거 증거를 보호한다. 색/null·원인4/-1/4·내구도1~3/0·null/새/같은턴/다음턴·외부 출발색 대체·벽/거리/보호/비활성/삭제의 실제 전환 전 입력/응답을 저장한다. 실제 매칭/자석 인접/Power/Hammer·미션/예약/연결 철거 호출부를 조사한다.
2. 검사부터 작성해 같은 저장 입력/시드로 전후 비교하고 새 계약 누락의 RED를 확인한다. 위 불변 프로필/기존 생성 계약과 한 번 등록·필수 조회를 최소 연결한다. true/false 메모리 프로필도 같은 카탈로그 조회로 확인한다.
3. Query 상태/비공개 문맥/규칙/전역 난수 무변경, Response/Amount/Message와 실제 피해/턴당1회/제거/미션/예약/철거, MemoryPack 전체 바이트/ID/버전1/50구간을 비교한다. 다른5종과 발전기 내부 자석 허용/외부 거절 및 Durability0을 보존한다.
4. 새 검사와 EF-23 GeneratorReactionPolicyVerification.Run 및 EF-23의 기존15종을 각각 별도 안전한 Editor에서 실행한다. 정확한 메서드/입력/응답/실제 종료0/필수FAIL0을 남기고 과거 결과/값을 백업·복원해 바이트 동일을 확인한다.
5. stage-24-progress.md에 결과/차이/미검증/남은 문제를 저장하고 현재 계획/목표를 갱신한다. 완료 후 실제 결과에 맞춘 다음 한 단계 계획/목표/전체 복사용 명령문만 작성·제시한다. 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 에이전트는 별도 요청 시에만 사용한다.

## 제외

Apply/Remove/충전/미션/예약/턴·칸 집계, 발전기 Query/Apply/연결/철거, 다른 종류 정책, 공급/낙하/드론/봇/UI/MVVM/표현/풀, 제작/배포/저장 포맷/변환은 변경하지 않는다. EF-05/09/11·enum 숫자/원본/meta/기존 작업을 보존한다. 빌드/재패킹/이미지/팩 재생성/임의 커밋/사용자 Editor 종료/씬 저장 금지. 실패를 숨기거나 목표를 축소하지 않는다.
