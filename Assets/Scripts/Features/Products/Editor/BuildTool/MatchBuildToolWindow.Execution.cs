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
        private Label executionHeadline, executionHint;
        private string executionView;
        private BuildToolReport executionPreflight;

        private void CreateExecutionPanel(VisualElement root)
        {
            var panel = new ScrollView { name = "build-run-results", style = { maxHeight = 200, minHeight = 115, flexShrink = 0, marginTop = 8 } };
            root.Add(panel);
            var section = Section(panel, "execution-section", "05  실행 상태와 결과", "진행 중인 요청과 마지막 결과입니다. 위에서 조회하는 제품을 바꿔도 이미 확정한 요청은 바뀌지 않습니다.");
            executionHeadline = new Label { name = "execution-headline" }; executionHeadline.AddToClassList("badge"); section.Add(executionHeadline);
            executionHint = new Label(); executionHint.AddToClassList("caption"); section.Add(executionHint);
            var technical = new Foldout { text = "실행 상세 · 시간 / 확정 옵션 / 오류 / 경로", value = false, tooltip = "Player 결과와 후처리·정리 오류를 구분해 확인합니다. 내용을 선택·복사할 수 있습니다." }; section.Add(technical);
            executionResult = new Label { name = "build-run-status", selection = { isSelectable = true } }; executionResult.AddToClassList("technical"); technical.Add(executionResult);
            var buttons = new VisualElement(); buttons.AddToClassList("result-actions"); section.Add(buttons);
            openLogButton = new Button(() => EditorUtility.RevealInFinder(BuildToolController.Runner.Current.logPath)) { text = "로그 위치 열기", name = "open-build-log", tooltip = "현재 요청의 로그 파일이 실제 존재할 때 위치를 엽니다." }; buttons.Add(openLogButton);
            openOutputButton = new Button(() => EditorUtility.RevealInFinder(BuildToolController.Runner.Current.output)) { text = "출력 위치 열기", name = "open-build-output", tooltip = "실제 출력이 존재할 때 엽니다. 실패한 빌드의 부분 파일도 있을 수 있으므로 성공 여부는 결과를 확인하세요." }; buttons.Add(openOutputButton);
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
                if (!EditorUtility.DisplayDialog("제품 빌드 실행", item.Product.Name + "\n플랫폼: " + item.Target + "\n출력: " + BuildToolPlatforms.Default.Output(item.Product, resolved) + "\n옵션:\n" + (summary.Length == 0 ? "없음" : summary) +
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
            rootVisualElement.Q("platform-settings")?.SetEnabled(idle && !runner.IsLocked);
            buildButton.SetEnabled(idle && !EditorUtility.scriptCompilationFailed && !runner.IsLocked && item?.Product != null && !item.IsCopy && item.ReadIssues.Count == 0 &&
                executionPreflight != null && !executionPreflight.HasErrors && !executionPreflight.IsDeferred && optionError == null);
            cancelButton.SetEnabled(idle && runner.CanCancel);
            resolveButton.SetEnabled(idle && request?.phase == BuildToolRunPhase.NeedsAttention && !request.isMock);
            openLogButton.SetEnabled(request != null && File.Exists(request.logPath));
            openOutputButton.SetEnabled(request != null && !request.isMock && (File.Exists(request.output) || Directory.Exists(request.output)));
            string phase = request == null ? "대기 · 실행 요청 없음" : request.phase switch
            {
                BuildToolRunPhase.Succeeded => "완료 · 작업 성공", BuildToolRunPhase.Failed => "실패 · 상세 오류 확인",
                BuildToolRunPhase.Canceled => "취소됨", BuildToolRunPhase.NeedsAttention => "확인 필요 · 자동 실행 중단",
                BuildToolRunPhase.Building => "진행 · Player 빌드 중", BuildToolRunPhase.Postprocessing => "진행 · 후처리 중",
                BuildToolRunPhase.Finalizing => "진행 · 임시 상태 정리", BuildToolRunPhase.WaitingForEditor => "대기 · 플랫폼 전환 / 컴파일",
                _ => "진행 · 빌드 준비"
            };
            executionHeadline.text = (request == null ? "" : request.product + "  |  ") + phase;
            executionHeadline.EnableInClassList("good", request?.phase == BuildToolRunPhase.Succeeded && runner.StoreError == null);
            executionHeadline.EnableInClassList("error", runner.StoreError != null || request?.phase == BuildToolRunPhase.Failed || request?.phase == BuildToolRunPhase.NeedsAttention);
            executionHeadline.EnableInClassList("unknown", request == null || !request.IsTerminal && request.phase != BuildToolRunPhase.NeedsAttention);
            executionHint.text = runner.StoreError ?? request?.status ?? "먼저 제품과 옵션을 확인한 뒤 ‘선택 프로필 빌드…’를 누르세요. 확인 전에는 빌드를 실행하지 않습니다.";
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
