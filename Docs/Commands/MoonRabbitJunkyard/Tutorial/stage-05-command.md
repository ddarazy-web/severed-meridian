# 튜토리얼 후속 5단계 — 복사용 목표 실행문

상태: 완료. 수행 당시 실행문을 보존한다. 새 실행에는 stage-06-command.md를 사용한다. 완료 근거와 제약은 stage-05-progress.md를 따른다.

~~~text
ServeredMeridian 튜토리얼 후속 5단계 ‘2레벨 청소로켓 콘텐츠’를 진행해.

Docs/Planning/MoonRabbitJunkyard/Tutorial/stage-05-rocket-content-plan.md와 integration-guideline.md, Docs/Goals/MoonRabbitJunkyard/Tutorial/stage-05-rocket-content-goal.md, Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-04-progress.md 및 연결된 기획을 읽고 목표를 설정하여 이 세션에서 직접 수행해. 현재 HEAD/WIP·완료된 1~4단계·기존 1레벨/GUID를 보존해.

기존 제작 도구·요소 카탈로그·등록 처리기·공통 엔진·실제 실행기·안내 UI·완료 기록을 재사용해. 전체 9×9의 실제 Level_02.asset을 제작해. 설명→지정 가로 4매칭으로 세로 로켓 생성→생성 설명→새 로켓의 인접 교환 발동→종료 설명으로 진행하고 같은 보드에서 자유 플레이로 이어가게 해. 검사용 좁은 보드를 출시 레벨로 복사하거나 새 진행 엔진/블록별 분기를 만들지 마.

먼저 메모리 후보에서 실제 조회·실행·재생으로 생성 칸/방향·후속 교환 대상·시드·고정 공급을 확정해. 낙하/연쇄 후 후속 대상 유지, Generated/Activated power.rocket 결과, 두 교환의 이동 차감, 실제 미션 집계와 관련 표시 완료를 검증해. 임의 입력과 제자리 발동은 거절하고, 공급 부족을 무작위 대체로 숨기지 마. 실제 다음 버튼과 보드 포인터, 두 화면 비율의 강조/손가락/말풍선과 일반 플레이 복귀를 확인해.

검증한 새 2레벨과 해당 1~50 구간 MemoryPack 갱신을 허용해. 기존 구간의 모든 레벨을 포함하는 기존 CreatePackBytes 경로를 사용하고 다른 팩/ECPK/주소/제작 SO 배포 제외를 보존해. 기존 2레벨이 이미 있으면 사용자 변경과 GUID를 덮어쓰지 말고 현재 데이터를 먼저 평가해. 다른 출시 레벨은 생성하지 마.

실제 에디터에서 2레벨을 선택해 Asset/MemoryPack 게임 플레이로 진입시키고 동등한 안내·최종 상태를 확인해. 자동/항상 실행/실행 안 함, 중단 후 첫 단계 재현, 완료 1회와 자동 생략, 레벨 1과 기록 분리 및 1→2 전환의 늦은 신호 격리를 검증해. 시험 기록은 실제 완료 기록과 분리해.

TutorialRocketLevelVerification의 Preview→Apply→Run과 관련 기존 안내/데이터/일반 게임/로켓/팝업 회귀를 수행해. 실제 UI·논리 재생·기록 검사의 범위를 구분하고 과거 출력은 백업·복원해. 테스트 연결을 해제한 뒤 게임 소스만 컴파일하고 승인 범위·원본/GUID·HEAD/WIP 보존을 확인해.

빌드·번들·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장은 하지 마. 새 이미지·다른 소개 레벨·전체 밸런스/해금/인벤토리/계정 시스템·상세 매뉴얼·고물탑은 포함하지 마. 기존 기능 결함이 실제 재현될 때만 직접 관련된 최소 수정과 회귀 근거를 기록해.

완료 후 변경·실제 검증 결과·제약을 보고하고 Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-05-progress.md에 저장해. 완료 결과를 기준으로 다음 3레벨 달 폭탄 소개의 계획서·목표 문서·복사용 실행 명령문을 작성해. 다음 구현은 자동 시작하지 마.
~~~

