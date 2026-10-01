# 5단계 목표 명령문

[작업 계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-goal.md)

아래 명령은 복사해 실행할 때 5단계 목표를 활성화한다. 문서 작성만으로 실행하지 않는다.

```text
목표를 설정하고 5단계 ‘통합 검증과 마무리’를 완료해줘.

C:/Projects/Git/ServeredMeridian만 사용해. 적용되는 AGENTS.md와 Unity 프로젝트 규칙을 확인하고 다음 문서를 읽어:
1. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md
2. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-04-progress.md
3. Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-goal.md
4. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-plan.md
5. Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-usage.md

superpowers:executing-plans로 네 작업을 순서대로 직접 실행해. 정상인 1~4단계를 유지하고 재현되는 결함만 최소 수정해. 두 입력 소스의 선택 맵·시드·미저장 사본 보존, 아이템·일시정지·연쇄·라스트팡·승패·다시하기·편집 복귀와 반복/취소 수명을 검증해.

실제 Addressables 번들을 빌드해 로드하고 일반/매칭 생성 파워/대표 장애물 이미지를 확인해. 50레벨 MemoryPack 경계와 잘못된 팩, 원본 LevelDefinition의 직접·간접 참조 배제를 검사해. 기존 분할 atlas와 GUID를 유지해. 빌드 전처리가 생성 팩/설정을 바꿀 수 있으므로 기준 상태를 보존하고 원본 레벨을 자동 저장하지 마.

PuzzleGame 씬을 명시한 Android Development APK를 Builds/Stage05/Android 아래 로컬 빌드해. 실제 빌드 씬이 제외 검사에 반영되게 하고 BuildReport·APK 해시·번들 구성으로 결과를 증명해. 임시 빌드/재생/Addressables 설정은 복원해. 모듈·SDK·서명 문제를 임의 설치나 상용 설정 변경으로 우회하지 마. Android 빌드가 막히면 다른 플랫폼으로 성공을 대체하지 마.

네 화면 비율과 안전 영역·회전 상태 보존을 확인하고 캡처를 남겨. 실기기 설치·실행은 기기와 사용자 승인이 있을 때만 진행하고, 없으면 미검증 항목과 재현 절차를 별도로 기록해. Editor/빌드 결과를 실기기 검증으로 표현하지 마.

열린 같은 프로젝트에 두 번째 Unity 프로세스를 실행하지 말고, 다른 프로젝트 Editor를 조작하거나 사용자 Editor를 강제 종료하지 마. 배치 종료용 검사를 열린 Editor에서 그대로 호출하지 마. 기존 변경과 미저장 데이터·.meta/GUID를 보존하고 자동 커밋·푸시·업로드하지 마.

Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-05-progress.md와 Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-usage.md를 작성해. 최종 독립 리뷰와 필요한 수정 검증·임시 파일 정리를 마친 뒤 항목별 완료 감사를 해. 필수 Editor·실제 번들·Android 빌드 조건을 충족한 경우에만 목표를 완료 처리하고 결과·캡처·빌드 위치·미검증 사항을 보고해.

새 이미지·상세 애니메이션·게임 규칙·광고/결제/보상·다음 레벨·상용 배포·iOS 빌드는 진행하지 마.
```
