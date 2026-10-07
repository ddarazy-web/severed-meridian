# 레벨 튜토리얼 4단계 — 복사용 목표 실행문

상태: 완료. 2026-10-07. 아래 실행문은 수행한 4단계의 범위 기록이다. [실제 검증](../../../Verification/MoonRabbitJunkyard/Tutorial/stage-04-progress.md).

~~~text
ServeredMeridian 레벨 튜토리얼 4단계 ‘안내·완료 기록·최종 적용’을 진행해.

Docs/Planning/MoonRabbitJunkyard/Tutorial/integration-guideline.md와 stage-04-guidance-plan.md, Docs/Goals/MoonRabbitJunkyard/Tutorial/stage-04-guidance-goal.md, Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-03-progress.md, Docs/Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md를 읽고 목표를 설정하여 이 세션에서 직접 수행해. 현재 HEAD·WIP·1~3단계 완료분·원본/GUID를 보존해.

TutorialProgress와 등록 처리기, PuzzleGameSession의 읽기 전용 상태/승인/실제 결과/표시 완료/종료 보류를 재사용해. 기존 Canvas/uGUI에 강조·말풍선·손가락·다음·무료 체험을 연결하고 UI는 기존 스타일을 사용해. 프리팹은 Assets/Prefabs/UI 아래 기능별로 만들고 Game 프리팹과 구분해. 새로운 진행 엔진·블록별 분기·MVVM 프레임워크를 만들지 마.

장식이 지정 보드 입력을 가로채지 않게 하고 실제 버튼/포인터로 진행시켜. 설명은 다음 요청, 조작은 실제 결과와 관련 표시 완료를 기다려. 무료 체험은 기존 아이템 선택/대상 입력을 사용하고 실제 보유량은 소비하지 마. 최초 건너뛰기와 드론 목적지 사전 표시는 추가하지 마. 가로/세로·Safe Area·카메라 변경, 팝업/정지, 최상위 키 입력, 나가기와 수명 정리를 확인해.

기존 저장 계층을 조사하고 레벨 번호별 완료만 기록해. 마지막 단계와 관련 표시가 끝나야 1회 저장하고 중단/실패/취소는 저장하지 마. 중간 진행은 복원하지 않아. 실행 여부는 보드 준비 전에 결정하여 완료한 자동 실행은 원본을 바꾸지 않고 일반 시드/공급으로 생략해. 에디터 자동/항상 실행/실행 안 함을 연결하고 시험 기록을 실제 완료 기록과 분리해. 재시작/다음 레벨/파기와 늦은 신호의 문맥 격리를 유지해.

대표 적용은 현재 존재하는 Assets/Data/Levels/Level_01.asset의 기본 매칭 튜토리얼 필드에 한정해. 실제 고정 칸·조회·재생 성공으로 좌표/시드/공급을 정하고 보드·미션·이동·흐름·색상·GUID는 유지해. 정적 왕복용 Populate fixture를 출시 안내로 복사하지 마. 이번 4단계에는 해당 50레벨 구간 MemoryPack만 기존 갱신 도구로 갱신하는 것을 허용해. 구간의 다른 기존 레벨을 누락하지 말고 주소·팩 호환·ECPK·제작 SO 배포 제외를 보존해. 없는 2~5/9 출시 레벨은 임의 생성하거나 소개 순서를 바꾸지 말고 파워/조합 안내는 소유한 임시 fixture로 검증해.

TutorialGuidanceVerification.Run으로 실제 UI/기록/시험 3모드/Asset·팩3/방향/수명을 검사하고 기존 튜토리얼·일반 플레이·드론·팝업/결과 회귀를 확인해. 논리/모의 검사와 실제 화면/저장 검사의 범위를 구분해. 테스트 연결 해제 후 게임 소스만 컴파일하고 과거 검사 출력은 보관 후 복원해.

빌드·번들·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장은 하지 마. 위에서 지정한 1레벨 튜토리얼과 해당 팩 외 출시 데이터를 변경하지 마. 새 이미지·고물탑·상세 매뉴얼·전체 재고/계정 시스템은 이 범위가 아니야.

완료 시 변경·실제 검증 근거·미완료/제약·확장 방법을 보고하고 Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-04-progress.md에 기록해. 다음 작업은 완료 결과를 기준으로 제안하고 자동 시작하지 마.
~~~
