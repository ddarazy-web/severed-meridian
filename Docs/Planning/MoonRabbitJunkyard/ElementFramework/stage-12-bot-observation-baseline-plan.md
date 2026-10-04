# EF-12 — 봇 공개 관찰·숨은 정보 차단 기준 확보 계획

상태: 완료. 추가102 PASS+기존32 PASS, 각각 종료0. [검증 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-12-progress.md).

연결: [가이드라인](integration-guideline.md) · [EF-11 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-11-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-12-bot-observation-baseline-goal.md).

실행 담당: superpowers:executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

**목표:** 현재 공개 관찰 값과 정보 차단 경계만 확보한다. BotObservationBuilder.Capture(BoardActionExecutor)의 결과를 실제 비교하며 전략 개선이나 전체 승률 검사는 포함하지 않는다.

**접근:** 기존 BotObservationVerification의 메모리 fixture/숨은 정보 쌍/원본 보존 검사를 우선 재사용한다. 필요한 Editor 검사만 Assets/Scripts/Features/AutoPlay/Editor/Tests 아래 보완하고 생산 코드는 변경하지 않는다. 결과는 Logs/ElementFramework/Stage12 및 단계 검증 기록에 저장한다. 실제 구조와 기존 검사를 읽은 뒤 필요한 검사 파일명을 확정한다.

## 작업 단위와 검증

1. 기존 변경·원본 해시를 보호하고 BotObservation.cs, BotObservationBuilder.cs, BasicBotStrategy의 관찰 소비 경계와 기존 BotObservationVerification/관련 검사를 읽는다. 실행 중 파일 저장/UI/빌드가 있는 검사는 분리한다. 현재 DTO의 공개 값·리스트·객체 참조·후보 생성 경로와 누락 위험을 대응표로 기록한다.
2. 안전한 메모리 fixture의 정상/파워/장애물/층/장치·미션 관찰과 후보 행동을 실제 기록한다. 수치/좌표/색/내구도/충전/2×2 점유를 확인하고 DTO 스냅샷이 실행 상태 변경에 영향을 받지 않는지 검증한다.
3. 곰팡이 아래 내용물, 미공개 공급/예약/난수 등 숨은 값만 다른 두 상태를 구성한다. 동일 공개 관찰과 동일 공개 후보를 비교하고, Capture/후보 조회 전후 원본 상태/난수 draw count를 확인한다. 정의/내부 ID/실행 상태/숨은 참조를 공개 DTO로 넘기는지 구조 검사와 실제 변경 관찰을 구분한다.
4. 기존 안전한 검사를 실행하고 누락된 Editor 검사/meta와 실제 입력·관찰·후보·난수 전후값만 보완한다. 필수 실패를 기존 문제와 검사 오류로 구분하고 생산 수정으로 확대하지 않는다.
5. 종료0/필수 FAIL0·보호 파일/GUID 보존·문서 링크와 요구사항 대응을 검토한다. stage-12-progress.md를 저장하고 결과에 맞춘 다음 작은 단계 계획·목표·전체 복사용 명령문만 작성한다. 다음 구현은 하지 않는다.

## 검토 초점·제약

Unknown을 Empty/Normal로 오인, 제거된 장애물/2×2 점유 유령 후보, 실행 상태 참조를 통한 사후 변경, 숨은 공급/난수/예약에 따른 공개 후보 차이, 조회의 원본·난수 변경을 각각 검사한다. 내부 ID가 공개 정의용 ID인지 숨은 인스턴스 ID인지 실제 계약을 읽어 구분하며 임의 정책을 만들지 않는다.

생산/원본/저장/GUID/기존 변경 보존. 빌드/재패킹·이미지 생성/수정·임의 커밋·사용자 Unity 종료·씬 저장 금지. 새 패키지/관찰 인터페이스/카탈로그/봇 전략/풀/드론/UI 구현 제외. 전체 봇 플레이·승률·성능은 분리한다. 필수 실패나 실제 비교 미실행은 미완료로 기록한다.
