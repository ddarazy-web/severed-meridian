# 요소 제작 원본과 MemoryPack 검증

## 적용 범위

현재 확정된 정의18개와 기본 규칙/표현 카탈로그2개를 Assets/Data/Elements에 생성했다. 기존 영구 ID, 피해·공급·미션 행동, 그림과 효과 프레임을 유지했다. 기획 경로와 근거 항목이 없는 출시 제작 입력은 거절한다.

콘텐츠 팩은 Assets/Data/ElementPacks/default-content.bytes, Addressables 주소는 Elements/default-content다. 규칙/이미지 메타데이터142542바이트이며 실제 픽셀이나 프리팹을 포함하지 않는다. 현재1개 레벨의 팩은6239바이트다. 기존50레벨 구간을 유지한다.

기존 Level_01.asset에 카탈로그와 신형 기본 필드만 추가했다. 기존 파일 본문 전체가 새 파일의 앞부분과 동일하며 번호·배치가 보존됐다. 기존 레벨 메타 파일의 SHA256도 같다. 씬·프리팹·이미지·패키지·플랫폼 설정은 변경하지 않았다.

## 실행 결과

Unity 6000.3.10f1의 별도 배치 Editor에서 다음 검사를 실행했다. 각 실제 종료 코드는0이다.

| 검사 | PASS | 확인 내용 |
|---|---:|---|
| ElementContentAuthoringVerification.Run | 74 | 실제 SO18개·수치 보존, 전체 표현 왕복, 재실행 보존, 기획 출처/ID 충돌 거절, 카탈로그 병합, Asset/MemoryPack 표현 분리, 제작 원본 의존성 제외 |
| ElementPackVerification.Run | 53 | 팩1/2 호환·정의 폐쇄·기존 실행 상태 및 원본 보존 |
| LevelStorageBaselineVerification.Run | 82 | 구형 데이터·자르기·50레벨 경계·현재 에셋/디스크 팩의 전체 논리 상태와 다음 난수 동일 |
| ElementVisualBoardVerification.RunOrdering | 11 | 실제 Play Mode의 보드 시각 리소스와 정렬 |
| ElementContentAuthoringVerification.RunLoad | 5 | 실제 Addressables 콘텐츠/레벨 로딩, 제작 SO 미참조, 시작 보드 구성과 블록 이미지 조회 |

테스트 코드를 연결하지 않은 상태의 게임 소스 Editor 컴파일도 실제 종료0으로 확인했다. 대표 검사는 총225개 통과했다. 전체 기존 테스트를 다시 돌렸다는 뜻은 아니다. 테스트 코드는 Tests/Editor에 있고 검사 시에만 임시 연결했다. 마지막 검사 후 연결 기록과 Assets/__ProjectTests가 남지 않았다.

## 검사 중 수정한 사항

디스크 팩이 새로 팩2로 생성되면서 기존 검사의 팩1 고정 가정을 수정했다. 제작 SO 참조는 배포 값에 없으므로 팩1 JSON 왕복에서는 이 참조만 제외하고 나머지 필드를 비교한다. 팩2로 전환한 레벨은 스키마·저장 지문이 다르므로 실행 값과 다음 난수까지 비교한다. 바이트 재생성 비교도 True다.

Unity 원본에 재귀 표현 DTO를 직접 저장하지 않고 효과 프레임의 유한한 제작 구조를 사용한다. 실행 DTO와 기존 JSON 계약은 유지했고 MemoryPack 왕복과 실제 저장 원본을 통한 모든 효과 프레임의 보존을 확인했다. 기존 실행 DTO를 JsonUtility로 검사하는 일부 경로에는 타입 깊이 경고가 남아 있으며, 데이터 손실 여부는 왕복 검사로 확인했다.

실행 영수증과 개별 결과는 Logs/ElementContentAuthoring/Final, 실제 로딩 결과는 load-results.txt, 원본 보존 감사는 source-preservation.json에 있다. 기존 검사 출력은 실행 전에 백업하고 결과를 별도 보관한 뒤 원래 바이트로 복원했다.

플레이어 빌드·Addressables 번들 빌드·기기 검증·커밋·푸시는 수행하지 않았다. 자동 빌드 전 변환과 원본 의존성 검증 경로를 연결하고 데이터 파일만 생성했다. 기획 내용과 데이터 수치의 의미 대조는 제작자가 수행해야 한다.

[제작 안내](../../Guides/MoonRabbitJunkyard/ElementFramework/element-content-authoring.md) · [설계 결정](../../Decisions/project-wide/2026-10-07-element-content-authoring.md)
