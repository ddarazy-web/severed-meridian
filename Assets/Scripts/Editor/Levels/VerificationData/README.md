# 버전 1 호환성 검증 원본

2026-09-27, 3단계 모델 변경 **이전 코드**의 `LevelDefinitionVerification.PrepareAndVerify`를 Unity 6000.3.10f1에서 실행하여 생성·저장했다.

- `Version1Normal.txt`, `Version1Invalid.txt`: 실제 Unity `.asset` 파일의 원본 바이트를 그대로 복사한 자료다. 신규 필드가 없는 이전 직렬화 형태를 유지한다.
- 같은 이름의 `.json`: 해당 실행에서 남긴 `restart-state.json`의 저장 직전 기존 필드 전체 스냅샷이다.
- 검증은 임시 전용 폴더에 `.asset`으로 복사하고 Unity가 직접 import하도록 한다. 전환 전후 기존 필드, 오류 값, 에셋 GUID와 디스크 자동 저장 여부를 비교한다.
- 새로운 모델로 만든 에셋을 버전 1이라고 표시한 대체 자료로 바꾸지 않는다. `m_Script` GUID는 프로젝트의 기존 `LevelDefinition.cs.meta`를 가리킨다.

자료의 오류는 의도된 검증 입력이며 자동 정리하지 않는다. 실제 게임용 레벨이나 런타임 리소스로 사용하지 않는다.

## 버전 2 원본

`Version2Normal/Invalid.txt`와 같은 이름의 JSON은 2026-09-27 4단계 모델 변경 전 기존 `LevelObstacleVerification.Prepare`를 Unity 6000.3.10f1에서 실행하여 확보했다. 정상 자료는 빈 보드, 오류 자료는 중복·잘못된 색·범위 밖 장애물과 일반 블록·먼지를 포함한다. 별도 `Restart` 실행으로 원본 보존과 임시 에셋 정리를 확인한 뒤 모델 변경을 시작했다. 로그는 `Logs/LevelFlowVerification/version2-prepare.log`, `version2-restart.log`에 있다. 버전 숫자를 바꿔 만든 자료가 아닌 실제 이전 직렬화 바이트다.

## 버전 3 원본

`Version3Flow/Invalid/Saved.txt`와 같은 이름의 JSON은 2026-09-27 5단계 모델 변경 전 `LevelFlowVerification.Prepare`에서 저장한 실제 v3 에셋이다. Flow는 장애물 6개·연결 3개, Invalid는 장애물 4개·연결 1개 및 오류를 포함한다. 기존 코드의 별도 Restart가 통과한 뒤 모델을 변경했다. `Logs/LevelSupplyVerification/v3-source-state.json`에 원본 폴더·JSON·GUID를, v3-baseline 및 v3-baseline-restart 로그에 실행 증거를 보관한다.
