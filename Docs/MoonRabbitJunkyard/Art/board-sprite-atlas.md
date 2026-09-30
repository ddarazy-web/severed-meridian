# 보드 Sprite Atlas와 Addressables

## 분리 기준

한 장짜리 아틀라스를 폐기하고 일반 블록, 파워 블록, 장애물 종류별로 분리한다. 같은 종류의 내구도·색상·애니메이션 프레임은 같은 아틀라스에 넣는다.

| 원본 경로 | Addressables 주소 |
|---|---|
| `Assets/Textures/Blocks/` | `MoonRabbitBoard-Blocks` |
| `Assets/Textures/PowerBlocks/` | `MoonRabbitBoard-PowerBlocks` |
| `Assets/Textures/Obstacles/<종류>/` | `MoonRabbitBoard-Obstacles-<종류>` |

현재 아틀라스 11개: Blocks, PowerBlocks, Crate, Scrap, RecoveryCapsule, MetalRodBox, ColorLock, Generator, Web, Mold, Dust. 페이지는 최대 2048×2048이며 실제 크기는 Unity가 2의 거듭제곱으로 패킹한다. 프레임이 늘어 한 페이지를 넘으면 해당 아틀라스에 페이지를 추가한다. 페이지별 텍스처 전환은 발생할 수 있다.

`SpriteAtlasV2`, 회전·타이트 패킹 비활성, 패딩 4px, mipmap 비활성, RGBA32 무압축을 사용한다. 원본 PNG는 보존한다. 선택 메뉴와 플레이 테스트는 기존 표시 경로를 유지하며 편집 보드만 아틀라스 Sprite를 사용한다.

## 이미지와 프레임 추가

- 일반 블록은 `Blocks/`, 파워 블록은 `PowerBlocks/`에 추가한다.
- 새 장애물은 `Obstacles/<새 종류>/`에 추가한다. 별도 아틀라스와 번들이 자동 생성된다. 게임 규칙과 보드의 종류→이미지 매핑은 별도 구현이 필요하다.
- 기존 장애물의 추가 애니메이션은 해당 종류의 `Animations/` 하위 폴더에 넣는다. 일반 블록에도 같은 방식으로 프레임을 추가할 수 있다.
- 개별 프레임은 256×256 PNG를 기본으로 한다. 여러 프레임을 담은 시트는 Sprite Mode를 Multiple로 설정하고 슬라이스한다. 생성기는 Multiple과 슬라이스 정보를 보존하고 텍스처의 모든 Sprite를 패킹한다. 시트의 Import Max Size는 원본 프레임 해상도를 보존하도록 설정한다.
- 같은 아틀라스 안에서 Sprite 이름은 중복되지 않아야 한다. 이름으로 프레임을 조회하며 애니메이션 재생·순서 제어는 별도 기능이다.
- 기존 폴더는 확정된 내구도 이미지와 최신 Web v2 등을 포함하는 목록을 유지한다. 구버전 대안과 중복된 로켓 첫 프레임은 제외한다. 기존 `4frames` 파일은 Multiple로 슬라이스한 경우에만 포함한다.
- 페이지 분리는 프레임 연속 배치를 보장하지 않는다. 한 종류가 커져 로드 부담이 커지면 그 시점에 상태나 색상 단위의 추가 분리를 검토한다.

## 로딩과 수명

Addressables 4.1.0의 `Board Artwork` 그룹을 Pack Separately로 구성하여 아틀라스마다 로컬 번들을 만든다. 원격 호스팅은 구성하지 않았다. Include in Build는 꺼 중복 배포를 피한다.

`BoardSpriteAtlas.AddressFor(원본 상대 경로)`로 주소를 구하고 `new BoardSpriteAtlas(주소)` → `LoadAsync()` → `Get(Sprite 이름)`으로 사용한다. 소유자는 `Dispose()`로 복제 Sprite와 Addressables 핸들을 해제한다.

편집 보드는 표시할 이미지가 처음 요청될 때 해당 종류만 비동기 로드한다. 로드 완료 후 다시 그린다. 마지막 보드가 닫히거나 스크립트 리로드 시 로드한 아틀라스를 모두 해제한다. 창이 열린 동안 한 번 사용한 종류는 재사용을 위해 유지한다. 따라서 레벨을 바꿀 때마다 즉시 이전 종류를 내리지는 않는다.

## 생성과 빌드

`BoardAtlasPrebuild`가 플레이어 빌드 전 원본 목록, 아틀라스, Addressables 항목을 갱신한다. `BoardAtlasContentBuild`가 플레이어 콘텐츠 복사 전에 Addressables를 빌드한다. 실패하면 플레이어 빌드도 실패한다. Addressables의 플레이어 동반 자동 빌드는 중복 실행을 막기 위해 껐다.

원본 변경 후 에디터에서 번들 결과를 확인하려면 Unity 배치 실행에서 `-executeMethod Levels.Editor.BoardAtlasPrebuild.Prepare`로 갱신한다. 이 메서드는 작업 후 Unity 프로세스를 종료하므로 작업 중인 창에서 직접 호출하지 않는다.

## 검증

검사 결과는 `Logs/BoardArtworkVerification/results.txt`에 기록한다. 실제 번들 로드, 요청한 종류만 추가 로드, 해제, 모든 Sprite 조회, 페이지 크기 제한, 내구도·색상·겹침·삭제·데이터 보존을 검사한다.

실제 보드 드로콜은 Frame Debugger로 별도 측정해야 한다. 종류별 아틀라스는 하나의 거대 아틀라스보다 텍스처 전환이 늘 수 있지만 사용하지 않는 종류를 로드하지 않는다. 텍스트·클리핑·재질·그리기 순서도 배치 수에 영향을 준다.

2026-09-30 검증: Android Addressables 빌드 성공. 아틀라스 11개와 개별 번들 11개, 총 12페이지를 확인했다. 실제 번들·부분 로드·보드 회귀 검사 242개가 통과했다. 발전기는 2048×2048 두 페이지이며 나머지 종류는 각각 한 페이지다. 모든 종류를 동시에 로드하면 RGBA32 픽셀 메모리는 약 80.75MiB로, 분리 전보다 여백 비용이 늘어난다. 필요한 종류만 로드하는 것이 이 구성의 메모리 절감 조건이다.
