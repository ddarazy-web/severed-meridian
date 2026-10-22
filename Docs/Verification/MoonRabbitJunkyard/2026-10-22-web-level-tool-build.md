# 웹 레벨툴 실제 빌드·Chrome 검사

## 범위
- 오래된 사전 검사 조건 수정, 사전 검사 재실행, 실제 Web Level Editor 빌드 및 Chrome 실행.
- 커밋·푸시하지 않음. Android/Windows 레벨툴 프로필은 삭제하지 않음.
- 앞으로 레벨툴 빌드·검증 기준은 웹. 기존 네이티브 프로필 삭제는 별도 작업.

## 수정 및 통과 항목
- `BuildToolPreflightVerification`: 단일 `product.Scene` 대신 전체 `product.Scenes`와 순서까지 비교. 로비와 게임 씬을 함께 사용하는 현재 제품 정의에 맞춤.
- `Tools/Testing/ProjectTests.ps1 -Action Run -Method Products.Editor.BuildToolPreflightVerification.Run`: PASS, 종료 코드 0. 임시 테스트 사본 정리 완료.
- 검사 로그: `Logs/TestHarness/20261022-151408-919-Run.log`.
- `Tools/Build/Build.ps1 -Profile 'Web Level Editor' -PreserveLogs`: Succeeded, 종료 코드 0. 후처리·정리 오류 없음.
- 빌드 결과: `Builds/Web/LevelTool`.
- 결과 JSON: `Logs/ExternalBuild/20261022-151451-916-84af0153/result.json`.
- 빌드 시간: 2026-10-22 15:16:34~15:31:41 KST.
- Chrome에서 localhost HTTP로 실제 Unity WebGL 플레이어 실행. WebGL2 초기화, 한국어 UI, UI Addressables 로드, 파일 메뉴 및 새 프로젝트 경로 입력창 표시 확인.

## 재현된 제한 — 웹 편집 완료로 판단하지 않음
1. 새 프로젝트에서 브라우저 가상 파일 경로 `/tmp/WebLevelToolSmoke`를 입력하고 선택하면 `파일 처리 중…` 상태가 지속됨. 실제 사용자 작업 폴더는 사용하지 않음.
   - `LevelToolScreen.Storage.cs`의 파일 작업은 `UniTask.RunOnThreadPool`을 사용. 웹 실행 방식에 맞춘 비동기 파일 처리 검토가 필요함.
   - 저장·재실행 복구·레벨 편집·게임 시험은 이번 실행으로 검증하지 못함.
2. `LevelToolFileTransfer.CreateDefault()`에는 웹용 ZIP 선택/다운로드 구현이 없음. 브라우저 파일 업로드·다운로드와 영속 저장 대응이 필요함.
3. Unity 기본 웹 템플릿의 960×600 캔버스로 표시되어 큰 화면에 여백이 큼. 반응형 HTML 목업은 현재 Unity UI에 이식되지 않음.
4. Chrome 콘솔에 CoreCopy, StencilDitherMaskSeed, HDRDebugView의 미지원 셰이더 메시지가 기록됨. 최초 UI 표시는 가능했지만 그래픽 호환 검토가 남음.

## 증거
- [프로젝트 생성 대기 화면](WebLevelTool/chrome-project-create-pending.png)
- 콘솔: `Logs/WebLevelToolProfile/chrome-console.json`.
- 로컬 시험 서버: `http://127.0.0.1:18765/` (이 PC에서 서버 실행 중에만 접근 가능).

## 후속 작업
- 웹용 프로젝트 저장소·ZIP 가져오기/내보내기 구현과 스레드 풀 의존 파일 경로 전환.
- 반응형 웹 화면 적용 후 새 프로젝트 → 레벨 편집 → 저장 → 새로고침 복구 → ZIP 왕복 → 게임 시험 검증.
- 모바일 Chrome 실기기 검증은 미실시.
