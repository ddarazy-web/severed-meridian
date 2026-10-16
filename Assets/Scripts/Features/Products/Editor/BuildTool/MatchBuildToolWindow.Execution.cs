using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Products.Editor
{
    public sealed partial class MatchBuildToolWindow
    {
        private Button buildButton, cancelButton, resolveButton, openLogButton, openOutputButton;
        private Label executionResult;
        private string executionView;
        private BuildToolReport executionPreflight;

        private void CreateExecutionPanel(VisualElement root)
        {
            var panel = new ScrollView { name = "build-run-results", style = { maxHeight = 180, minHeight = 70, flexShrink = 0, marginTop = 8 } };
            root.Add(panel);
            executionResult = new Label { name = "build-run-status", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(executionResult);
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } }; panel.Add(buttons);
            openLogButton = new Button(() => EditorUtility.RevealInFinder(BuildToolController.Runner.Current.logPath)) { text = "요청 로그 위치", name = "open-build-log" }; buttons.Add(openLogButton);
            openOutputButton = new Button(() => EditorUtility.RevealInFinder(BuildToolController.Runner.Current.output)) { text = "출력 위치 · 부분 결과 가능", name = "open-build-output" }; buttons.Add(openOutputButton);
            executionView = null;
        }

        private void RequestBuild()
        {
            try
            {
                var backend = new UnityProfileBuildBackend(); var item = backend.Read(selectedGuid); backend.Validate(item);
                var choices = BuildToolPreferences.Choices(SelectionPath, selectedGuid);
                var resolved = OptionRegistry.Resolve(item.Product, choices);
                string summary = string.Join("\n", resolved.Select(state => state.id + " v" + state.version + " : " + (state.enabled ? "ON" : "OFF")));
                if (!EditorUtility.DisplayDialog("제품 빌드 실행", item.Product.Name + "\n플랫폼: " + item.Target + "\n출력: " + item.Product.Output + "\n옵션:\n" + (summary.Length == 0 ? "없음" : summary) +
                    "\n\n실제 Player와 Addressables 콘텐츠를 빌드합니다. 활성 제품·플랫폼이 바뀔 수 있습니다. SDK·서명·배포 성공은 아직 확인되지 않았습니다.", "빌드 시작", "취소")) return;
                BuildToolController.Runner.Start(selectedGuid, true, choices); RefreshExecution();
            }
            catch (Exception error) { notice.text = error.Message; }
        }

        private void CancelBuild()
        {
            try { BuildToolController.Runner.Cancel(); }
            catch (Exception error) { notice.text = error.Message; }
            RefreshExecution();
        }

        private void ResolveBuild()
        {
            if (!EditorUtility.DisplayDialog("미완료 요청 정리", "빌드를 다시 호출하지 않고 소유 임시 설정만 정리합니다. 외부 변경과 충돌하면 중단하고 이유를 표시합니다.", "정리", "취소")) return;
            try { BuildToolController.Runner.ResolveInterrupted(); }
            catch (Exception error) { notice.text = error.Message; }
            RefreshExecution();
        }

        private void RefreshExecution()
        {
            if (executionResult == null || buildButton == null) return;
            var runner = BuildToolController.Runner; var request = runner.Current;
            var item = profiles.FirstOrDefault(value => value.Guid == selectedGuid);
            bool idle = !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer;
            buildButton.SetEnabled(idle && !EditorUtility.scriptCompilationFailed && !runner.IsLocked && item?.Product != null && !item.IsCopy && item.ReadIssues.Count == 0 &&
                executionPreflight != null && !executionPreflight.HasErrors && !executionPreflight.IsDeferred && optionError == null);
            cancelButton.SetEnabled(idle && runner.CanCancel);
            resolveButton.SetEnabled(idle && request?.phase == BuildToolRunPhase.NeedsAttention && !request.isMock);
            openLogButton.SetEnabled(request != null && File.Exists(request.logPath));
            openOutputButton.SetEnabled(request != null && !request.isMock && (File.Exists(request.output) || Directory.Exists(request.output)));
            string view = (runner.StoreError == null ? "" : "기록 오류: " + runner.StoreError + "\n") + (request == null ? "실행 요청 없음 · 빌드 버튼에서 대상과 실제 실행 여부를 확인합니다." :
                (request.isMock ? "[모의 실행 · 배포 결과 아님] " : "[실제 요청] ") + request.product + " · " + request.phase + "\n" + request.status +
                "\n프로필: " + request.profilePath + "\n플랫폼: " + request.target + " · 출력: " + request.output +
                "\n시작(UTC): " + request.startedUtc + " · 종료: " + (request.finishedUtc ?? (request.phase == BuildToolRunPhase.NeedsAttention ? "중단 · 확인 필요" : "진행 중")) + " · " + request.ElapsedSeconds.ToString("F1") + "초" +
                "\nPlayer 결과: " + (request.result == null ? request.callStarted ? "완료 증거 없음" : "미호출" : request.result.outcome.ToString()) +
                "\n확정 옵션: " + (request.options.Count == 0 ? "없음" : string.Join(", ", request.options.Select(state => state.id + " v" + state.version + " " + (state.enabled ? "ON" : "OFF")))) +
                "\n후처리 오류: " + (request.postprocessError ?? "없음") +
                "\n오류: " + (request.error ?? "없음") + " · 정리 오류: " + (request.cleanupError ?? "없음") + "\n로그: " + request.logPath);
            if (view == executionView) return;
            executionView = view; executionResult.text = view;
        }
    }
}
