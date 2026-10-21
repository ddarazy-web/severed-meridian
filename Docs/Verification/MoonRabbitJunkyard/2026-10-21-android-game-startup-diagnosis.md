# Android 게임 시작 지연 진단

2026-10-21. 조사와 PC CPU 측정만 수행. 제품 코드·에셋 수정, 제품 빌드, 커밋·푸시, HTML 갱신 없음.

## 확인 범위

- 현재 Android Game 프로필은 PuzzleGame 씬으로 직접 시작한다.
- 기존 APK: Builds/Android/moon_rabbit_junk_0.1.0_1.apk (2026-10-20, 67,932,061바이트). 설치된 APK와 동일한지는 확인하지 못했다.
- ADB에는 offline emulator만 표시되어 실제 기기 측정 불가. 기기를 재시작하거나 데이터를 삭제하지 않았다.
- 현재 생성된 MemoryPack으로 레벨 1/2/15를 각각 최초 1회+반복 3회 측정했다. Unity Editor CPU 측정이며 APK I/O·텍스처 업로드·씬 첫 프레임·Android 실제 시간은 포함하지 않는다.

## 확인된 비용

레벨 1 최초 / 정상 3회 중앙값(ms):

| 구간 | 최초 | 중앙값 |
|---|---:|---:|
| 공통 요소 데이터 해석 | 40.90 | 12.60 |
| 레벨 팩 해석·검증·선택 | 357.98 | 193.02 |
| 재시도용 스냅샷 | 18.81 | 1.70 |
| 튜토리얼 전체 사전 재생 | 304.80 | 237.15 |
| 실제 시작 보드 구성 | 80.26 | 80.00 |
| 필요한 리소스 계획 | 13.01 | 3.20 |

첫 측정 합계 약 816ms. 정상 구간 중앙값의 단순 합은 약 528ms이며 전체 구간 중앙값과 같다는 뜻은 아니다. 최초 비용에는 Editor JIT/정적 초기화가 포함될 수 있다. Android IL2CPP 수치로 환산하지 않는다.

## 코드 근거

1. PuzzleGameSession.PrepareAsync → CheckTutorialReplay → LevelTutorialReplayValidator.Validate: 시작 전 튜토리얼의 교환·연쇄를 동기적으로 재생한다. 이 과정에서 별도 보드를 만든 후 실제 시작 보드를 다시 만든다.
2. LevelPackCodec.ReadTutorialLevel → DecodeTutorial/ValidateTutorialPack → DecodeElements/ValidateElements: 요청 레벨 하나뿐이어도 팩 전체의 레벨·튜토리얼을 검사한다. 중첩 버전 래퍼와 선택 레벨 읽기에서 요소 팩 해석·검증이 반복된다. 전체 게임 JSON을 읽는 것은 아니며 50레벨 구간 팩을 사용한다.
3. PuzzleArtwork.LoadAsync는 foreach에서 각 atlas.LoadAsync를 await한다. 레벨 1 리소스 계획은 Blocks, Floor, BombExplosion, Drone, Magnet, Match, PowerCreation, Rocket, PowerBlocks 총 9개 아틀라스다. 등장하지 않는 장애물까지 모두 읽는 구조는 아니다.
4. LevelPackLoader는 진입마다 세대·공통 요소·레벨 팩을 읽고 ContentPackAssetLoader는 각각 핸들을 해제한다. 세대 일관성은 보존해야 하지만 재방문 비용 개선 후보다. 실제 번들 재로드 횟수는 아직 계측하지 않았다.
5. PuzzleAudioPlayback.Initialize는 14개 효과음의 샘플을 동기로 합성한다. 이번 CPU 표에는 포함하지 않았다.
6. PowerBlocks 아틀라스 importer는 기본 RGBA32/무압축 설정이며 별도 Android override가 없다. 실제 기기의 GPU 포맷·페이지 수·업로드 시간을 측정한 후 압축을 결정해야 한다. APK 번들 압축 크기는 GPU 메모리 크기가 아니다.

## 권장 순서

- 1차: 시작 단계별 계측을 마련하고 제작 시 전체 검증과 게임 실행 시 필요한 검증을 분리한다. 팩을 한 번 해석해 재사용하고 선택 레벨만 실행 객체로 만든다. 세대 해시·버전·필수 참조·손상 데이터 거절은 유지한다. 튜토리얼 사전 재생은 검증된 배포 데이터와 툴 시험 입력의 정책을 구분해 줄인다.
- 2차: 필요한 아틀라스만 제한된 동시 로딩으로 준비한다. 취소·실패·핸들 소유권을 유지하고 최대 동시 로딩/메모리를 실측한다. 모바일 텍스처 압축, 효과음 사전 생성도 따로 비교한다.
- 3차: 기기에서 앱 실행→첫 화면→조작 가능까지 최초/재실행/다음 레벨을 측정해 완료 판단한다. Unity 스플래시 이전 지연과 게임 세션 지연을 구분한다.

## 증거

`Logs/TestHarness/20261021-175513-857-Run.log`: exit 0, 테스트 연결 해제 확인.
`Logs/GameStartup/bb90ecbf44094d689b48109da44d19f7`: timings.tsv, atlases-1/2/15.txt, result.txt 보존.
일회용 검사 스크립트와 meta는 검사 후 제거했다. 실제 제작 데이터는 읽기만 했다. 실제 APK 지연의 주원인은 아직 확정하지 않았다.
## 사용자 증상 확인 후 우선순위 변경

사용자: 조작은 느리지 않으며, 앱 실행 후 게임 화면이 나타나기 전 빈 화면이 오래 지속됨.

- Android Game의 첫 씬은 PuzzleGame 하나다. 가벼운 시작 씬은 없다.
- 첫 씬은 PuzzleScreen/WorldBoard/GameSession 프리팹을 직접 참조한다. UI는 MoonRabbitUI-Regular.ttf를 직접 참조한다. 기존 APK 빌드 로그의 폰트 크기는 5.9MB, 세로/가로 배경은 각각 약 914.4KB다. 이는 빌드의 비압축 크기이며 로딩 시간이나 실제 동시 메모리 점유량이 아니다.
- 게임 자체의 Assets/Scripts에서 RuntimeInitializeOnLoadMethod 초기화는 검색되지 않았다. 외부 패키지 전체에 초기화가 없다는 뜻은 아니다.
- 실제 APK manifest는 UnityPlayerGameActivity와 androidx.startup provider(EmojiCompat/ProcessLifecycle)를 포함한다. Firebase/GPG의 native 자동 초기화 provider는 이 manifest에서 발견하지 못했다. 플러그인이 느리다고 추정하여 제거하지 않는다.
- 프로젝트 Android 프로필과 APK manifest 모두 Unity splash가 켜져 있다. 사용자가 본 빈 화면의 시점은 아직 구별되지 않는다.
- PuzzleBoardPreview 컴포넌트는 씬에서 disabled다. 미리보기와 게임 세션이 함께 로드되어 중복 시작한다고 판단하지 않는다.

수정 우선순위: 앱 Activity 시작→Unity 초기화→첫 씬/첫 프레임→게임 준비 완료를 구분하는 실기기 기록이 먼저다. Unity 이전 공백은 Android 시작 화면/엔진 초기화 영역이며, Unity 이후 공백은 최소 리소스의 시작 씬을 먼저 표시하고 게임 씬을 비동기로 준비하는 방식을 검토한다. 첫 프레임을 실제로 출력한 뒤 무거운 처리를 시작해야 한다. 이는 대기 안내 개선과 실제 처리 시간 단축을 별개로 검증해야 하는 변경이다. 게임 씬에 로딩 UI만 추가하면 엔진 이전 공백은 해결되지 않는다.

기존 CPU 병목(반복 팩 검증·튜토리얼 사전 재생)은 확인됐지만 이번 빈 화면의 주원인으로 확정하지 않는다. 이 추가 조사에서도 제품 코드/씬/프리팹/빌드 프로필을 수정하거나 빌드하지 않았다.
