# 튜토리얼 후속8단계 — 복사용 목표 실행문

상태: 재검토 대기. [조합형 튜토리얼 전환 기획](../../../Contents/MoonRabbitJunkyard/15_조합형튜토리얼.md)에 맞춰 연결된 계획·목표와 실행문을 조정하기 전에는 아래 이전 실행문을 바로 사용하지 않는다. 신규5레벨 구현은 시작하지 않았다.

~~~text
ServeredMeridian 튜토리얼 후속8단계 ‘5레벨 무지개 자석 콘텐츠’를 진행해.

Docs/Planning/MoonRabbitJunkyard/Tutorial/stage-08-magnet-content-plan.md와 integration-guideline.md, Docs/Goals/MoonRabbitJunkyard/Tutorial/stage-08-magnet-content-goal.md, Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-07-progress.md 및 연결된 기획을 모두 읽고 이 세션에서 직접 수행해. 현재 HEAD/WIP·기존1~4레벨/GUID·모든 기존 구간 레벨을 보존해.

기존 공통 엔진·제작 도구·카탈로그·등록 처리기·실제 게임 실행/표시/기록을 재사용해 전체9×9 schema5 실제 Level_05.asset을 작성해. 설명→지정 직선5매칭으로 자석 하나 생성→생성 설명→일반 색 블록과 인접 교환 발동→종료 설명의5단계를 같은 보드 자유 플레이로 이어가게 해. Generated/Activated power.magnet을 사용하고 새 엔진·자석별 튜토리얼 분기·파워 조합 안내는 만들지 마.

메모리 후보의 실제 조회/실행/재생으로 정확한 직선5매칭·생성 결정/보드 자석1개·생성/낙하 후 좌표/상대·시드·고정 공급을 확정해. schema5 ElementSupply를 유지하고 실제 소비량/필요 여유를 확인해. 공급 누락/부족/무작위 대체·비지정 교환·제자리 발동을 거절해. 현재 교환 상대 색 선택과 직접 제거/미션 기록을 추가 연쇄와 구분해. 두 교환 각 이동1, 표시 완료 대기·일반 공급 복귀·남은 미션을 검증해.

검증된 신규5/메타와 동일1~50 구간 팩 갱신만 허용해. CreatePackBytes에 모든 기존 구간 레벨을 포함하고 기존5 발견 시 먼저 평가하여 사용자 데이터/GUID를 덮어쓰지 마. 다른 팩/ECPK/주소/제작 SO 배포 제외를 보존해. 다른 출시 레벨을 생성하지 마.

실제 에디터5 선택→Asset/MemoryPack 게임 플레이와 실제 Next/보드 포인터5단계를 확인해. 가로/세로 투명 포커스·주변 어둠·말풍선/손가락/입력 통과·최종 상태 동등·같은 보드 자유 플레이/일반 교환을 확인해. 자동/항상/실행 안 함·중단 재진입·완료1회/자동 생략·미완료 다른 레벨 포함1~5 기록 분리·실제4→5 늦은 신호 격리를 검사해. 실제 플레이어 기록/시험 값/화면 크기를 복원하고 강제 미션 완료 전환은 승리 밸런스와 구분해.

TutorialMagnetLevelVerification Preview→Apply→Run, 계획에 명시된 기존 회귀/관련 자석 검사를 실행해. 기존 종합 드론 시간 기대값 실패를 별도 보고하고 관계없는 변경은 하지 마. 과거 출력/임시 상태를 복원해. 테스트 해제 후 native Unity 게임 소스 단독 컴파일과 승인 diff·원본/GUID/HEAD/WIP 보존을 감사해. ProjectTests Compile로 대체하지 마.

빌드·번들·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장 금지. 새 이미지·전체 밸런스/해금/계정/인벤토리/매뉴얼/고물탑은 제외해. 실제 재현한 직접 관련 결함만 최소 수정해.

완료 보고를 Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-08-progress.md에 저장하고 현재 기획/구현 상태에 맞는 다음 콘텐츠의 계획·목표·복사용 실행문만 작성해. 다음 구현은 자동 시작하지 마.
~~~
