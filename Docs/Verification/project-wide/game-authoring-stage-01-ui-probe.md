# 게임·제작 도구 1단계 — 런타임 UI 기술 시제품

> 2026-10-08 · Unity Play Mode 검사 통과 / 렌더 이미지 확인 완료

## 결정

Windows 제작 도구 화면은 런타임 UI Toolkit을 우선 적용한다. 이는 전체 게임 HUD의 UI 프레임워크 변경이 아니다. 게임 플레이 화면의 기존 프리팹·월드 보드 표현은 그대로 재사용한다. 시제품 검사 결과를 확인한 뒤 4단계에서 보드·속성 패널부터 이식하고, 복잡한 튜토리얼 및 흐름 편집은 5단계에서 연결한다.

기존 UI를 그대로 Editor 폴더 밖으로 옮기지 않는다. 표시 요소와 편집 명령을 분리해야 한다.

## 실제 코드에서 확인한 재사용 경계

| 현재 코드 | 재사용할 부분 | 분리 또는 교체할 부분 |
|---|---|---|
| LevelEditor/Editor/Presentation/Board/LevelBoardView.cs | VisualElement 셀, PointerDown/Move/Up, 포인터 캡처, 선택·드래그 이벤트 방식 | Editor 영역의 LevelBrush/편집 모델, artwork 로더 및 partial 의존 |
| LevelEditor/Editor/Presentation/Board/LevelBoardArtwork.cs | 이미지 겹침·셀 위치 표현 규칙 | UnityEditor를 사용하는 로딩 경로를 런타임 이미지 공급자로 연결 |
| LevelEditorWindow.cs | ScrollView, 패널 구성과 보드/속성/도구 배치 | EditorWindow, SerializedObject, UnityEditor.UIElements 바인딩 |
| LevelEditorPlacementPanel.cs | 배치 규칙과 선택 항목 구조 | SerializedObject 변경을 3단계 공통 편집 명령으로 교체 |
| Levels/Editor/Inspection/LevelDefinitionDrawers.cs | 항목 이름·입력 의미·검증 규칙 | PropertyField, SerializedProperty, ObjectField를 일반 필드/ID 선택 UI로 교체 |
| LevelEditor/Editor/Styles/LevelEditor.uss | 색·간격·VisualElement 기반 스타일 일부 | Editor toolbar/전용 제어 스타일과 런타임 테마를 구분 |

런타임 UI Toolkit은 보드와 속성·긴 목록의 구성 개념을 재사용하기 쉽다. uGUI로 제작 도구 전체를 새로 구성하면 동일한 목록·속성·포인터 편집 구조를 다시 작성해야 한다. 반면 현재 게임 화면은 uGUI/월드 표현을 유지하므로 제작 도구의 ‘시험 실행’은 공통 게임 세션으로 전환하는 경계가 필요하다. 이 시제품은 두 UI 체계의 모든 팝업·입력 경합까지 해결했다는 의미가 아니다.

## 검사 구현

파일: Tests/Editor/Features/GameScreen/AuthoringRuntimeUIProbe.cs

실행:

    & Tools/Testing/ProjectTests.ps1 -Action Run -Method GameScreen.Editor.AuthoringRuntimeUIProbe.Run

- 미저장 씬이 있으면 변경 전에 중단한다.
- 원래 씬 구성, Play Mode 시작 씬, Enter Play Mode 설정을 보존한다.
- 빈 임시 씬에서 Play Mode로 들어가 UIDocument + PanelSettings + 1024×768 RenderTexture를 생성한다.
- Runtime 패널(ContextType.Player)에 실제로 연결됐는지 확인한다.
- 9×9 셀의 실제 레이아웃과 패널 Pick 좌표 판정을 검사한다.
- 선택 셀에 합성 PointerDownEvent를 전달하고 선택 모델 갱신을 확인한다.
- 속성 버튼에 NavigationSubmitEvent를 보내 선택된 셀의 내구도만 변경되는지 확인한다.
- 200개 항목의 실제 콘텐츠/뷰포트 크기, ScrollTo 후 좌표 이동 및 마지막 항목 가시성을 확인한다.
- 렌더 텍스처 픽셀이 단색이 아닌지 확인하고 PNG를 저장한다.
- Play Mode 종료 후 임시 오브젝트·텍스처와 설정을 정리하고 기존 씬을 복원한다.
- 제품 씬·프리팹·Build Profile에 시제품을 등록하지 않는다. Player/Addressables 빌드를 하지 않는다.

산출:
- Logs/GameAuthoringStage01/UIProbe/results.txt
- Logs/GameAuthoringStage01/UIProbe/runtime-panel.png

## 검사 한계

합성 입력 이벤트와 런타임 패널 hit-test를 검사한다. 실제 Windows 마우스/키보드 입력 장치, IME·한글 폰트, DPI·창 크기 변경, 파일 열기 대화상자, 성능 수치와 완성 레벨툴 동등성은 검증 범위 밖이다. 속성 변경 모델은 테스트용 81개 내구도 배열이며 실제 레벨 편집 명령과의 연결은 3/4단계 대상이다. ScrollTo 검사이며 OS 마우스 휠 입력 검사가 아니다.

## 실행 결과

2026-10-08 주 담당이 Unity 배치 실행(exit 0)과 runtime-panel.png를 확인했다. Player 패널 연결·9×9 레이아웃·셀 hit-test·포인터 선택·선택 칸 내구도 변경·200행 콘텐츠·스크롤 이동/마지막 행 가시성·실제 렌더 검사를 통과했다. 콘텐츠 높이 6400, 뷰포트 높이 540으로 실제 스크롤 영역을 확인했다. 최초 시제품은 빈 테마에서 뷰포트 제약이 없어 실패했고, 테스트 전용 테마·뷰포트 크기를 명시한 뒤 재검증했다. 최초 실패는 results-first-failure.txt에 보존했다.
