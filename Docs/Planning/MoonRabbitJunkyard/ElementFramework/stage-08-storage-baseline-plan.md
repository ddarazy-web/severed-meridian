# EF-08 — 저장·50레벨 팩 호환 기준 확보 계획

상태: **완료**, 2026-10-04. 81 PASS / 0 FAIL, 실제 관찰35건.

목표: 정의 기반 전환 전 기존 스키마4·팩 포맷1·50레벨 구간·9×9 데이터의 직렬화와 호환 거절 기준을 확보한다. 새 저장 형식과 원본 재생성은 하지 않는다.

연결: [가이드라인](integration-guideline.md) · [EF-07 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-07-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-08-storage-baseline-goal.md).

실행 담당: superpowers:executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 작은 작업 단위

1. 기존 변경·Editor·보호 파일을 기록하고 LevelDefinition.ToPacked/FromPacked, PackedLevel, LevelPackCodec/Loader, LevelBoardSizeMigration과 원본/팩/Addressables 참조를 조사한다. ToPacked는 참조 목록을 공유하므로 원본 보호를 복사 보장으로 오해하지 않는다.
2. 기존 LevelPackVerification의 순수 메모리 검증 사례와 BoardNineVerification.Migration을 재사용한다. 전체 Run은 금지한다. LevelPackVerification은 Generate/에셋 생성·삭제/UI가 포함되고 BoardNineVerification.Run은 SaveAssetsAndPacks를 호출한다. 안전한 부분만 새 Editor 진입점에서 재사용한다.
3. 현재9×9 메모리 레벨과 읽기 전용 JSON fixture로 전체 필드 왕복/같은 시드 상태·fingerprint를 비교한다. 블록/장애물/덮개/바닥/흐름/전선/공급/회수/미션을 포함한다. 구형/잘못된 데이터는 현재 거절·진단 결과를 기록하며 자동으로 업그레이드하거나 고치지 않는다.
4. 1/50/51/100/101 구간·주소, 번호 정렬/누락 보존, 중복/구간 혼합/잘린 바이트/미지원 포맷·스키마 거절과 완전한10×10의9×9 자르기·불완전 데이터 보존·반복 변환을 확인한다. 기존 원본/팩은 읽기 전용으로 열어 메모리 비교하고 실제 호환 불일치를 숨기지 않는다. 디스크 쓰기·팩 갱신·Addressables 빌드는 하지 않는다.
5. 필요한 안전한 메모리 Baseline.cs/meta만 Levels/Editor/Tests에 추가하고 시드·원본 JSON/해시·바이트 해시/길이·버전·구간/주소·진단·왕복 결과를 Logs/ElementFramework/Stage08에 기록한다. 별도 Editor 실행 종료 코드·필수 FAIL/예외0·원본 해시 보존을 확인한다.
6. stage-08-progress.md와 계획/목표를 갱신하고 EF-09의 가장 작은 선행 작업을 실제 결과로 정한다. 후보는 보드 표현/리소스 준비 집합/봇 공개 관찰 경계 중 하나이며 한 단계에 모두 합치지 않는다. 다음 계획·목표·명령문만 제공하고 EF-09 구현은 하지 않는다.

## 제약·위험

생산 코드·스키마/enum/직렬화 필드 순서·에셋/GUID·MemoryPack 원본·Addressables·패키지·진행 중 변경을 보존한다. 빌드·임의 커밋·사용자 Unity 종료·씬 저장·전체 검사 Run의 에셋 생성/삭제 금지. 새 프레임워크·드론·UI·저장 변환/적용은 제외한다.

위험: ToPacked 공유 참조를 변환 중 변경, 구형 스키마와 팩 포맷을 같은 버전으로 오해,50구간 경계/누락 번호 왜곡,9×9 변환 후 흐름/2×2/연결 누락, 기존 파일의 불일치를 임의 재생성으로 덮기. 필수 실패가 나오면 원인과 수정 경계를 보고하고 생산 수정까지 임의 확대하지 않는다.

실제 결과: [EF-08 완료 보고](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-08-progress.md). 기존 메모리 Migration과 팩 사례 재사용, 신규 Editor 검사만 추가. 원본/팩/기존 파일 보존. 실제 원본/팩 일치, 새 포맷·변환은 미구현.
