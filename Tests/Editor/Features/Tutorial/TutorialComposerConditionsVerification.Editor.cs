using System;
using System.IO;
using System.Linq;
using Board;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    public static partial class TutorialComposerConditionsVerification
    {
        private static void VerifyConditionEditor()
        {
            LevelDefinition packed = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(packed), level);
            JsonUtility.FromJsonOverwrite("{\"embeddedDefinitions\":[]}", level);
            UnityEngine.Object.DestroyImmediate(packed);
            level.Tutorial.steps.Clear(); level.Tutorial.steps.Add(TutorialSampleCatalog.CreateSwap(new BoardCoordinate(2, 2), new BoardCoordinate(2, 3), false));
            LevelBoardView board = new LevelBoardView(); board.Display(level, null);
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>(); host.ShowUtility(); host.rootVisualElement.Add(board);
            try
            {
                using LevelTutorialEditorPanel panel = new LevelTutorialEditorPanel(level, board, 0, _ => { }); host.rootVisualElement.Add(panel);
                void Click(string name)
                {
                    Button button = panel.Q<Button>(name); Check(button != null, "편집 제어 제공 " + name);
                    using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
                }
                Click("tutorial-condition-add-DurabilityDecrease");
                Check(level.Tutorial.steps[0].conditions.Count == 2 && level.Tutorial.steps[0].conditions[1].kind == TutorialConditionKind.DurabilityDecrease, "새 감소 조건은 독립된 기본값으로 추가");
                panel.Q<PopupField<string>>("tutorial-target-kind-1").value = "특정 개체";
                Click("tutorial-target-pick-1"); board.TutorialTargetPicked(new BoardCoordinate(4, 4));
                Check(level.Tutorial.steps[0].conditions[1].target.coordinate.Equals(new BoardCoordinate(4, 4)), "보드 셀 선택을 조건 개체에 저장");
                panel.Q<PopupField<string>>("tutorial-target-kind-1").value = "선택 영역";
                Click("tutorial-target-pick-1"); board.TutorialTargetPicked(new BoardCoordinate(4, 4)); board.TutorialTargetPicked(new BoardCoordinate(4, 5));
                Click("tutorial-target-confirm");
                Check(level.Tutorial.steps[0].conditions[1].target.cells.Count == 2, "다중 셀 대상 영역을 한 번에 확정");
                Check(board.Q("tutorial-targets").Children().OfType<Label>().Any(mark => mark.style.left.value.value == 4 * LevelBoardView.CellSize + 2 &&
                    mark.style.top.value.value == 4 * LevelBoardView.CellSize + 2), "편집 보드 자동 강조에 조건 영역 포함");
                Click("tutorial-target-pick-1"); board.TutorialTargetPicked(new BoardCoordinate(4, 6)); board.CancelStroke();
                Check(level.Tutorial.steps[0].conditions[1].target.cells.Count == 2 && panel.Q<Button>("tutorial-target-confirm") == null && board.TutorialTargetPicked == null,
                    "영역 선택 취소는 기존 값을 유지하고 확정 버튼과 선택 모드를 해제");
                Click("tutorial-condition-add-RemainingDurability");
                Check(panel.Q<Label>("tutorial-exact-durability-2") != null, "남은 내구도는 정확히 N으로 표시");
                Click("tutorial-condition-add-Generated");
                Check(panel.Q<PopupField<string>>("tutorial-power-kind-3") != null && panel.Q("tutorial-binding-name-3") != null, "등록 파워 선택과 생성 연결 이름 편집 제공");
                Click("tutorial-condition-add-MissionProgress");
                PopupField<string> missionChoice = panel.Q<PopupField<string>>("tutorial-condition-mission-4");
                Check(missionChoice != null && missionChoice.choices.Count == level.Missions.Count + 1 && panel.Q("tutorial-target-kind-4") == null, "미션 조건은 실제 미션 선택만 제공하고 개체 대상과 분리");
                missionChoice.value = missionChoice.choices.Last();
                Check(level.Tutorial.steps[0].conditions[4].missionIndex == level.Missions.Count - 1, "선택한 미션 항목 저장");
                string beforeArea = JsonUtility.ToJson(level);
                Click("tutorial-action-area-pick"); board.TutorialTargetPicked(new BoardCoordinate(2, 2)); board.TutorialTargetPicked(new BoardCoordinate(2, 3));
                Check(JsonUtility.ToJson(level) == beforeArea, "조작 영역 임시 선택은 레벨 배치나 저장 데이터를 변경하지 않음");
                Click("tutorial-action-area-confirm");
                Check(level.Tutorial.steps[0].actionArea.Count == 2, "조작 영역을 선택 완료로 한 번에 저장");
                Undo.PerformUndo(); Check(level.Tutorial.steps[0].actionArea.Count == 0, "조작 영역 확정 Undo");
                Undo.PerformRedo(); Check(level.Tutorial.steps[0].actionArea.Count == 2, "조작 영역 확정 Redo");
                Click("tutorial-action-area-pick"); board.TutorialTargetPicked(new BoardCoordinate(3, 3)); board.CancelStroke();
                Check(level.Tutorial.steps[0].actionArea.Count == 2 && board.TutorialTargetPicked == null && panel.Q("tutorial-action-area-confirm") == null, "조작 영역 취소는 기존 영역과 선택 수명주기 보존");
                Click("tutorial-action-area-clear");
                Check(level.Tutorial.steps[0].actionArea.Count == 0 && level.Tutorial.steps[0].hasFirst && level.Tutorial.steps[0].hasSecond, "영역 해제 후 기존 지정 칸으로 복귀");
                var safe = level.CreateElementCatalog().Definitions.First(value => value.DamageSourcePolicy?.AdjacentMatch == false && value.Placement != null);
                level.Tutorial.steps[0].conditions.Clear();
                level.Tutorial.steps[0].conditions.Add(new TutorialConditionDefinition { kind = TutorialConditionKind.Removed,
                    target = new TutorialTargetDefinition { kind = TutorialTargetKind.Definition, definitionId = safe.Id.Value },
                    allowedOrigins = new System.Collections.Generic.List<Simulation.EffectOrigin> { Simulation.EffectOrigin.AdjacentMatch } });
                Check(LevelTutorialValidator.Validate(level).Any(issue => issue.PropertyPath.Contains("allowedOrigins")), "피해 불허 원인은 대상 정의 설정으로 정적 오류");
                using (LevelTutorialEditorPanel capabilities = new LevelTutorialEditorPanel(level, board, 0, _ => { }))
                {
                    host.rootVisualElement.Add(capabilities);
                    Toggle origin = capabilities.Q<Toggle>("tutorial-origin-0-AdjacentMatch");
                    Check(origin != null && !origin.enabledSelf && !string.IsNullOrWhiteSpace(origin.tooltip), "대상이 허용하지 않는 원인은 비활성화하고 이유 제공");
                    Button repair = capabilities.Q<Button>("tutorial-origin-clear-invalid-0");
                    Check(repair != null, "대상 변경 후 비활성 원인을 제거할 수단 제공");
                    using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = repair; repair.SendEvent(evt); }
                    Check(level.Tutorial.steps[0].conditions[0].allowedOrigins.Count == 0, "비활성 선택값을 명시적으로 제거 가능");
                }
                level.Tutorial.steps[0].conditions[0] = new TutorialConditionDefinition { kind = TutorialConditionKind.DurabilityDecrease,
                    target = new TutorialTargetDefinition { kind = TutorialTargetKind.Definition,
                        definitionId = level.CreateElementCatalog().Definitions.First(value => value.Supply?.Content == Simulation.RuntimeContent.Rocket).Id.Value } };
                Check(LevelTutorialValidator.Validate(level).Any(issue => issue.Message.Contains("내구도")), "내구도 없는 종류의 감소 조건은 정적 오류");
                level.Tutorial.steps[0].conditions.Clear();
                using (LevelTutorialEditorPanel samples = new LevelTutorialEditorPanel(level, board, 0, _ => { }))
                {
                    host.rootVisualElement.Add(samples);
                    PopupField<string> sampleChoice = samples.Q<PopupField<string>>("tutorial-condition-sample");
                    Check(sampleChoice != null && sampleChoice.choices.Contains("내구도 합계 줄이기"), "내구도 기본 샘플 선택 제공");
                    Check(sampleChoice.choices.Contains("미션 진행 늘리기"), "미션 진행 기본 샘플 제공");
                    Check(level.Tutorial.steps[0].conditions.Count == 0 && samples.Q<Label>("tutorial-condition-sample-preview") != null, "샘플 미리보기는 적용 전 데이터를 변경하지 않음");
                    Button applySample = samples.Q<Button>("tutorial-condition-sample-apply");
                    using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = applySample; applySample.SendEvent(evt); }
                    Check(level.Tutorial.steps[0].conditions.Single().kind == TutorialConditionKind.DurabilityDecrease, "샘플 조건을 현재 단계에 복사 적용");
                    level.Tutorial.steps[0].conditions[0].requiredCount = 7;
                    applySample = samples.Q<Button>("tutorial-condition-sample-apply");
                    using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = applySample; applySample.SendEvent(evt); }
                    Check(level.Tutorial.steps[0].conditions[0].requiredCount == 7 && level.Tutorial.steps[0].conditions[1].requiredCount == 1, "샘플 반복 적용 사본은 서로 독립");
                }
                level.Tutorial.steps.Clear();
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"elements\":[{\"definitionId\":\"" + safe.Id.Value + "\",\"layer\":1,\"coordinate\":{\"row\":4,\"column\":4},\"durability\":2}]}", level);
                board.Display(level, null);
                using (LevelTutorialEditorPanel obstacleSamples = new LevelTutorialEditorPanel(level, board, 0, _ => { }))
                {
                    host.rootVisualElement.Add(obstacleSamples);
                    Button pick = obstacleSamples.Q<Button>("tutorial-sample-pick");
                    using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = pick; pick.SendEvent(evt); }
                    board.TutorialTargetPicked(new BoardCoordinate(4, 4));
                    Check(obstacleSamples.Q<PopupField<string>>("tutorial-obstacle-sample") != null && level.Tutorial.steps.Count == 0, "장애물 한 칸 선택으로 내구도/제거 샘플 미리보기");
                    Button apply = obstacleSamples.Q<Button>("tutorial-obstacle-sample-apply");
                    using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = apply; apply.SendEvent(evt); }
                    Check(level.Tutorial.steps.Count == 1 && level.Tutorial.steps[0].conditions[0].target.kind == TutorialTargetKind.Entity &&
                        level.Tutorial.steps[0].conditions[0].target.coordinate.Equals(new BoardCoordinate(4, 4)) && !level.Tutorial.steps[0].hasFirst,
                        "선택한 장애물을 조건 대상으로 복사하고 조작 셀은 임의 지정하지 않음");
                }
                level.Tutorial.steps.Clear(); level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item,
                    item = Simulation.BoardItem.Shuffle, freeItemCount = 2, instructions = "섞기" });
                using (LevelTutorialEditorPanel itemPanel = new LevelTutorialEditorPanel(level, board, 0, _ => { }))
                {
                    host.rootVisualElement.Add(itemPanel);
                    Check(itemPanel.Q<PropertyField>("tutorial-free-item-count") != null, "아이템 단계 무료 횟수 편집 제공");
                    Button add = itemPanel.Q<Button>("tutorial-condition-add-ItemUsed");
                    Check(add != null && itemPanel.Q<Button>("tutorial-condition-add-SuccessfulSwap") == null, "아이템에는 사용 조건을 제공하고 일반 교환 횟수는 제외");
                    using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = add; add.SendEvent(evt); }
                    Check(level.Tutorial.steps[0].conditions[0].item == Simulation.BoardItem.Shuffle && itemPanel.Q<PopupField<string>>("tutorial-condition-item-0") != null,
                        "사용 조건은 현재 단계 아이템으로 초기화하고 종류 편집 제공");
                    Check(itemPanel.Q<PopupField<string>>("tutorial-condition-sample").choices[0].Contains("섞기"), "아이템 단계에서는 해당 아이템 샘플을 우선 표시");
                }
            }
            finally { host.Close(); UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
