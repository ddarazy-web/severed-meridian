# 튜토리얼 후속7단계 — 복사용 목표 실행문

상태: 완료. 신규4레벨과 실제 검증을 마쳤다. 아래는 수행한 실행문이다. 결과는 Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-07-progress.md를 따른다.

~~~text
ServeredMeridian 튜토리얼 후속7단계 ‘4레벨 수거 드론 콘텐츠’를 진행해.

Docs/Planning/MoonRabbitJunkyard/Tutorial/stage-07-drone-content-plan.md와 integration-guideline.md, Docs/Goals/MoonRabbitJunkyard/Tutorial/stage-07-drone-content-goal.md, Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-06-progress.md 및 연결된 기획을 읽고 목표를 설정해 이 세션에서 직접 수행해. 현재 HEAD/WIP·완료된1~6단계·기존1/2/3레벨/GUID·모든 기존 구간 레벨을 보존해.

기존 제작 도구·요소 카탈로그·등록 처리기·공통 엔진·실제 실행기·드론 표적 선택/비행·안내/기록을 재사용해. 전체9×9 schema5 실제 Level_04.asset을 제작해. 설명→지정2×2로 드론 하나 생성→생성 설명→새 드론 인접 교환 발동→종료 설명으로 진행하고 같은 보드에서 자유 플레이로 이어가게 해. 새 진행 엔진·드론별 분기·새 표적 선택기·파워 조합 안내는 추가하지 마.

메모리 후보의 실제 조회·실행·재생으로 생성/낙하 후 좌표·후속 상대·시드·고정 공급을 확정해. 정확한2×2/4칸·생성 결정/보드 드론1개·Generated/Activated power.drone을 확인해. schema5 일반 생성구는 ElementSupply를 유지해. 임의 입력/제자리 발동·공급 부족/누락/무작위 대체를 거절해. 두 교환 이동 차감·실제 미션 집계·남은 미션·일반 공급 복귀를 검증해.

직접+5칸과 비행 후 추가 표적1개/실제 제거를 추가 연쇄와 구분해. 현재 미션 우선 선택과 떠오름→호버→돌진을 그대로 사용해. 표적을 미리 표시/강조하지 마. 드론 비행/재탐색/피해/수집 표시가 끝나기 전에 단계나 완료 기록을 앞당기지 마. 임의의 비행 시간/궤도/우선순위 변경은 하지 마.

검증된 신규4레벨/메타와 동일1~50 구간 팩 갱신을 허용해. CreatePackBytes로 모든 기존 구간 레벨을 포함하고 다른 팩/ECPK/주소/제작 SO 배포 제외를 보존해. 기존4가 있으면 먼저 현재 데이터를 평가하고 사용자 변경/GUID를 덮어쓰지 마. 다른 출시 레벨을 생성하지 마.

실제 에디터에서4레벨 선택→Asset/MemoryPack 게임 플레이와 실제 Next/보드 포인터로5단계를 확인해. 가로/세로 투명 포커스·어둠 막·손가락·말풍선/입력 전달과 최종 상태 동등·일반 교환을 확인해. 자동/항상/실행 안 함·중단 재진입·완료1회/자동 생략·미완료 다른 레벨 포함1/2/3/4 기록 분리·실제3→4 늦은 신호 격리를 검증해. 시험 기록과 실제 플레이어 기록을 분리하고 이전 값을 복원해. 전환을 시험 인스턴스 미션 강제 완료로 유도했다면 실제 승리 밸런스 검사와 구분해.

TutorialDroneLevelVerification Preview→Apply→Run과 계획에 명시된 기존 회귀/관련 드론 검사를 실행해. 기존 종합 드론 시간 기대값 실패를 별도 보고하고 관계없는 변경은 하지 마. 실제 UI/논리/기록 검사 범위를 구분하고 과거 출력은 백업·복원해. 테스트 해제 후 native Unity 게임 소스 단독 컴파일과 승인 diff·원본/GUID/HEAD/WIP 보존을 감사해. ProjectTests Compile로 단독 컴파일을 대체하지 마.

빌드·번들·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장을 하지 마. 새 이미지·다른 소개 레벨·전체 밸런스/해금/인벤토리/계정/매뉴얼/고물탑은 제외해. 실제 재현된 직접 관련 결함만 최소 수정하고 회귀 근거를 기록해.

완료 후 변경·실제 검증·제약을 보고하고 Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-07-progress.md에 저장해. 완료 결과를 기준으로 다음5레벨 무지개 자석의 계획서·목표 문서·복사용 실행문을 작성해. 다음 구현은 자동 시작하지 마.
~~~
