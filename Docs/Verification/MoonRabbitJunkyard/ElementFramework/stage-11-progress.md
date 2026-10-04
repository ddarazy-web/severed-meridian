# EF-11 — 월드 보드·효과 오브젝트 풀 기준 검증

상태: 완료. 2026-10-04, ServeredMeridian / Unity6000.3.10f1.

## 변경·검사 결과

Editor 전용 PoolBaselineVerification.cs, PoolBaselineVerification.Play.cs 및 meta2개만 추가했다. 생산 코드/기존 예외 원복 수정/원본은 보존했다. 별도 batchmode/nographics Editor의 실제 월드 보드 프리팹 복제 검사 **34 PASS/0 FAIL**, 별도 저장하지 않는 빈 씬 Play Mode 검사 **48 PASS/0 FAIL**, 각각 종료0. 총 **82 PASS**, 보드 관찰16건+Play 관찰28건=**44건**, 씬 해제 별도 CSV2건. 로그/실행값/관찰은 Logs/ElementFramework/Stage11의 board.*, board-results.txt, board-execution.json, board-observations.jsonl, play.*, play-results.txt, play-execution.json, play-observations.jsonl, scene-lifetime.csv에 있다.

## 실제 수량·재사용

| 검사 입력/시점 | 셀 | 본체 | 장식 | 공급/마스크 | 효과 슬롯 |
| --- | --- | --- | --- | --- | --- |
| 일반 고정 색 판 최초 | 81 | 0 | 0 | 0/0 | 0 |
| 활성9칸, 나머지 비활성 | 풀81/활성9 | 0 | 0 | 0/0 | 0 |
| 활성81 복귀 | 동일81 | 0 | 0 | 0/0 | 0 |
| 발전기+상자/전선 입력 | 81 | 2 | 3 | 0/0 | 0 |
| 직접 효과 슬롯0/1 사용 | 81 | 0 | 0 | 0/0 | 2 |
| 로켓 재생 후 | 81 | 0 | 0 | 0/0 | 13 |
| 폭탄/드론 재생 후 | 81 | 0 | 0 | 0/0 | 13 |
| 자석 재생 후 | 81 | 0 | 0 | 0/0 | 16 |
| 실제 공급 기록 표시 | 81 | 0 | 0 | 1/1 | 16 |

9×9 배열 크기를 바꾸지 않고 활성 칸9→81, 표시량 작은→큰→작은으로 검사했다. 일반 판10회 반복과 본체/장식5회 왕복의 instanceID는 동일하다. 작은 판에서 남는 본체/장식은 비활성화되고 용량은 줄이지 않는다. 효과는 사용량에 맞게2→13→16으로 늘며, 같은 파워의 두 번째 사용에서는 모든 슬롯 instanceID/용량이 그대로다. 전체 종류를 사전 생성하는 공용 풀은 아니다. 객체 인스턴스·활성 여부와 아틀라스 주소/핸들은 별개다.

입력 seed12345, 실제 LevelDefinition JSON과 공급용 runtime(8,4) Empty/Color=null 변경 절차를 관찰에 기록했다. 보드 관찰은 셀/본체/장식/공급/마스크/효과 root instanceID, 실제 renderer 활성/enabled·sprite·색·local/world 위치·크기·회전·order/group order 및 마스크/효과 활성 배열을 기록한다.

## 상태 초기화·종료 경계

- 선택 Preview는 order50으로 올리고 ClearPreview는 원래 위치/order로 복원한다. Capture→Swap→Restore는 기존 색/크기/위치/order를 실제 복원한다.
- 직접 Draw에만 의존하면 색/order/회전은 유지된다. 고의로 색blue/order701/회전45°를 넣고 Draw한 실제 결과가 그대로였다. 크기·위치는 다시 설정한다. 이는 현재 초기화 책임이 Draw 하나로 통합되지 않았다는 기준이며 새 연출은 Snapshot/Reset 복원 경로를 지켜야 한다. 이번에 생산 수정하지 않았다. dirty-before/after 관찰을 저장했다.
- 드론4컷의 실제 시트 mask/사분면 위치를 확인했다. 시트→일반 Match 효과는 흰색/enabled/위치0/단일 Sprite·maskInteraction.None/마스크 비활성으로 바뀌며 같은 슬롯 객체를 재사용한다. group order45→40 등 실제 값을 관찰했다.
- 네 파워의 실제 BoardActionExecutor.Activate 기록으로 PuzzleEffectTimeline/PowerPlayback.Prepare→Begin→Tick→Reset을 실행했다. 정상 종료와 중간 Reset 취소 모두 모든 효과 root를 비활성화했다. 두 번째 동일 파워 사용은 추가 생성0이다. 비행 규칙이나 연출은 변경하지 않았다.
- 실제 SettlementResolution.Resolve의 Supply 기록을 SupplyImage와 SettlementPlayback에 사용했다. SupplyImage는 재사용 때 색white/order10/크기/위치/sprite/마스크를 설정한다. Snapshot.Image.Restore/Hide는 공급 renderer와 짝 마스크를 숨기고 maskInteraction을 해제한다.
- 정상 정착 Tick은 완료 신호를 반환하지만 공급 객체를 즉시 반환하지 않는다. 실제 세션은 그 신호 뒤 ResetPresentation→settlement.Reset→Snapshot.Restore→Draw를 호출한다. 검사도 그 순서에 맞춰 Reset까지 실행해 정상/취소의 공급·마스크 비활성을 확인했다. Tick 단독을 반환 완료로 오해하지 않는다.
- Play Mode Destroy(board) 후 모든 자식 renderer와 공용 mask Sprite가 해제된다. 별도 메모리 씬에 board를 넣고 Unload해도 해제된다. CSV의 최초 관찰은 boardDestroyed=true/remainingRenderers=0/maskSpriteDestroyed=false, LastPostLateUpdate와 다음 Update를 기다린 두 번째 관찰은 모두 해제였다. Unload 완료와 OnDestroy에서 호출한 Destroy(Sprite)의 완료 시점을 같은 것으로 가정하지 않는다. 최대5회 관찰 경계를 두고 실제2건만 기록했다.
- 씬 풀 객체가 해제돼도 외부 PuzzleArtwork가 소유하는 atlas는 살아 있고 Get(Floor)이 성공했다. 검사 소유자가 finally에서 Dispose한다. 원본 Texture나 아틀라스를 풀 객체 파괴와 함께 임의 해제하지 않았다. 실제 공유 핸들 소유권 기준은 EF-10을 유지한다.

## 호출부 검토·한계

source-evidence.json은 World/Session 생산 소스20개의 내용/해시를 저장했다. 재시작/레벨 전환은 동일 board.Draw(candidate) 후 ResetPresentation과 candidate/previous artwork 소유권 교체를 수행한다. ResetPresentation은 effect 준비 취소·power.Reset·swap/removal/settlement.Reset·snapshot.Restore·preview.Clear를 호출한다. OnDestroy는 같은 Reset과 artwork.Dispose/lifetime 취소를 수행한다. 실제 보드 재사용·네 파워 종료/취소·공급 종료/취소·Destroy·메모리 씬 Unload는 실행했고, **전체 PuzzleGameSession 재시작/레벨 전환 UI는 소스 호출부만 검토했다.** 전체 UI/팩/대체 provider를 사용하는 기존 PoolReuseChecks를 실행했다고 표현하지 않는다. 기존 Make/Place/Generator fixture와 관찰 항목·반환 순서를 재사용해 독립 검사로 분리했다.

렌더링 화면·실기기·GC/메모리 성능·프레임 예산·Addressables 콘텐츠 빌드·전체 봇은 미검증이다. 반복 검사에서 측정한 객체 수량/참조 안정성을 실기기 메모리 누수 없음으로 확장하지 않는다. Draw의 색/order/회전 유지와 Reset에 의존하는 반환은 이후 구조 전환 때 보존/개선 여부를 판단할 명시적 경계다. 예외 원복 외 추가 생산 수정 없음.

## 중간 실패·보존·검토

Play 첫 실행은 새 파일 namespace 누락 컴파일 오류였다. 공급 fixture는 초기 배치 삭제만으로 빈 칸이 되지 않아 공급구와 runtime 빈 칸을 구성했다. 다음 실패는 Tick 직후 반환을 기대한 검사 순서 오류로 실제 Reset 호출까지 반영했다. 씬 해제 시점 검사는 한 번 Yield로 mask Sprite 해제를 보장할 수 없어 프레임별 관찰을 추가했다. 중간 로그/실행값은 attempt/pre-* 파일로 보존했고 최종 결과만 위 통과 수치로 집계했다.

preservation-results.json: 시작 보호 **1789파일 SHA256 변경0**. 생산·원본/meta GUID·프리팹/씬·아틀라스/Addressables·저장·기존 작업 보존. 빌드/재패킹·이미지 생성/수정·임의 커밋·사용자 Unity 종료·씬 저장 없음. 새 풀/사전 생성 정책/드론/UI/봇 구현 없음. EF-09 아트 차이는 그대로다. 계획의 별도 요청 시 병렬 원칙에 따라 에이전트는 사용하지 않았다. 범위 내 필수 실제 검사 누락/미해결 실패는 없다. 검토는 요구사항→실제 결과/소스 대응, 원본 해시와 로그 오류/문서 링크를 확인했다.

운영 판단: 전체 UI 검사는 원본 보존 범위의 독립 풀 검사로 분리했다. 비용/한계는 UI 버튼부터 전환 완료까지의 통합 보증을 제공하지 못한다는 점이며 실제 객체 수명과 호출부 검토를 명확히 구분했다. 생산 초기화 정책을 바꾸지 않고 현재 책임 경계를 기준으로 삼았다. 추후 복원 경로를 생략하면 표시 잔류가 생길 수 있다.

## 다음 단계

[EF-12 봇 관찰 기준 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-12-bot-observation-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-12-bot-observation-baseline-goal.md) · [복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-12-command.md).

공개 관찰 값·숨은 정보 차단·원본/난수 보존만 다룬다. 전략 개선·전체 승률·카탈로그 구현과 분리한다. EF-12 문서만 작성했고 구현 미착수다.
