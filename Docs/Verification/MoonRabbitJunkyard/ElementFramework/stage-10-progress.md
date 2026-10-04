# EF-10 — 레벨 리소스 준비·아틀라스 소유권 검증

상태: 완료. 2026-10-04, ServeredMeridian / Unity 6000.3.10f1.

## 변경·실행 결과

ResourceBaselineVerification.cs와 meta만 추가했다. 기존 예외 원복 수정과 모든 생산/원본/기존 작업은 그대로 유지했다. 별도 batchmode/nographics Editor에서 **200 PASS / 0 FAIL, 종료0**, 실제 관찰 **45건**. 증거는 Logs/ElementFramework/Stage10의 baseline.log, baseline-results.txt, baseline-execution.json, resource-observations.jsonl이다. 기존 번들을 실제 BundledAssetProvider로 로드했으며 대체 locator/provider/로더를 넣지 않았다. 콘텐츠/플레이어 빌드·재패킹 없음.

## 실제 준비 집합

모든 기본 입력은 seed12345, 메모리 LevelDefinition의 원본 JSON을 관찰에 저장했다. 주소의 MoonRabbitBoard- 접두사는 아래 표에서 생략했다.

| 입력 | 실제 atlas 수 | 기본3개 외 주소 |
| --- | --- | --- |
| 일반 색 미션, 초기 파워 없음 | 3 | 없음 |
| 초기 로켓/폭탄/드론/자석 | 3 | 없음 |
| 고정 공급 고철/회수, 회수 미션/출구 | 5 | Obstacles-Scrap, BoardDevices-Recovery |
| 고철/회수 유지 공급 | 5 | 위와 같음 |
| 발전기/상자·거미줄/먼지·벽/통로/전선 | 10 | Obstacles-Generator/Crate/Web/Dust, BoardTerrain-Walls, BoardDevices-Portals/Wiring |
| 상자/고철/회수캡슐/색잠금/철근상자와 해당 미션, 각각 | 4 | 해당 Obstacles 주소1개 |
| 거미줄/먼지/곰팡이와 해당 미션 | 6 | Obstacles-Web/Dust/Mold |
| 전체 효과 원본80프레임 inventory 입력 | 12 | Effects-BombExplosion/Drone/DustClear/GeneratorCharge/Magnet/Match/MetalBreak/MoldClear/PowerCreation/Rocket/WebBreak/WoodBreak |

기본3개는 Blocks, PowerBlocks, BoardTerrain-Floor. 매칭으로 파워가 만들어지므로 초기 파워가 없어도 PowerBlocks를 준비한다. 해당 공유 아틀라스 안의 다른 색·파워·내구도 이미지는 같은 주소에 동반 포함되는 것이며 별도 종류 주소 요청과 다르다. 기본 사례에는 미사용 장애물/장치/효과 주소가 없었다. 준비를2회 반복해 BoardSpriteAtlas 객체와 Sprite 클론의 재사용을 확인했다. 같은 Match 프레임2개/null은 효과 주소1개만 추가했다. 효과 inventory는 전체 리소스 경로의 준비/조회 검증이며 특정 타임라인이 모든 효과를 동시에 준비한다는 뜻이 아니다. 실제80개 Sprite 조회 성공. 실제 준비한 주소와 provider/internalID는 JSONL에 기록했다.

## 수명·소유권

- 준비 전 유효 경로 Get은 InvalidOperationException, Get(null)은 null. Dispose 후 유효 Get/Prepare는 ObjectDisposedException이며 Get(null)은 계속 null이다.
- 사전 취소는 atlas0/pending0. native InternalIdTransformFunc에서 경로를 그대로 반환하며 pending1에 취소/Dispose를 실행했다. Dispose 직후 아직 목록1개를 유지하고 실제 로드 완료 후 pending0/목록0/핸들 해제로 정리됐다. 취소만 하면 완료 핸들을 소유자가 유지하고 최종 Dispose에서 반환했다. 원래 훅은 finally에서 복원했다.
- 이 훅은 native 요청의 ID/cache-key 처리 시점이다. 아직 핸들이 대입되지 않은 상태이므로 물리 I/O가 이미 진행 중인 시점이나 모바일 비동기 프레임 지연을 검증했다고 표현하지 않는다.
- 두 PuzzleArtwork가 같은 주소를 준비하면 실제 Addressables 핸들은 공유하고 Sprite 클론은 독립이다. 이전 소유자의 Dispose 후 후보 핸들/클론은 유지된다. 마지막 소유자 Dispose 후 native Editor update3회와 지연 콜백 처리가 끝나면 공유 핸들이 무효화된다.
- 누락 주소 Effects-__EF10Missing__는 실제 InvalidKeyException으로 실패, pending0/실패 atlas1. Dispose 직후는 Addressables 지연 완료 콜백의 참조 때문에 handleValid=true일 수 있다. Editor update3회 후 false를 실제 확인했다. 즉 Dispose 호출 직후 핸들 무효만으로 해제 누수 여부를 판단하면 안 된다.
- 예상 누락 주소의 Addressables 오류 로그1건은 실패 경로 검사 입력에 따른 정상 진단이다. 검사 자체 필수 FAIL/예기치 않은 예외/컴파일 오류는 최종0이다. 엔진 종료 MemoryLeaks 요약은 실기기 메모리 성능 측정으로 해석하지 않았다.

## 호출부 확인·검증 한계

ownership-source-evidence.json은8개 소스의 전체 내용/해시를 보존한다. 세션 시작은 PuzzleGameSession.PrepareAsync가 artwork를 소유하며 시작 취소/실패/파괴에서 반환한다. 재시작/전환은 candidate 준비→조회/보드 표시 성공→소유권 교체→previous Dispose, 실패/취소의 finally는 candidate Dispose다. 효과는 PuzzlePowerPlayback.PrepareAsync가 clips.Paths만 PrepareEffectsAsync 후 Get으로 조회한다. 실제 클래스의 두 소유자 검사는 수행했지만 세션 전체 UI/PlayMode 재시작/전환 검사는 이번에 실행하지 않았다. 기존 전환 검사는 임시 팩/대체 provider/UI를 포함해 이번 원본 보존/native loader 범위에서 실행하지 않았으며 안전한 메모리 fixture/기대집합만 재사용했다.

실기기·플레이어·프레임 중 I/O 지연·전체 UI 전환·렌더러 풀/GC/플랫폼 메모리 성능은 미측정이다. 12개 효과 전체 준비는 검사 입력이며 생산의 필요 시 준비 정책은 변경하지 않았다. 취소는 underlying Addressables 요청을 중단시키는 정책이 아니라 로드 완료 후 소유자가 반환하는 현재 정책이다.

## 중간 실패·보존

attempt1은 새 검사에서 internal 메서드 직접 접근한 컴파일 오류였다. Editor reflection으로 생산 메서드를 호출하도록 수정했다. attempt2는 실패 핸들의 즉시 무효화를 요구한 잘못된 검사 시점, attempt4는 새 경로 문자 문법 오류, attempt5는 덮개 내부 블록 누락 fixture였다. attempt7/8의 공유 핸들 즉시 해제 기대도 native 지연 콜백 이후 관찰로 바로잡았다. 중간 로그/실행값은 삭제하지 않았다. 생산 코드는 수정하지 않았다.

preservation-results.json: 시작 보호 **1787파일 SHA256 변경0**. 원본/meta GUID·아틀라스/Addressables/레벨 팩·씬/프리팹·기존 생산 변경 보존. 임의 커밋·사용자 Unity 종료·씬 저장·이미지 생성/수정·빌드/재패킹 없음. EF-09의 원본1254px3개/바닥 표현 차이는 그대로 남았다. 병렬 에이전트 사용 없음. 이번 리소스 수명 범위의 미해결 필수 실패 없음.

## 다음 단계

[EF-11 풀 기준 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-11-pool-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-11-pool-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-11-command.md).

리소스 소유권과 분리하여 월드 보드/효과 렌더러의 생성·재사용·초기화·반환 기준만 확보한다. 공용 풀 전환·사전 생성·봇은 구현하지 않는다. EF-11 상세 문서만 작성했고 구현 미착수다.
