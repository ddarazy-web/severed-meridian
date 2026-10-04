# EF-09 — 보드 표현 경로·프레임·아틀라스 대응 기준 목표

상태: **완료**, 2026-10-04. 추가1615 PASS / 0 FAIL, 기존 파워 검사 통과. 실제 관찰233건.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-09-artwork-baseline-plan.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

- [x] Editor 편집/플레이/재생과 런타임 보드의 실제 매핑 호출부를 확인했다.
- [x] 현재 일반/파워/회수/장애물/층/장치 매핑의 상태별 경로·원본 그림 대응·차이를 기록했다.
- [x] 제거/빈칸/비활성/곰팡이 은폐의 null 계약과 데이터 무변경을 확인했다.
- [x] 현재 애니메이션 프레임/스프라이트 rect·피벗·개별/전체 크기와 실제 아틀라스 주소/등록/패커블 관계를 확인했다.
- [x] 실제 입력·경로/프레임/주소·GUID/메타데이터·일치/차이를 저장하고 안전한 Editor 검사 종료 코드·필수 FAIL/예외0을 확인했다.
- [x] 생산/이미지/아틀라스/Addressables/저장/GUID/기존 작업을 보존했다. 빌드/재패킹·이미지 수정·임의 커밋·씬 저장 없음.
- [x] 완료 보고와 EF-10 계획·목표·복사용 실행문을 제공했다. 다음 구현은 하지 않았다.

필수 실패/미실행은 미완료다. 실제 화면·기기·번들 로드와 리소스 준비/풀/봇 구조 전환을 이 매핑 기준의 완료로 표현하지 않는다.

실제 결과: [EF-09 완료 보고](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-09-progress.md). 유효 Editor 경로는 로드 금지에 따라 소스/호출부 대조, 런타임 경로·Sprite/atlas 메타데이터와 Editor null은 직접 검사. 기존 원본1254px3개/바닥 차이는 보존하고 보고했다. EF-10은 리소스 준비/소유권만 상세화하며 풀은 분리한다.
