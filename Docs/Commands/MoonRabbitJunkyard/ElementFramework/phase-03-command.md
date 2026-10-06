# 큰 구간 3단계 — 복사용 목표 실행문

상태: 완료. 2026-10-06. 실행문은 완료 범위의 기록으로 보존한다. 다음 실행문은 [큰 구간4](phase-04-command.md)다.

```text
ServeredMeridian 요소 확장 리팩토링의 큰 구간 3단계 ‘정의 ID 저장과 레벨 제작 도구’를 실행해.

Docs/Planning/MoonRabbitJunkyard/ElementFramework/phase-03-storage-authoring-plan.md, Docs/Goals/MoonRabbitJunkyard/ElementFramework/phase-03-storage-authoring-goal.md, integration-guideline.md와 2단계 완료 기록을 읽고 목표를 설정해 진행해. work의 현재 HEAD·완료분·기존 WIP를 보존하고 하위 에이전트 없이 직접 작업해.

계획의 A~D 묶음으로 제작 카탈로그와 불변 정의/DTO, 정의 ID 배치와 선택 변환, 스키마5/팩2 및 명시적 구형4/1 읽기, 50레벨 팩, 카탈로그 검색·선택·검사 MVVM을 연결해. 정의 ID와 본체 인스턴스 ID를 구분하고 구형/신형 목록을 중복 실행하지 마. 같은 행동의 서로 다른 두 신규 ID가 새 종류 enum이나 실행 분기 없이 실제 배치·타격·미션·드론 조회까지 재사용되는지 검증해. ID를 저장만 하거나 실행 시 카탈로그 정의를 버리는 것으로 마무리하지 마.

검색/선택/검사에서 원본을 자동 저장하지 말고, 실제 변환은 미리보기와 선택 적용을 분리해. 원본 편집은 기존 SerializedObject/Undo를 유지해. Asset·기존 디스크 MemoryPack·메모리 팩2로 같은 레벨/시드를 테스트하고 게임 플레이·다시하기·연결·공급·미션·난수와 보드 이미지/2단계 드론 동작을 대조해. 팩에는 구간의 배치·공급·행동·연결에 필요한 정의 DTO를 포함하고 제작 원본의 배포 제외를 검증해.

실패 검사→최소 구현→관련 검증으로 진행하고 내부 항목마다 별도 EF 단계나 전체 회귀를 반복하지 마. 최종 신규/관련 회귀와 기존30종·드론9종의 실제 종료0/FAIL0 및 원문 비교를 확인해. 새 ID/버전 메타데이터와 실제 논리 차이를 분리하고 기존 출력/팩/에셋/GUID/과거 증거를 보존해.

빌드·Addressables 콘텐츠 빌드·디스크 팩 재생성·전체 에셋 일괄 변환·이미지 생성·커밋·푸시·사용자 Editor 종료·임의 씬 저장은 하지 마. 새 패키지/asmdef·전면 MVVM·HUD/팝업/튜토리얼/고물탑·4단계 표현/리소스/풀 구현은 끼워 넣지 마. 목표 의미나 원본 변경 범위가 바뀌면 그 변경만 논의하고 독립 작업은 계속해.

완료 시 변경 결과·검증 근거·제약을 보고하고 Docs/Verification/MoonRabbitJunkyard/ElementFramework/phase-03-progress.md에 기록해. 다음 큰 구간4의 계획서·목표문서·전체 복사용 실행문을 작성하되 4단계 구현은 자동 시작하지 마.
```
