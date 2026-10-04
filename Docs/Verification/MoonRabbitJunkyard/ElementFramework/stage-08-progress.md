# EF-08 — 저장·50레벨 팩 호환 기준 확보 결과

상태: **완료**, 2026-10-04(KST). EF-09 문서 준비, 구현 미착수.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-08-storage-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-08-storage-baseline-goal.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경·실제 실행

Assets/Scripts/Features/Levels/Editor/Tests/LevelStorageBaselineVerification.cs와 meta만 추가했다. BoardNineVerification.Migration의 순수 메모리 검사를 직접 재사용하고 LevelPackVerification의 왕복/구간/거절 사례를 안전한 새 진입점으로 재사용했다. 생산 코드·원본/팩·저장 포맷·GUID·기존 작업 변경 없음. 금지된 전체 Run, LevelPackBuild.Generate, SaveAssetsAndPacks는 호출하지 않았다.

Unity6000.3.10f1 별도 배치 Editor에서 Levels.Editor.LevelStorageBaselineVerification.Run: **81 PASS / 0 FAIL**, 종료 코드0. 증거는 Logs/ElementFramework/Stage08/data.log, data-results.txt, data-execution.json이다. storage-observations.jsonl에는 실제 관찰 **35건**을 저장했다.

## 요구사항 대응

| 요구사항 | 검사/실제 결과 |
| --- | --- |
| 전체 필드 왕복·원본 보존 | 현재 유효 메모리 fixture는14개 PackedLevel 필드의 스칼라/기본 보드와 모든 목록에 값을 포함한다. 블록·발전기/상자·덮개·먼지·흐름 경로/출구·전선·고정 공급·회수·미션의 JSON/재인코딩 바이트 동일, 원본 JSON 보존 |
| 같은 시드/fingerprint | 현재 종합 fixture와 실제 Level_01 모두 시드12345의 유효한 초기 플레이 상태·전체 Snapshot/fingerprint 동일 |
| 참조 경계 | ToPacked의 Board/Obstacles/Supply는 원본 참조 공유. 복사 함수로 가정하지 않음. Encode/ReadLevel 왕복에서 실제 입력 보존 확인 |
| 50구간/주소 | 1/50→1,51/100→51,101→101, 주소 Levels/levels-NNNNNN. 입력50,1의 결과1,50으로 정렬하며 누락 번호를 생성하지 않음 |
| 거절 | 중복 번호·구간 혼합·누락/다른 구간 요청·잘린10바이트·빈 팩·번호0·포맷2·스키마99를 실제 거절하고 예외 종류/메시지 기록 |
| 구형/잘못된 fixture | 기존 v1/v2/v3 JSON7개를 버전 덮어쓰기 없이 읽음. 모두 초기 플레이 UnsupportedSchemaVersion/관련 진단 및 팩 인코딩 거절. 원본 오류와 디스크 보존 |
| 10×10→9×9 | 기존 Migration: 행 간격·마지막 행/열·경계2×2·덮개/먼지/회수·흐름/통로/합류/벽/전선/생성구·반복 변환·불완전 배열 보존. 추가 실제 값 관찰도 기록 |
| 기존 원본/팩 | Level_01.asset와 levels-000001.bytes 읽기 전용. 전체 JSON/팩 바이트 일치. 팩2047바이트, 주소 Levels/levels-000001, 원본 entry=<excluded>, 팩 의존성은 팩 자신뿐 |
| 안전/보존 | 시작 보호1148파일 SHA256 변경0. 기존 전체 Run/에셋 생성·저장·삭제/팩 갱신·빌드·임의 커밋·사용자 Unity 종료·씬 저장 없음 |

## 실제 관찰과 버전 구분

현재 레벨 스키마4, 팩 포맷1, 묶음50을 유지한다. unsupported-format-2와 unsupported-schema-decode는 메모리에서 만든 잘못된 바이너리이며 구형 파일을 변환한 자료가 아니다. JSONL의 codecFormat은 검사 당시 코덱 상수1이고 입력 파일에서 파싱한 임의 버전이 아니다. schema는 기록 대상 레벨 버전이며 바이너리 거절 사례의 입력 버전은 검사 사례/이름/코드로 구분한다.

각 관찰에는 입력 설명·시드·원본/결과 JSON·JSON SHA256·바이트 SHA256/길이·버전/구간/주소·실제 진단을 기록한다. detail의 상태/진단은 읽기용 Snapshot 문자열이고 내부 JSON이 아니다. legacy 사례의 bytes는 JSON fixture 원본 바이트이며 팩 바이너리와 구분한다. migration-values의 beforeJson은 실제 시험용10×10 보드/회수 입력 부분이며 나머지 메타데이터/색/이동 수는 New 기본값을 쓴다.

자르기 관찰의 원본 팩은253바이트다. 100칸→81칸, 비활성 원본 인덱스18→목적17, 회수(8,1) 유지/(9,1) 제거를 확인했다. 좌표는0부터 시작한다. 재역직렬화는 결과를 다시 바꾸지 않고 원본 메모리10×10/회수2개는 유지한다. 실제 Level_01 원본은11273바이트, 기존 팩2047바이트이며 byteMatch=True/jsonMatch=True다. 불일치나 누락 주소/원본 의존은 관찰되지 않았다.

## 검사 수정 이력·검토·한계

초안1회 실패는 번호0인 오류 fixture를 기록하며 FirstLevel을 호출한 신규 기록부 문제였다. 생산 코드는 변경하지 않고 유효하지 않은 번호의 구간=-1/주소 없음으로 기록하도록 수정했다. attempt1 로그/종료 코드/부분 결과를 보존했다. 이후79개 통과 후 자르기 실제 값 기록2개를 보완하고 최종81개 전체를 재실행했다. 인위적 생산 RED나 구형 원본 버전 덮어쓰기는 하지 않았다.

검사 범위·거절 진단·주소/의존 정보·fixture 소유권/finally 메모리 해제·GUID·해시·diff·문서 링크를 자체 검토했다. 계획의 별도 요청 시 병렬 에이전트 규칙에 따라 에이전트는 사용하지 않았다. 최종 미해결 필수 실패 없음.

실제 Addressables 번들 로드·Play Mode·플랫폼·UI·운영 데이터 로드는 미실행이다. 읽기 전용 주소/의존 정보가 실제 번들/플레이어 로드를 증명하지는 않는다. txt Unity 에셋 import/restart 검사는 생성·저장이 필요하므로 이번에는 기존 JSON fixture와 코덱 메모리 기준만 확보했다. 새 저장 형식·구형 업그레이드·프레임워크·드론 비행은 미구현이다.

## 다음 단계

[EF-09 보드 표현 경로 기준 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-09-artwork-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-09-artwork-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-09-command.md).

현재 표현 매핑이 Editor/런타임에 나뉘어 있으므로 같은 종류/상태의 이미지·프레임·아틀라스 주소 대응을 먼저 확보한다. 리소스 준비 집합/풀 수명과 봇 공개 관찰은 별도 후속 단계로 나누며 EF-09 구현은 시작하지 않았다.
