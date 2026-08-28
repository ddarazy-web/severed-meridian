# 확인된 프로젝트 프로필

안정적인 프로젝트 정보를 매번 다시 조사하지 않도록 이 문서를 사용한다. 업그레이드 작업에서 버전이나 의존성을 근거로 사용할 때는 명시된 원본 파일에서 다시 확인한다.

## 엔진과 핵심 기술 스택

- Unity Editor: `ProjectSettings/ProjectVersion.txt` 기준 `6000.3.10f1`
- 렌더 파이프라인: Universal Render Pipeline `17.3.0`
- 런타임 UI: UI 팀에서 제작한 프리팹을 전달받아 사용하며 프로젝트 차원의 신규 UI 프레임워크는 두지 않음
- Editor 전용 도구의 UI 선택지: uGUI `2.0.0`, UI Toolkit 모듈 및 에셋
- Input System: `1.18.0`
- Addressables: `2.9.0`, Addressable Importer `0.17.0`
- 비동기 처리: Cysharp UniTask
- 사용 가능한 의존성 주입 도구: VContainer `1.17.0`
- 사용 가능한 반응형 라이브러리: UniRx
- 사용 가능한 직렬화 도구: MemoryPack, Newtonsoft JSON
- 주요 콘텐츠 및 런타임 연동: Spine 4.2, Naninovel, Cinemachine, Firebase, Google Play Games 및 플랫폼 SDK 코드
- 테스트 및 프로파일링 패키지: Unity Test Framework, Memory Profiler, Profile Analyzer, Project Auditor 및 profiling core

패키지 버전은 `Packages/manifest.json`을 기준으로 한다. 실제 해석된 의존성 정보가 중요하면 `Packages/packages-lock.json`과 패키지 원본을 확인한다.

## 저장소 경계

- 프로젝트가 직접 관리하는 게임플레이 및 애플리케이션 코드는 주로 `Assets/Scripts` 아래에 있다.
- 프로젝트 문서는 `Docs` 아래에 있으며 주요 경로는 `Docs/architecture`, `Docs/Contents`, `Docs/Systems`, `Docs/Decisions/project-wide`, `Docs/Planning/project-wide`, `Docs/Handover`이다.
- `Assets/ExternalAssets`, `Assets/Packages`의 대부분, `LocalLibrary`, 생성된 IDE 프로젝트 파일, `Library`, `Temp`, `obj` 및 가져온 SDK·플러그인 디렉터리는 조사 결과가 달리 입증하기 전까지 외부 코드 또는 생성 코드로 취급한다.
- 기존 프로젝트 코드는 대부분 Unity 기본 어셈블리로 컴파일되며 asmdef는 주로 플러그인과 일부 독립 유틸리티에 사용된다. 작업 도중 새로운 어셈블리 경계를 부수적으로 도입하지 않는다.

## 확인된 로컬 규칙

- C# 서식은 `.editorconfig`를 최종 기준으로 한다. 현재 설정에는 공백 4칸 들여쓰기, CRLF, `var` 대신 명시적 타입, block-scoped namespace, 중괄호 및 저장소 명명 규칙 제안이 포함되어 있다.
- 기존 코드에는 여러 레거시 명명법과 전역 namespace 타입이 혼재한다. 작업 기회에 맞춘 광범위한 정리 대신 해당 하위 시스템의 기존 형태를 따른다.
- 저장소 지침에 따라 소스 수정 전에 조사와 해결 방향을 제시하고, 무관한 리팩터링을 피하며, 사용자 변경을 보존하고, 한글 주석을 사용하며, 다른 플랫폼의 영향을 확인하고, 완료 전 검증을 수행한다.
- 중요한 아키텍처 결정은 문서에 지정된 경로 아래 ADR로 기록한다. 단일 구현상의 선호를 알리지 않고 프로젝트 전체 규칙으로 바꾸지 않는다.
