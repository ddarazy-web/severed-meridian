using System;
using System.Linq;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStatePanel
    {
        [SerializeField] private bool manualMode;
        private Foldout manualDiagnostics;
        private Button manualStart;
        private Label manualSummary;

        private void CreateManualUI()
        {
            manualStart = null; manualSummary = null; manualDiagnostics = null;
            if (!manualMode) return;
            VisualElement root = rootVisualElement;
            root.AddToClassList("manual-play");
            root.Q<Label>(className: "editor-title").text = "MATCH / 플레이 테스트";
            VisualElement controls = new VisualElement { name = "manual-toolbar" };
            manualStart = new Button(() =>
            {
                CheckInput();
                if (execution != null) return;
                if (CurrentState == null) { Build(); return; }
                executionToggle.value = true;
                BeginManualCascade();
            }) { name = "manual-start", text = "시작" };
            controls.Add(manualStart);
            controls.Add(new Button(() => RestartManual(false)) { name = "manual-restart", text = "같은 시드로 다시" });
            controls.Add(new Button(() => RestartManual(true)) { name = "manual-new-seed", text = "새 시드로 다시" });
            controls.Add(new Button(() =>
            {
                Owner.SelectWorkspaceTab(0);
            }) { name = "manual-return", text = "편집으로 돌아가기" });
            root.Insert(2, controls);
            manualSummary = new Label { name = "manual-summary" }; root.Insert(3, manualSummary);
            VisualElement input = new VisualElement { name = "manual-input" }; root.Insert(4, input);
            input.Add(executeButton); input.Add(activateButton); input.Add(cancelSelection); input.Add(skipLastPangButton);
            manualDiagnostics = new Foldout { name = "manual-diagnostics", text = "상세 진단 · 단계 실행", value = false };
            ScrollView diagnosticScroll = new ScrollView(); manualDiagnostics.Add(diagnosticScroll);
            diagnosticScroll.Add(root.Q<VisualElement>("initial-toolbar"));
            diagnosticScroll.Add(root.Q<VisualElement>("initial-mode"));
            diagnosticScroll.Add(querySummary); diagnosticScroll.Add(root.Q<VisualElement>("execution-toolbar"));
            root.Insert(5, manualDiagnostics);
            root.Q<VisualElement>("initial-mode").style.display = DisplayStyle.None;
            executionToggle.style.display = DisplayStyle.None;
            manualDiagnostics.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != manualDiagnostics) return;
                if (evt.newValue) StopCascadeRun(); else BeginManualCascade();
                UpdateExecutionControls();
            });
        }

        private void RestartManual(bool newSeed)
        {
            if (newSeed)
            {
                int next;
                do { next = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0); } while (next == seed);
                seed = next; seedField.SetValueWithoutNotify(next);
            }
            Build();
        }

        // 시작 정규화·일반 행동·아이템 모두 기존 연쇄 실행 예약을 사용한다.
        private void BeginManualCascade()
        {
            if (!visible || !manualMode || manualDiagnostics == null || manualDiagnostics.value || cascadeRunning || execution?.HasPendingCascade != true) return;
            cascadeOwner = execution; cascadeRunning = true;
            EditorApplication.update += CascadeTick;
            UpdateExecutionControls();
        }

        private void UpdateManualControls()
        {
            if (!manualMode || manualStart == null) return;
            if (botSession != null) { UpdateBotControls(); return; }
            manualStart.SetEnabled(execution == null && !IsSearching && level != null);
            manualStart.text = CurrentState == null ? "검사·구성" : "시작";
            LevelRuntimeState shown = execution?.State ?? CurrentState;
            manualSummary.text = shown == null ? "레벨을 구성하면 시작할 수 있습니다. 재시작에는 같은 시드·부스터·조작이 필요합니다." :
                $"레벨 {shown.LevelNumber} · 시드 {seed} · 남은 이동 {shown.MovesRemaining}\n" +
                string.Join("  /  ", shown.Missions.Select(m => $"{LevelMissionRules.Name(m.Definition.Kind)}{(m.Definition.Kind == MissionKind.Color ? " 토" + ((int)m.Definition.Color + 1) : "")}: {m.Progress}/{m.Target}")) +
                (execution == null ? "\n부스터 선택 후 시작하세요. 재시작하면 부스터 선택도 초기화됩니다." :
                    "\n" + (execution.Outcome?.Message ?? "퍼즐 진행 중") + (execution.Outcome?.Kind == BoardOutcomeKind.Won ? " · " + execution.LastPangMessage : ""));
            rootVisualElement.Q<VisualElement>("initial-inspector").style.display = manualDiagnostics.value ? DisplayStyle.Flex : DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("initial-diagnostics").style.display = CurrentState == null || manualDiagnostics.value ? DisplayStyle.Flex : DisplayStyle.None;
            executionInfo.style.display = manualDiagnostics.value ? DisplayStyle.Flex : DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("booster-toolbar").style.display = execution == null ? DisplayStyle.Flex : DisplayStyle.None;
            if (execution == null && CurrentState != null)
                rootVisualElement.Q<Label>("initial-boundary").text = "시작 조건 통과 · 시험용 사본 준비";
            else if (execution != null)
                rootVisualElement.Q<Label>("initial-boundary").text = execution.Outcome != null ? execution.Outcome.Message :
                    cascadeRunning ? "후속 처리 중 · 입력 대기" : "두 칸 선택 후 교환 실행 · 파워 한 칸 선택 후 제자리 발동";
        }
    }
}
