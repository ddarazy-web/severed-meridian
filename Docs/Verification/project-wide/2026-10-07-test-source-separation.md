# 테스트 소스 분리 검증

## 변경 범위

- 14개 테스트 폴더를 Tests/Editor의 Features 및 Systems 아래로 이동했다.
- C# 294개를 포함해 총 633개 파일을 이동했다. 메타 324개는 원본 바이트와 GUID를 보존했다.
- fixture를 읽는 C# 6개에서 경로만 새 원본 위치로 수정했다. 변경 경로를 역치환한 SHA256도 이동 전 원본과 일치한다.
- 테스트 외 기존 Assets/Scripts 파일 632개는 SHA256이 모두 일치한다.
- 런타임·에셋·씬·패키지·asmdef 변경, 게임 빌드, 커밋은 하지 않았다.

## 실행 검증

- 연결 도구: 실제 임시 파일 및 실패하는 외부 프로세스로 검사 17개 통과. 중복 연결, 복사본 수정, 추가 파일, 미소유 폴더, 잘못된 메서드, 열린 Editor, 실행 실패 후 해제를 검사했다.
- 연결 상태의 Unity 6000.3.10f1 Editor 컴파일: 실제 exit 0.
- 연결 해제 상태의 게임 소스 Editor 컴파일: 실제 exit 0. 최종 상태에는 임시 테스트 폴더 및 연결 기록이 없다.
- Elements.Editor.ElementIdVerification.Run: PASS 49 / FAIL 0 / 실제 exit 0.
- Levels.Editor.LevelStorageBaselineVerification.Run: PASS 81 / FAIL 0 / 실제 exit 0. 이전 데이터 fixture의 새 경로를 실제로 사용했다.
- Elements.Editor.ElementVisualBoardVerification.RunOrdering: PASS 11 / FAIL 0 / 실제 exit 0. 실제 Play Mode와 시각 리소스 경로를 검사했다.

대표 검사 합계는 141개다. 전체 기존 검사를 다시 실행했다는 의미가 아니다. 이동 파일 바이트 동일성, 전체 컴파일 및 이동으로 영향을 받는 경로·어셈블리·실제 Play Mode를 검증했다.

과거 검사 출력 5개는 실행 전 백업하고 실행 후 원래 바이트로 복원했다. 이번 결과와 실제 실행 영수증은 Logs/TestHarness/Verification-20261007-105817에 별도로 보관했다. 이동 감사는 Logs/TestHarness/migration-audit.json에 있다.

최초 제한된 실행 환경에서는 Unity가 사용자 Curl 캐시 DB를 열지 못해 컴파일 전에 종료했다. 정상 실행 권한으로 재검사하여 통과했으며, 실패 후에도 임시 연결은 해제됐다. 실제 기기 검사와 게임 빌드는 수행하지 않았다.

[사용 안내](../../../Tests/README.md)

영구 .asset/.prefab/.unity/.controller/.overrideController에서 테스트 C# 294개의 GUID를 조회했으며 참조가 없다.
